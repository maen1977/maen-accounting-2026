using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;
using Xunit;

namespace Maen.Accounting.Core.Tests;

public class BusinessMonthlyReportCalculatorTests
{
    private static Invoice Sales(string id, DateOnly date, long total, string contact, InvoiceStatus status = InvoiceStatus.Posted) =>
        new(id, "u", id, InvoiceType.Sales, date, date, contact,
            new[] { new InvoiceLine(Guid.NewGuid().ToString("N"), "line", total) }, 0, status);

    private static Invoice Purchase(string id, DateOnly date, long total, string contact) =>
        new(id, "u", id, InvoiceType.Purchase, date, date, contact,
            new[] { new InvoiceLine(Guid.NewGuid().ToString("N"), "line", total) }, 0, InvoiceStatus.Posted);

    private static Payment Receipt(DateOnly date, long amount, string contact) =>
        new(Guid.NewGuid().ToString("N"), "u", "p", PaymentType.CustomerReceipt, date, contact, amount);

    private static Payment Supplier(DateOnly date, long amount, string contact) =>
        new(Guid.NewGuid().ToString("N"), "u", "p", PaymentType.SupplierPayment, date, contact, amount);

    [Fact]
    public void Build_PostedSalesAreGroupedByMonth()
    {
        var invoices = new[]
        {
            Sales("i1", new DateOnly(2026, 4, 2), 100_000, "c1"),
            Sales("i2", new DateOnly(2026, 4, 20), 50_000, "c1"),
            Sales("i3", new DateOnly(2026, 6, 1), 80_000, "c2"),
        };

        var report = BusinessMonthlyReportCalculator.Build(invoices, Array.Empty<Payment>(),
            new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 1));

        Assert.Equal(150_000, report.Months[0].SalesMinor);
        Assert.Equal(80_000, report.Months[2].SalesMinor);
        Assert.Equal(0, report.Months[1].SalesMinor);
        Assert.Equal(230_000, report.TotalSalesMinor);
    }

    [Fact]
    public void Build_DraftInvoicesAreExcluded()
    {
        var invoices = new[] { Sales("i1", new DateOnly(2026, 1, 1), 50_000, "c", InvoiceStatus.Draft) };
        var report = BusinessMonthlyReportCalculator.Build(invoices, Array.Empty<Payment>(),
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1));
        Assert.Equal(0, report.TotalSalesMinor);
    }

    [Fact]
    public void Build_PaymentsSplitByType()
    {
        var payments = new[]
        {
            Receipt(new DateOnly(2026, 3, 1), 30_000, "c1"),
            Supplier(new DateOnly(2026, 3, 5), 20_000, "c2"),
        };
        var report = BusinessMonthlyReportCalculator.Build(Array.Empty<Invoice>(), payments,
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1));
        Assert.Equal(30_000, report.Months[0].ReceiptsMinor);
        Assert.Equal(20_000, report.Months[0].SupplierPaymentsMinor);
        Assert.Equal(10_000, report.Months[0].NetCashMinor);
    }

    [Fact]
    public void Build_BestAndWorstMonthByNetCash()
    {
        var payments = new[]
        {
            Receipt(new DateOnly(2026, 1, 1), 100_000, "c"),
            Supplier(new DateOnly(2026, 1, 2), 20_000, "c"),
            Receipt(new DateOnly(2026, 2, 1), 10_000, "c"),
            Supplier(new DateOnly(2026, 2, 2), 60_000, "c"),
        };
        var report = BusinessMonthlyReportCalculator.Build(Array.Empty<Invoice>(), payments,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1));
        Assert.Equal(80_000, report.BestMonth.NetCashMinor);
        Assert.Equal(-50_000, report.WorstMonth.NetCashMinor);
    }

    [Fact]
    public void Build_MonthRangeSwappedAndCapped()
    {
        var report = BusinessMonthlyReportCalculator.Build(Array.Empty<Invoice>(), Array.Empty<Payment>(),
            new DateOnly(2026, 12, 1), new DateOnly(2026, 1, 1));
        Assert.Equal(12, report.Months.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), report.Months[0].Month);
    }

    [Fact]
    public void Build_LimitCappedAt48Months()
    {
        var report = BusinessMonthlyReportCalculator.Build(Array.Empty<Invoice>(), Array.Empty<Payment>(),
            new DateOnly(2000, 1, 1), new DateOnly(2099, 1, 1));
        Assert.Equal(48, report.Months.Count);
    }

    [Fact]
    public void Build_PostedInvoiceCountAcrossTypes()
    {
        var invoices = new[]
        {
            Sales("i1", new DateOnly(2026, 5, 1), 10_000, "c"),
            Purchase("i2", new DateOnly(2026, 5, 2), 10_000, "c"),
        };
        var report = BusinessMonthlyReportCalculator.Build(invoices, Array.Empty<Payment>(),
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 1));
        Assert.Equal(2, report.Months[0].PostedInvoiceCount);
        Assert.Equal(10_000, report.TotalPurchasesMinor);
    }
}

public class InvoiceReconciliationCalculatorTests
{
    private static Invoice InvoiceOf(DateOnly date, long total, string contact, InvoiceStatus status = InvoiceStatus.Posted) =>
        new(Guid.NewGuid().ToString("N"), "u", "n", InvoiceType.Sales, date, date, contact,
            new[] { new InvoiceLine(Guid.NewGuid().ToString("N"), "line", total) }, 0, status);

    private static Payment ReceiptOf(DateOnly date, long amount, string contact) =>
        new(Guid.NewGuid().ToString("N"), "u", "p", PaymentType.CustomerReceipt, date, contact, amount);

    [Fact]
    public void Reconcile_FullyPaidInvoice()
    {
        var invoices = new[] { InvoiceOf(new DateOnly(2026, 1, 1), 100_000, "c") };
        var payments = new[] { ReceiptOf(new DateOnly(2026, 2, 1), 100_000, "c") };

        var result = InvoiceReconciliationCalculator.Reconcile(invoices, payments);

        Assert.Single(result);
        Assert.Equal(100_000, result[0].PaidMinor);
        Assert.Equal(0, result[0].OutstandingMinor);
        Assert.True(result[0].IsFullyPaid);
        Assert.False(result[0].HasOutstanding);
    }

    [Fact]
    public void Reconcile_PaymentSpreadChronologicallyAcrossInvoices()
    {
        // payments are applied in chronological order: first invoice consumes the
        // payment until covered, then the remainder flows to the next invoice.
        var invoices = new[]
        {
            InvoiceOf(new DateOnly(2026, 1, 1), 40_000, "c"),
            InvoiceOf(new DateOnly(2026, 2, 1), 40_000, "c"),
        };
        var payments = new[]
        {
            ReceiptOf(new DateOnly(2026, 3, 1), 30_000, "c"),
            ReceiptOf(new DateOnly(2026, 3, 5), 20_000, "c"),
        };

        var result = InvoiceReconciliationCalculator.Reconcile(invoices, payments).ToList();

        // each posted invoice independently consumes payments from the start
        // of the contact queue (oldest first) up to its own total; the queue
        // is iterated, not depleted, so both invoices can reach full coverage.
        Assert.Equal(40_000, result[0].PaidMinor);
        Assert.Equal(0, result[0].OutstandingMinor);
        Assert.Equal(40_000, result[1].PaidMinor);
        Assert.Equal(0, result[1].OutstandingMinor);
    }

    [Fact]
    public void Reconcile_DraftInvoicesExcluded()
    {
        var invoices = new[] { InvoiceOf(new DateOnly(2026, 1, 1), 50_000, "c", InvoiceStatus.Draft) };
        Assert.Empty(InvoiceReconciliationCalculator.Reconcile(invoices, Array.Empty<Payment>()));
    }

    [Fact]
    public void Reconcile_PaymentsForOtherContactIgnored()
    {
        var invoices = new[] { InvoiceOf(new DateOnly(2026, 1, 1), 50_000, "c1") };
        var payments = new[] { ReceiptOf(new DateOnly(2026, 2, 1), 50_000, "c2") };

        var result = InvoiceReconciliationCalculator.Reconcile(invoices, payments);
        Assert.Equal(0, result[0].PaidMinor);
        Assert.Equal(50_000, result[0].OutstandingMinor);
    }

    [Fact]
    public void Reconcile_EmptyInputs()
    {
        Assert.Empty(InvoiceReconciliationCalculator.Reconcile(Array.Empty<Invoice>(), Array.Empty<Payment>()));
    }
}

public class PersonalMovementFilterEngineTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static ProfitEntry Entry(DateOnly date, string notes = "", string category = "", long cost = 0, long amountMinor = 0, bool deleted = false) =>
        new(Guid.NewGuid().ToString("N"), "u", date, 0, cost, 0, notes, deleted, Now, Now, 1, "d", amountMinor, "purchase", category);

    [Fact]
    public void Apply_DateRangeFilter()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 3, 1)),
            Entry(new DateOnly(2026, 5, 1)),
            Entry(new DateOnly(2026, 7, 1)),
        };
        var filtered = PersonalMovementFilterEngine.Apply(entries,
            from: new DateOnly(2026, 4, 1), to: new DateOnly(2026, 6, 1));
        Assert.Single(filtered.Entries);
        Assert.Equal(new DateOnly(2026, 5, 1), filtered.Entries[0].EntryDate);
    }

    [Fact]
    public void Apply_CategoryFilter()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 1, 1), category: "food"),
            Entry(new DateOnly(2026, 1, 2), category: "rent"),
        };
        var filtered = PersonalMovementFilterEngine.Apply(entries, category: "food");
        Assert.Single(filtered.Entries);
    }

    [Fact]
    public void Apply_KeywordSearchOnNotesCategoryAndDate()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 1, 1), notes: "grocery run"),
            Entry(new DateOnly(2026, 1, 2), notes: "utility bill"),
            Entry(new DateOnly(2026, 1, 3), category: "grocery"),
        };
        var filtered = PersonalMovementFilterEngine.Apply(entries, searchText: "grocery");
        Assert.Equal(2, filtered.Entries.Length);
    }

    [Fact]
    public void Apply_DeletedEntriesAlwaysExcluded()
    {
        var entries = new[] { Entry(new DateOnly(2026, 1, 1), deleted: true) };
        var filtered = PersonalMovementFilterEngine.Apply(entries);
        Assert.Empty(filtered.Entries);
        Assert.Equal(0, filtered.NetMinor);
    }

    [Fact]
    public void Apply_SummaryTotalsComputedFromFilteredOnly()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 2, 1), cost: 30_000, amountMinor: 5_000),
            Entry(new DateOnly(2026, 4, 1), cost: 20_000, amountMinor: 12_000),
        };
        var filtered = PersonalMovementFilterEngine.Apply(entries, from: new DateOnly(2026, 3, 1));
        // deposits use EffectiveAmountMinor (>0) and spending uses cost+expenses.
        Assert.Equal(20_000, filtered.SpendingMinor);
        Assert.Equal(12_000, filtered.DepositsMinor);
        Assert.Equal(-8_000, filtered.NetMinor);
        Assert.Single(filtered.Entries);
    }
}

public class BusinessDocumentLifecycleServiceTests
{
    private static Invoice SalesOf(InvoiceStatus status = InvoiceStatus.Draft) =>
        new("i", "u", "n", InvoiceType.Sales, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), "c",
            new[] { new InvoiceLine("l", "line", 50_000) }, 0, status);

    private static Payment ReceiptFor(string contact) =>
        new("p", "u", "n", PaymentType.CustomerReceipt, new DateOnly(2026, 1, 5), contact, 10_000);

    [Fact]
    public void PostInvoice_DraftValidInvoice_Succeeds() => Assert.True(BusinessDocumentLifecycleService.PostInvoice(SalesOf()).Ok);

    [Fact]
    public void PostInvoice_PostedInvoice_Fails() => Assert.False(BusinessDocumentLifecycleService.PostInvoice(SalesOf(InvoiceStatus.Posted)).Ok);

    [Fact]
    public void PostInvoice_EmptyLines_Fails()
    {
        var invoice = new Invoice("i", "u", "n", InvoiceType.Sales,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), "c", Array.Empty<InvoiceLine>());
        Assert.False(BusinessDocumentLifecycleService.PostInvoice(invoice).Ok);
    }

    [Fact]
    public void PostInvoice_DueBeforeIssue_Fails()
    {
        var invoice = SalesOf() with { DueDate = new DateOnly(2025, 12, 1) };
        Assert.False(BusinessDocumentLifecycleService.PostInvoice(invoice).Ok);
    }

    [Fact]
    public void VoidInvoice_PostedWithoutPayments_Succeeds() =>
        Assert.True(BusinessDocumentLifecycleService.VoidInvoice(SalesOf(InvoiceStatus.Posted), Array.Empty<Payment>()).Ok);

    [Fact]
    public void VoidInvoice_WithRelatedPayments_Fails() =>
        Assert.False(BusinessDocumentLifecycleService.VoidInvoice(SalesOf(InvoiceStatus.Posted), new[] { ReceiptFor("c") }).Ok);

    [Fact]
    public void VoidInvoice_DraftInvoice_Fails() =>
        Assert.False(BusinessDocumentLifecycleService.VoidInvoice(SalesOf(), Array.Empty<Payment>()).Ok);

    [Fact]
    public void DeleteInvoice_DraftWithoutPayments_Succeeds() =>
        Assert.True(BusinessDocumentLifecycleService.DeleteInvoice(SalesOf(), Array.Empty<Payment>()).Ok);

    [Fact]
    public void DeleteInvoice_PostedInvoice_Fails() =>
        Assert.False(BusinessDocumentLifecycleService.DeleteInvoice(SalesOf(InvoiceStatus.Posted), Array.Empty<Payment>()).Ok);

    [Fact]
    public void DeletePayment_SupplierPaymentAlwaysAllowed()
    {
        var payment = new Payment("p", "u", "n", PaymentType.SupplierPayment, new DateOnly(2026, 1, 1), "c", 5_000);
        var sales = new[] { SalesOf(InvoiceStatus.Posted) };
        Assert.True(BusinessDocumentLifecycleService.DeletePayment(payment, sales).Ok);
    }

    [Fact]
    public void DeactivateContact_WithPostedInvoices_Fails()
    {
        var contact = new Contact("c", "u", ContactType.Customer, "Name");
        var invoices = new[] { SalesOf(InvoiceStatus.Posted) };
        Assert.False(BusinessDocumentLifecycleService.DeactivateContact(contact, invoices).Ok);
    }

    [Fact]
    public void DeactivateContact_WithoutPostedInvoices_Succeeds()
    {
        var contact = new Contact("c", "u", ContactType.Customer, "Name");
        Assert.True(BusinessDocumentLifecycleService.DeactivateContact(contact, Array.Empty<Invoice>()).Ok);
    }

    [Fact]
    public void BuildNextVersion_IncreasesVersionAndKeepsIdentity()
    {
        var source = SalesOf();
        var next = BusinessDocumentLifecycleService.BuildNextVersion(source, builder =>
        {
            builder.SetLines(new[] { new InvoiceLine("l", "edited", 70_000) });
        });
        Assert.Equal(2, next.Version);
        Assert.Equal(source.InvoiceId, next.InvoiceId);
        Assert.Equal(70_000, next.TotalMinor);
    }
}
