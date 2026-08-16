using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Reconciles payments against invoices per contact and returns, for every
/// invoice, how much has been collected/paid and how much is still outstanding.
/// Payments are spread across the contact's posted invoices chronologically
/// (oldest first). Amounts use minor units (see <see cref="Money"/>).
/// </summary>
public static class InvoiceReconciliationCalculator
{
    public static IReadOnlyList<ReconciledInvoice> Reconcile(
        IEnumerable<Invoice> invoices,
        IEnumerable<Payment> payments)
    {
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(payments);

        var paymentByContact = new Dictionary<string, Queue<Payment>>();
        foreach (var payment in payments.OrderBy(payment => payment.PaymentDate))
        {
            if (!paymentByContact.TryGetValue(payment.ContactId, out var queue))
            {
                queue = new Queue<Payment>();
                paymentByContact[payment.ContactId] = queue;
            }

            queue.Enqueue(payment);
        }

        var result = new List<ReconciledInvoice>();
        foreach (var invoice in invoices.Where(invoice => invoice.Status == InvoiceStatus.Posted)
            .OrderBy(invoice => invoice.IssueDate))
        {
            var queue = paymentByContact.TryGetValue(invoice.ContactId, out var contactQueue)
                ? contactQueue
                : EmptyQueue;

            var outstanding = invoice.TotalMinor;
            var paidMinor = 0L;
            foreach (var payment in queue)
            {
                var take = Math.Min(payment.AmountMinor, outstanding);
                outstanding -= take;
                paidMinor = checked(paidMinor + take);
                if (outstanding <= 0)
                {
                    break;
                }
            }

            result.Add(new ReconciledInvoice(
                invoice,
                paidMinor,
                outstanding,
                outstanding <= 0 && invoice.TotalMinor > 0));
        }

        return result;
    }

    private static readonly Queue<Payment> EmptyQueue = new();
}

public sealed record ReconciledInvoice(
    Invoice Invoice,
    long PaidMinor,
    long OutstandingMinor,
    bool IsFullyPaid)
{
    public bool HasOutstanding => OutstandingMinor > 0;
}
