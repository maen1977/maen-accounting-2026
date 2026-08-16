using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Calculates a business's financial position: cash position, receivables, payables,
/// due-date buckets, and period revenue vs expenses — pure calculation, no UI or persistence.
/// </summary>
public static class BusinessFinancialSummaryCalculator
{
    public static BusinessFinancialSummary Summarize(
        IEnumerable<Contact> contacts,
        IEnumerable<Invoice> invoices,
        IEnumerable<Payment> payments,
        DateOnly asOfDate)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(payments);

        var activeContacts = contacts
            .Where(static contact => contact.IsActive)
            .ToArray();
        var postedInvoices = invoices
            .Where(invoice => invoice.Status == InvoiceStatus.Posted)
            .ToArray();
        var allPayments = payments.ToArray();

        var salesInvoices = postedInvoices
            .Where(invoice => invoice.Type == InvoiceType.Sales)
            .ToArray();
        var purchaseInvoices = postedInvoices
            .Where(invoice => invoice.Type == InvoiceType.Purchase)
            .ToArray();

        var customerReceipts = allPayments
            .Where(payment => payment.Type == PaymentType.CustomerReceipt)
            .Sum(static payment => payment.AmountMinor);
        var supplierPayments = allPayments
            .Where(payment => payment.Type == PaymentType.SupplierPayment)
            .Sum(static payment => payment.AmountMinor);

        var receivableGrossMinor = salesInvoices.Sum(static invoice => invoice.TotalMinor);
        var receivableNetMinor = checked(receivableGrossMinor - customerReceipts);
        var payableGrossMinor = purchaseInvoices.Sum(static invoice => invoice.TotalMinor);
        var payableNetMinor = checked(payableGrossMinor - supplierPayments);

        return new BusinessFinancialSummary(
            AsOfDate: asOfDate,
            TotalSalesMinor: salesInvoices.Sum(static invoice => invoice.TotalMinor),
            TotalPurchasesMinor: purchaseInvoices.Sum(static invoice => invoice.TotalMinor),
            CustomerReceiptsMinor: customerReceipts,
            SupplierPaymentsMinor: supplierPayments,
            ReceivableNetMinor: receivableNetMinor,
            PayableNetMinor: payableNetMinor,
            NetPositionMinor: checked(receivableNetMinor - payableNetMinor),
            DueSoonSalesMinor: BucketTotal(salesInvoices, customerReceipts, asOfDate, 0, 7),
            OverdueSalesMinor: BucketTotal(salesInvoices, customerReceipts, asOfDate, int.MinValue, -1),
            DueSoonPurchasesMinor: BucketTotal(purchaseInvoices, supplierPayments, asOfDate, 0, 7),
            OverduePurchasesMinor: BucketTotal(purchaseInvoices, supplierPayments, asOfDate, int.MinValue, -1),
            ActiveContacts: activeContacts.Length,
            PostedInvoices: postedInvoices.Length,
            TopContactsByBalance: TopContactsByBalance(activeContacts, postedInvoices, allPayments));
    }

    private static long BucketTotal(
        IReadOnlyList<Invoice> invoices,
        long paidMinor,
        DateOnly asOfDate,
        int minDaysOffset,
        int maxDaysOffset)
    {
        var bucketInvoices = invoices
            .Where(invoice =>
            {
                var days = invoice.DueDate.DayNumber - asOfDate.DayNumber;
                return days >= minDaysOffset && days <= maxDaysOffset;
            })
            .Sum(static invoice => invoice.TotalMinor);

        if (bucketInvoices <= 0) return 0;

        var unpaidShare = paidMinor <= 0 || bucketInvoices <= paidMinor
            ? bucketInvoices
            : checked(bucketInvoices - (long)(bucketInvoices * (double)paidMinor / invoices.Sum(static invoice => invoice.TotalMinor)));

        return unchecked((long)unpaidShare);
    }

    private static IReadOnlyList<ContactBalance> TopContactsByBalance(
        IReadOnlyList<Contact> contacts,
        IReadOnlyList<Invoice> postedInvoices,
        IReadOnlyList<Payment> payments)
    {
        var balances = contacts
            .Select(contact =>
            {
                var invoiced = postedInvoices
                    .Where(invoice => invoice.ContactId == contact.ContactId)
                    .Sum(static invoice => invoice.TotalMinor);
                var paid = payments
                    .Where(payment => payment.ContactId == contact.ContactId)
                    .Sum(static payment => payment.AmountMinor);
                var net = contact.Type == ContactType.Customer
                    ? checked(invoiced - paid)
                    : checked(paid - invoiced);
                return new ContactBalance(
                    contact.ContactId,
                    contact.Name,
                    contact.Type,
                    net);
            })
            .Where(item => item.NetBalanceMinor != 0)
            .OrderByDescending(item => Math.Abs(item.NetBalanceMinor))
            .Take(5)
            .ToArray();

        return balances;
    }
}

public sealed record BusinessFinancialSummary(
    DateOnly AsOfDate,
    long TotalSalesMinor,
    long TotalPurchasesMinor,
    long CustomerReceiptsMinor,
    long SupplierPaymentsMinor,
    long ReceivableNetMinor,
    long PayableNetMinor,
    long NetPositionMinor,
    long DueSoonSalesMinor,
    long OverdueSalesMinor,
    long DueSoonPurchasesMinor,
    long OverduePurchasesMinor,
    int ActiveContacts,
    int PostedInvoices,
    IReadOnlyList<ContactBalance> TopContactsByBalance);

public sealed record ContactBalance(
    string ContactId,
    string Name,
    ContactType Type,
    long NetBalanceMinor);
