using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Classifies outstanding invoice amounts by how long they have remained
/// unpaid. Receivables (sales) and payables (purchases) are grouped into
/// aging buckets measured in days from the invoice issue date. Amounts are
/// in minor currency units (see <see cref="Money"/>).
/// </summary>
public static class DebtAgingCalculator
{
    public static DebtAgingResult Age(
        IEnumerable<Invoice> invoices,
        IEnumerable<Payment> payments,
        DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(payments);
        var reconciled = InvoiceReconciliationCalculator.Reconcile(invoices, payments);

        var sales = new List<ReconciledInvoice>();
        var purchases = new List<ReconciledInvoice>();
        foreach (var invoice in reconciled.Where(invoice => invoice.OutstandingMinor > 0))
        {
            if (invoice.Invoice.Type == InvoiceType.Sales)
            {
                sales.Add(invoice);
            }
            else if (invoice.Invoice.Type == InvoiceType.Purchase)
            {
                purchases.Add(invoice);
            }
        }

        return new DebtAgingResult(
            BuildSummary(sales, asOf),
            BuildSummary(purchases, asOf));
    }

    private static DebtAgingSummary BuildSummary(IReadOnlyList<ReconciledInvoice> outstanding, DateOnly asOf)
    {
        long current = 0;
        long oneToThirty = 0;
        long thirtyOneToSixty = 0;
        long sixtyOneToNinety = 0;
        long overNinety = 0;

        foreach (var invoice in outstanding)
        {
            var days = asOf.DayNumber - invoice.Invoice.IssueDate.DayNumber;
            if (days < 0)
            {
                days = 0;
            }

            var amount = invoice.OutstandingMinor;
            if (days <= 0)
            {
                current += amount;
            }
            else if (days <= 30)
            {
                oneToThirty += amount;
            }
            else if (days <= 60)
            {
                thirtyOneToSixty += amount;
            }
            else if (days <= 90)
            {
                sixtyOneToNinety += amount;
            }
            else
            {
                overNinety += amount;
            }
        }

        var total = checked(current + oneToThirty + thirtyOneToSixty + sixtyOneToNinety + overNinety);
        return new DebtAgingSummary(current, oneToThirty, thirtyOneToSixty, sixtyOneToNinety, overNinety, total);
    }
}

public sealed record DebtAgingSummary(
    long CurrentMinor,
    long DaysOneToThirtyMinor,
    long DaysThirtyOneToSixtyMinor,
    long DaysSixtyOneToNinetyMinor,
    long DaysOverNinetyMinor,
    long TotalMinor)
{
    public string FormatBucket(DebtAgingBucket bucket) => bucket switch
    {
        DebtAgingBucket.Current => Money.Format(CurrentMinor),
        DebtAgingBucket.OneToThirty => Money.Format(DaysOneToThirtyMinor),
        DebtAgingBucket.ThirtyOneToSixty => Money.Format(DaysThirtyOneToSixtyMinor),
        DebtAgingBucket.SixtyOneToNinety => Money.Format(DaysSixtyOneToNinetyMinor),
        DebtAgingBucket.OverNinety => Money.Format(DaysOverNinetyMinor),
        DebtAgingBucket.Total => Money.Format(TotalMinor),
        _ => Money.Format(TotalMinor)
    };
}

public sealed record DebtAgingResult(DebtAgingSummary Receivables, DebtAgingSummary Payables);

public enum DebtAgingBucket
{
    Current,
    OneToThirty,
    ThirtyOneToSixty,
    SixtyOneToNinety,
    OverNinety,
    Total
}
