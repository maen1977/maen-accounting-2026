using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Computes a month-by-month business report: posted sales and purchases,
/// cash collected, cash paid to suppliers, net cash, and best/worst month.
/// Amounts use minor units (see <see cref="Money"/>).
/// </summary>
public static class BusinessMonthlyReportCalculator
{
    public static BusinessMonthlyReport Build(
        IEnumerable<Invoice> invoices,
        IEnumerable<Payment> payments,
        DateOnly fromMonth,
        DateOnly toMonth)
    {
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(payments);

        var months = MonthRange(fromMonth, toMonth);
        var postedInvoices = invoices.Where(invoice => invoice.Status == InvoiceStatus.Posted).ToArray();

        var salesByMonth = postedInvoices
            .Where(invoice => invoice.Type == InvoiceType.Sales)
            .GroupBy(invoice => new DateOnly(invoice.IssueDate.Year, invoice.IssueDate.Month, 1))
            .ToDictionary(group => group.Key, group => (SalesMinor: group.Sum(invoice => invoice.TotalMinor), PostedInvoiceCount: group.Count()));
        var purchasesByMonth = postedInvoices
            .Where(invoice => invoice.Type == InvoiceType.Purchase)
            .GroupBy(invoice => new DateOnly(invoice.IssueDate.Year, invoice.IssueDate.Month, 1))
            .ToDictionary(group => group.Key, group => (PurchasesMinor: group.Sum(invoice => invoice.TotalMinor), PostedInvoiceCount: group.Count()));
        var receiptsByMonth = payments
            .Where(payment => payment.Type == PaymentType.CustomerReceipt)
            .GroupBy(payment => new DateOnly(payment.PaymentDate.Year, payment.PaymentDate.Month, 1))
            .ToDictionary(group => group.Key, group => group.Sum(payment => payment.AmountMinor));
        var supplierByMonth = payments
            .Where(payment => payment.Type == PaymentType.SupplierPayment)
            .GroupBy(payment => new DateOnly(payment.PaymentDate.Year, payment.PaymentDate.Month, 1))
            .ToDictionary(group => group.Key, group => group.Sum(payment => payment.AmountMinor));

        var slices = months.Select(month => new MonthlySlice(
            month,
            SalesMinor: salesByMonth.TryGetValue(month, out var salesEntry) ? salesEntry.SalesMinor : 0,
            PurchasesMinor: purchasesByMonth.TryGetValue(month, out var purchasesEntry) ? purchasesEntry.PurchasesMinor : 0,
            ReceiptsMinor: receiptsByMonth.TryGetValue(month, out var receipts) ? receipts : 0,
            SupplierPaymentsMinor: supplierByMonth.TryGetValue(month, out var supplierPayments) ? supplierPayments : 0,
            PostedInvoiceCount: (salesByMonth.TryGetValue(month, out var entry1) ? entry1.PostedInvoiceCount : 0)
                + (purchasesByMonth.TryGetValue(month, out var entry2) ? entry2.PostedInvoiceCount : 0))).ToArray();

        if (slices.Length == 0)
        {
            throw new InvalidOperationException("The requested month range did not produce any report slices.");
        }

        var fallbackMonth = slices[0];
        var bestMonth = slices
            .Where(static slice => slice.NetCashMinor > 0)
            .OrderByDescending(static slice => slice.NetCashMinor)
            .FirstOrDefault();
        var worstMonth = slices
            .Where(static slice => slice.NetCashMinor < 0)
            .OrderBy(static slice => slice.NetCashMinor)
            .FirstOrDefault();

        return new BusinessMonthlyReport(
            slices,
            bestMonth ?? fallbackMonth,
            worstMonth ?? fallbackMonth,
            slices.Sum(static slice => slice.SalesMinor),
            slices.Sum(static slice => slice.PurchasesMinor),
            slices.Sum(static slice => slice.ReceiptsMinor),
            slices.Sum(static slice => slice.SupplierPaymentsMinor));
    }

    private static DateOnly[] MonthRange(DateOnly from, DateOnly to)
    {
        if (from.CompareTo(to) > 0)
        {
            (to, from) = (from, to);
        }

        var limit = 48;
        var result = new List<DateOnly>(limit);
        var current = new DateOnly(from.Year, from.Month, 1);
        var end = new DateOnly(to.Year, to.Month, 1);
        while (current.CompareTo(end) <= 0 && result.Count < limit)
        {
            result.Add(current);
            current = current.AddMonths(1);
        }

        return result.ToArray();
    }
}

public sealed record BusinessMonthlyReport(
    IReadOnlyList<MonthlySlice> Months,
    MonthlySlice BestMonth,
    MonthlySlice WorstMonth,
    long TotalSalesMinor,
    long TotalPurchasesMinor,
    long TotalReceiptsMinor,
    long TotalSupplierPaymentsMinor);

public sealed record MonthlySlice(
    DateOnly Month,
    long SalesMinor = 0,
    long PurchasesMinor = 0,
    long ReceiptsMinor = 0,
    long SupplierPaymentsMinor = 0,
    int PostedInvoiceCount = 0)
{
    public long NetCashMinor => checked(ReceiptsMinor - SupplierPaymentsMinor);
}
