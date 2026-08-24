using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

public sealed class DebtAgingCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 8, 16);

    private static Invoice Invoice(DateOnly issueDate, InvoiceType type, long totalMinor, int daysOutstanding) =>
        new Invoice(
            Guid.NewGuid().ToString("N"), "user", "INV",
            type, issueDate, issueDate, "c", new[] { new InvoiceLine("l1", "x", totalMinor) }, 0,
            InvoiceStatus.Posted, "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d");

    private static Payment Payment(long amountMinor, int daysBeforeToday) =>
        new Payment(Guid.NewGuid().ToString("N"), "user", "PAY", PaymentType.CustomerReceipt, Today.AddDays(-daysBeforeToday), "c", amountMinor, "", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d", "1000", false);

    [Fact]
    public void Age_NoOutstanding_ReturnsAllZeroSummaries()
    {
        var invoices = new[] { Invoice(Today.AddDays(-100), InvoiceType.Sales, 100_000, 100) };
        var payments = new[] { Payment(100_000, 1) };

        var result = DebtAgingCalculator.Age(invoices, payments, Today);

        Assert.Equal(0, result.Receivables.TotalMinor);
        Assert.Equal(0, result.Payables.TotalMinor);
        Assert.Equal(0, result.Receivables.DaysOverNinetyMinor);
    }

    [Fact]
    public void Age_OldSalesInvoice_GoesToOverNinetyBucket()
    {
        var invoices = new[] { Invoice(Today.AddDays(-100), InvoiceType.Sales, 45_000, 100) };
        var payments = new[] { Payment(15_000, 1) };

        var result = DebtAgingCalculator.Age(invoices, payments, Today);

        Assert.Equal(30_000, result.Receivables.TotalMinor);
        Assert.Equal(30_000, result.Receivables.DaysOverNinetyMinor);
        Assert.Equal(0, result.Receivables.CurrentMinor);
    }

    [Fact]
    public void Age_FreshInvoice_GoesToCurrentBucket()
    {
        var invoices = new[] { Invoice(Today, InvoiceType.Sales, 12_000, 0) };
        var payments = Array.Empty<Payment>();

        var result = DebtAgingCalculator.Age(invoices, payments, Today);

        Assert.Equal(12_000, result.Receivables.CurrentMinor);
        Assert.Equal(12_000, result.Receivables.TotalMinor);
    }

    [Fact]
    public void Age_DistributesInvoicesAcrossBucketsCorrectly()
    {
        var invoices = new[]
        {
            Invoice(Today, InvoiceType.Sales, 1_000, 0),
            Invoice(Today.AddDays(-15), InvoiceType.Sales, 2_000, 15),
            Invoice(Today.AddDays(-45), InvoiceType.Sales, 3_000, 45),
            Invoice(Today.AddDays(-75), InvoiceType.Sales, 4_000, 75),
            Invoice(Today.AddDays(-120), InvoiceType.Sales, 5_000, 120),
        };

        var result = DebtAgingCalculator.Age(invoices, payments: Array.Empty<Payment>(), Today);

        Assert.Equal(1_000, result.Receivables.CurrentMinor);
        Assert.Equal(2_000, result.Receivables.DaysOneToThirtyMinor);
        Assert.Equal(3_000, result.Receivables.DaysThirtyOneToSixtyMinor);
        Assert.Equal(4_000, result.Receivables.DaysSixtyOneToNinetyMinor);
        Assert.Equal(5_000, result.Receivables.DaysOverNinetyMinor);
        Assert.Equal(15_000, result.Receivables.TotalMinor);
    }

    [Fact]
    public void Age_SeparatesReceivablesFromPayables()
    {
        var invoices = new[]
        {
            Invoice(Today.AddDays(-40), InvoiceType.Sales, 2_000, 40),
            Invoice(Today.AddDays(-40), InvoiceType.Purchase, 7_000, 40),
        };

        var result = DebtAgingCalculator.Age(invoices, Array.Empty<Payment>(), Today);

        Assert.Equal(2_000, result.Receivables.DaysThirtyOneToSixtyMinor);
        Assert.Equal(2_000, result.Receivables.TotalMinor);
        Assert.Equal(7_000, result.Payables.DaysThirtyOneToSixtyMinor);
        Assert.Equal(7_000, result.Payables.TotalMinor);
    }

    [Fact]
    public void Age_FutureInvoiceClampsToCurrentBucket()
    {
        var invoices = new[] { Invoice(Today.AddDays(3), InvoiceType.Sales, 9_000, -3) };

        var result = DebtAgingCalculator.Age(invoices, Array.Empty<Payment>(), Today);

        Assert.Equal(9_000, result.Receivables.CurrentMinor);
    }
}

public sealed class DataQualityCheckerTests
{
    private static ProfitEntry Entry(DateOnly date, long amountMinor, string category)
    {
        var now = DateTimeOffset.UtcNow;
        return new ProfitEntry(Guid.NewGuid().ToString("N"), "user", date, amountMinor, 0, 0, "", false, now, now, 1, "d", amountMinor, "salary", category);
    }

    private static Contact Contact(string id, bool active = true) =>
        new Contact(id, "user", ContactType.Customer, $"name {id}", "", "", "", active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d");

    private static Invoice Invoice(string contactId, InvoiceStatus status = InvoiceStatus.Posted) =>
        new Invoice(Guid.NewGuid().ToString("N"), "user", "INV", InvoiceType.Sales, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), contactId,
            new[] { new InvoiceLine("l1", "x", 100) }, 0, status, "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d");

    [Fact]
    public void CheckPersonal_FlagsUncategorizedAndNegativeEntries()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 1, 2), 5_000, "rent"),
            Entry(new DateOnly(2026, 2, 2), 7_000, ""),
            Entry(new DateOnly(2026, 3, 2), -3_000, "food"),
        };

        var warnings = DataQualityChecker.CheckPersonal(entries, Array.Empty<Contact>(), Array.Empty<Payment>(), Array.Empty<Invoice>());

        Assert.Contains(warnings, warning => warning.Code == "UncategorizedEntry");
        Assert.Contains(warnings, warning => warning.Code == "NegativeAmountEntry");
        Assert.DoesNotContain(warnings, warning => warning.Code == "UncategorizedEntry" && warning.Detail == "2026-01-02");
    }

    [Fact]
    public void CheckBusiness_FlagsDraftInvoices()
    {
        var invoices = new[] { Invoice("c1", InvoiceStatus.Draft), Invoice("c1", InvoiceStatus.Draft), Invoice("c1") };

        var warnings = DataQualityChecker.CheckBusiness(invoices, Array.Empty<Payment>(), Array.Empty<Contact>());

        var draft = Assert.Single(warnings, warning => warning.Code == "DraftInvoices");

        Assert.Equal("2", draft.Detail);
    }

    [Fact]
    public void CheckPersonal_FlagsUnusedActiveContacts()
    {
        var contacts = new[] { Contact("c1"), Contact("c2") };
        var invoices = new[] { Invoice("c1") };

        var warnings = DataQualityChecker.CheckPersonal(Array.Empty<ProfitEntry>(), contacts, Array.Empty<Payment>(), invoices);

        Assert.Contains(warnings, warning => warning.Code == "UnusedContact" && warning.Detail == "name c2");
        Assert.DoesNotContain(warnings, warning => warning.Code == "UnusedContact" && warning.Detail == "name c1");
    }

    [Fact]
    public void CheckBusiness_IgnoresDeletedPaymentsWhenEvaluatingContactUsage()
    {
        var contacts = new[] { Contact("c1") };
        var payments = new[]
        {
            new Payment(Guid.NewGuid().ToString("N"), "user", "PAY", PaymentType.CustomerReceipt, new DateOnly(2026, 1, 1), "c1", 1_000, "", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d", "1000", true),
        };

        var warnings = DataQualityChecker.CheckBusiness(Array.Empty<Invoice>(), payments, contacts);

        Assert.Contains(warnings, warning => warning.Code == "UnusedContact");
    }
}

public sealed class DataIntegrityServiceTests
{
    [Fact]
    public void ComputeProfitEntryHash_IsDeterministicAndVersionBound()
    {
        var a = DataIntegrityService.ComputeProfitEntryHash("e1", "u1", 1, 100, 20, 5, false);
        var b = DataIntegrityService.ComputeProfitEntryHash("e1", "u1", 1, 100, 20, 5, false);
        var c = DataIntegrityService.ComputeProfitEntryHash("e1", "u1", 2, 100, 20, 5, false);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void ComputeInvoiceHash_BindsStatusAndType()
    {
        var posted = DataIntegrityService.ComputeInvoiceHash("i1", "u1", 1, 1, 100, 0, (int)InvoiceStatus.Posted);
        var draft = DataIntegrityService.ComputeInvoiceHash("i1", "u1", 1, 1, 100, 0, (int)InvoiceStatus.Draft);
        var sale = DataIntegrityService.ComputeInvoiceHash("i1", "u1", 1, 2, 100, 0, (int)InvoiceStatus.Posted);

        Assert.NotEqual(posted, draft);
        Assert.NotEqual(posted, sale);
    }

    [Fact]
    public void ComputePaymentHash_BindsDeletionFlag()
    {
        var alive = DataIntegrityService.ComputePaymentHash("p1", "u1", 1, 1, 400, false);
        var deleted = DataIntegrityService.ComputePaymentHash("p1", "u1", 1, 1, 400, true);

        Assert.NotEqual(alive, deleted);
    }

    [Fact]
    public void Verify_RejectsWhitespaceAndMismatches()
    {
        Assert.False(DataIntegrityService.Verify("", "abc"));
        Assert.False(DataIntegrityService.Verify("abc", "   "));
        Assert.False(DataIntegrityService.Verify("abc", "def"));
        Assert.True(DataIntegrityService.Verify(" abc ", "abc"));
    }
}

public sealed class InputSanitizerTests
{
    [Fact]
    public void SanitizeName_TrimsAndCapsLength()
    {
        var longName = new string('x', 200);
        Assert.Equal(80, InputSanitizer.SanitizeName(longName).Length);
        Assert.Equal("a b", InputSanitizer.SanitizeName("  a b  "));
    }

    [Fact]
    public void SanitizeNotes_StripsControlCharacters()
    {
        var dirty = "\u0001hello\u0007 world\u0002";
        Assert.Equal("hello world", InputSanitizer.SanitizeNotes(dirty));
        Assert.Contains('\n', InputSanitizer.SanitizeNotes("line1\nline2"));
        Assert.Equal(300, InputSanitizer.SanitizeNotes(new string('x', 1000)).Length);
    }

    [Fact]
    public void SanitizeNumber_AcceptsValidNumericShapes()
    {
        Assert.Equal("12.34", InputSanitizer.SanitizeNumber("  12.34 "));
        Assert.Equal("abc", InputSanitizer.SanitizeNumber("abc\u0000"));
        Assert.Equal(40, InputSanitizer.SanitizeNumber(new string('9', 100)).Length);
    }
}

public sealed class LegacyBackupImportHardeningTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Import_StampesIntegrityHashOnV2Entries()
    {
        const string json = """
        {"version":2,"backupEmail":"person@example.com","entries":[
          {"entry_date":"2026-07-01","sales":10,"cost":0,"expenses":0,"notes":"ok"}
        ]}
        """;

        var result = LegacyBackupParser.Parse(json, "uid-a", "person@example.com", "device", Now);
        var entry = Assert.Single(result.Entries);

        var expected = DataIntegrityService.ComputeProfitEntryHash(entry.EntryId, entry.UserId, 1, entry.SalesMinor, entry.CostMinor, entry.ExpensesMinor, false);
        Assert.Equal(expected, entry.IntegrityHash);
    }

    [Fact]
    public void Import_StampesIntegrityHashOnV3EntriesWithOriginalVersion()
    {
        const string json = """
        {"version":3,"backupEmail":"person@example.com","entries":[
          {"entryId":"e1","userId":"uid-a","entryDate":"2026-07-01","salesMinor":5,"costMinor":2,"expensesMinor":1,"notes":"n","version":7}
        ]}
        """;

        var result = LegacyBackupParser.Parse(json, "uid-a", "person@example.com", "device", Now);
        var entry = Assert.Single(result.Entries);

        var expected = DataIntegrityService.ComputeProfitEntryHash("e1", "uid-a", 7, 5, 2, 1, false);
        Assert.Equal(expected, entry.IntegrityHash);
    }

    [Fact]
    public void Import_SanitizesLongNotesAndCategory()
    {
        const string json = """
        {"version":3,"backupEmail":"person@example.com","entries":[
          {"entryId":"e1","userId":"uid-a","entryDate":"2026-07-01","salesMinor":5,"costMinor":2,"expensesMinor":1,"notes":"<script>","category":"\u0000\u0001dirty","wallet":"\u0000","counterparty":""}
        ]}
        """;

        var result = LegacyBackupParser.Parse(json, "uid-a", "person@example.com", "device", Now);
        var entry = Assert.Single(result.Entries);

        Assert.Equal("<script>", entry.Notes);
        Assert.Equal("dirty", entry.Category);
        Assert.Equal("", entry.Wallet); // control-character-only wallet collapses to empty
        Assert.Equal("", entry.Counterparty);
    }

    [Fact]
    public void Import_SanitizesMovementTypeForLegacyFormat()
    {
        const string json = """
        {"version":2,"backupEmail":"person@example.com","entries":[
          {"entry_date":"2026-07-01","sales":10,"cost":0,"expenses":0,"notes":"n"}
        ]}
        """;

        var result = LegacyBackupParser.Parse(json, "uid-a", "person@example.com", "device", Now);
        var entry = Assert.Single(result.Entries);

        Assert.Equal(PersonalMovementTypes.OtherIncome, entry.MovementType);
    }
}
