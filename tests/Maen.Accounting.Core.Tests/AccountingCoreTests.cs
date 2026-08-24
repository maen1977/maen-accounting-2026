using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

public sealed class AccountingCoreTests
{
    [Fact]
    public void Default_chart_of_accounts_contains_required_system_accounts()
    {
        var accounts = DefaultChartOfAccounts.Create("user-1");

        Assert.Equal(12, accounts.Count);
        Assert.Contains(accounts, account => account.Code == DefaultChartOfAccounts.CashCode && account.Type == AccountType.Asset);
        Assert.Contains(accounts, account => account.Code == DefaultChartOfAccounts.BankCode && account.Type == AccountType.Asset);
        Assert.Contains(accounts, account => account.Code == DefaultChartOfAccounts.SalesRevenueCode && account.Type == AccountType.Revenue);
        Assert.All(accounts, account => Assert.True(account.IsSystem));
    }

    [Fact]
    public void Validator_accepts_balanced_posted_entry()
    {
        var entry = CreateBalancedEntry();
        var accountIds = DefaultChartOfAccounts.Create("user-1").Select(static account => account.AccountId).ToHashSet();

        var errors = JournalEntryValidator.ValidateForPosting(entry, accountIds);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validator_rejects_unbalanced_entry_and_mixed_line()
    {
        var entry = CreateBalancedEntry() with
        {
            Lines =
            [
                new JournalLine("line-1", DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.CashCode), DebitMinor: 1000, CreditMinor: 1),
                new JournalLine("line-2", DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.SalesRevenueCode), CreditMinor: 900)
            ]
        };

        var errors = JournalEntryValidator.ValidateForPosting(entry);

        Assert.Contains(errors, error => error.Contains("بين المدين والدائن", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("إجمالي المدين", StringComparison.Ordinal));
    }

    [Fact]
    public void Legacy_profit_entry_maps_to_balanced_posted_journal()
    {
        var now = DateTimeOffset.Parse("2026-01-10T08:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture);
        var profit = new ProfitEntry("entry-001", "user-1", new DateOnly(2026, 1, 10), 150_000, 60_000, 20_000, "بيع يومي", false, now, now, 1, "device-1");

        var journal = LegacyProfitJournalMapper.Map(profit);

        Assert.NotNull(journal);
        Assert.Equal("legacy-profit-entry-001", journal.EntryId);
        Assert.Equal(JournalEntryStatus.Posted, journal.Status);
        Assert.Equal(230_000, journal.TotalDebitMinor);
        Assert.Equal(journal.TotalDebitMinor, journal.TotalCreditMinor);
        Assert.Equal(6, journal.Lines.Count);
    }

    [Fact]
    public void Legacy_deleted_or_empty_entry_is_not_migrated()
    {
        var now = DateTimeOffset.UtcNow;
        var deleted = new ProfitEntry("deleted", "user-1", new DateOnly(2026, 1, 10), 100, 0, 0, "", true, now, now, 1, "device");
        var empty = deleted with { EntryId = "empty", IsDeleted = false, SalesMinor = 0 };

        Assert.Null(LegacyProfitJournalMapper.Map(deleted));
        Assert.Null(LegacyProfitJournalMapper.Map(empty));
    }

    [Fact]
    public void Posted_sales_invoice_creates_balanced_receivable_revenue_and_tax_journal()
    {
        var invoice = new Invoice(
            "invoice-1",
            "user-1",
            "INV-001",
            InvoiceType.Sales,
            new DateOnly(2026, 1, 20),
            new DateOnly(2026, 2, 20),
            "customer-1",
            [new InvoiceLine("line-1", "خدمة", 10_000)],
            TaxMinor: 1_500,
            Status: InvoiceStatus.Posted);

        var journal = DocumentJournalFactory.CreateInvoiceJournal(invoice);

        Assert.Equal(11_500, journal.TotalDebitMinor);
        Assert.Equal(journal.TotalDebitMinor, journal.TotalCreditMinor);
        Assert.Equal(3, journal.Lines.Count);
    }

    [Fact]
    public void Customer_receipt_creates_balanced_cash_and_receivable_journal()
    {
        var payment = new Payment(
            "payment-1",
            "user-1",
            "REC-001",
            PaymentType.CustomerReceipt,
            new DateOnly(2026, 1, 21),
            "customer-1",
            5_000);

        var journal = DocumentJournalFactory.CreatePaymentJournal(payment);

        Assert.Equal(5_000, journal.TotalDebitMinor);
        Assert.Equal(journal.TotalDebitMinor, journal.TotalCreditMinor);
        Assert.Equal(2, journal.Lines.Count);
    }

    [Fact]
    public void Customer_receipt_can_post_to_bank_account()
    {
        var payment = new Payment(
            "payment-bank-1",
            "user-1",
            "REC-BANK-001",
            PaymentType.CustomerReceipt,
            new DateOnly(2026, 1, 21),
            "customer-1",
            7_500,
            AccountCode: DefaultChartOfAccounts.BankCode);

        var journal = DocumentJournalFactory.CreatePaymentJournal(payment);

        Assert.Contains(journal.Lines, line => line.AccountId == DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.BankCode) && line.DebitMinor == 7_500);
        Assert.DoesNotContain(journal.Lines, line => line.AccountId == DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.CashCode));
    }

    [Fact]
    public void Backup_parser_rejects_unsupported_version()
    {
        const string json = "{\"version\":5,\"backupEmail\":\"user@example.com\",\"entries\":[]}";

        Assert.Throws<InvalidDataException>(() => LegacyBackupParser.Parse(
            json,
            "user-1",
            "user@example.com",
            "device-1",
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Backup_parser_rejects_backup_for_another_account()
    {
        const string json = "{\"version\":3,\"backupEmail\":\"other@example.com\",\"entries\":[]}";

        Assert.Throws<InvalidOperationException>(() => LegacyBackupParser.Parse(
            json,
            "user-1",
            "user@example.com",
            "device-1",
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Backup_parser_preserves_version_three_deleted_entry()
    {
        const string json = """
        {
          "version": 3,
          "backupEmail": "user@example.com",
          "entries": [
            {
              "entryId": "entry-1",
              "userId": "user-1",
              "entryDate": "2026-01-15",
              "salesMinor": 10000,
              "costMinor": 2000,
              "expensesMinor": 500,
              "notes": "deleted",
              "isDeleted": true,
              "createdAtUtc": "2026-01-15T08:00:00Z",
              "updatedAtUtc": "2026-01-15T09:00:00Z",
              "version": 2,
              "deviceId": "device-1"
            }
          ]
        }
        """;

        var result = LegacyBackupParser.Parse(
            json,
            "user-1",
            "user@example.com",
            "device-2",
            DateTimeOffset.Parse("2026-01-20T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        var entry = Assert.Single(result.Entries);
        Assert.True(entry.IsDeleted);
        Assert.Equal("entry-1", entry.EntryId);
        Assert.Equal(2, entry.Version);
        Assert.Equal("device-1", entry.DeviceId);
    }

    [Fact]
    public void Trial_balance_ignores_drafts_and_filters_by_date()
    {
        var accounts = DefaultChartOfAccounts.Create("user-1");
        var posted = CreateBalancedEntry();
        var draft = posted with
        {
            EntryId = "draft-1",
            Status = JournalEntryStatus.Draft,
            EntryDate = new DateOnly(2026, 2, 1)
        };

        var trialBalance = TrialBalanceCalculator.Build(
            accounts,
            [posted, draft],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31));

        Assert.True(trialBalance.IsBalanced);
        Assert.Equal(10_000, trialBalance.TotalDebitMinor);
        Assert.Equal(10_000, trialBalance.TotalCreditMinor);
        Assert.Equal(10_000, trialBalance.Accounts.Single(account => account.Account.Code == DefaultChartOfAccounts.CashCode).TotalDebitMinor);
    }

    private static JournalEntry CreateBalancedEntry() => new(
        "entry-1",
        "user-1",
        new DateOnly(2026, 1, 15),
        "JV-0001",
        "قيد اختبار",
        [
            new JournalLine("line-1", DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.CashCode), DebitMinor: 10_000),
            new JournalLine("line-2", DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.SalesRevenueCode), CreditMinor: 10_000)
        ],
        JournalEntryStatus.Posted);
}
