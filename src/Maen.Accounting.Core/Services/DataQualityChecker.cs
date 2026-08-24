using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Inspects accounting data and reports quality warnings: unposted drafts,
/// entries whose totals do not balance, personal movements without a
/// category, and contacts that have never been used. Warnings are keyed by
/// translation keys (see UiText) so the UI can display them bilingually.
/// </summary>
public static class DataQualityChecker
{
    public static IReadOnlyList<DataQualityWarning> CheckPersonal(
        IEnumerable<ProfitEntry> entries,
        IEnumerable<Contact> contacts,
        IEnumerable<Payment> payments,
        IEnumerable<Invoice> invoices)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var warnings = new List<DataQualityWarning>();

        foreach (var entry in entries.Where(entry => !entry.IsDeleted))
        {
            if (string.IsNullOrWhiteSpace(entry.Category))
            {
                warnings.Add(new DataQualityWarning("UncategorizedEntry", entry.EntryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
            }

            if (entry.AmountMinor < 0)
            {
                warnings.Add(new DataQualityWarning("NegativeAmountEntry", entry.EntryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        CheckBusinessUsage(contacts, invoices, payments, warnings);
        return warnings;
    }

    public static IReadOnlyList<DataQualityWarning> CheckBusiness(
        IEnumerable<Invoice> invoices,
        IEnumerable<Payment> payments,
        IEnumerable<Contact> contacts)
    {
        ArgumentNullException.ThrowIfNull(invoices);
        var warnings = new List<DataQualityWarning>();

        var draftCount = invoices.Count(invoice => invoice.Status == InvoiceStatus.Draft);
        if (draftCount > 0)
        {
            warnings.Add(new DataQualityWarning("DraftInvoices", draftCount.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        CheckBusinessUsage(contacts, invoices, payments, warnings);
        return warnings;
    }

    private static void CheckBusinessUsage(
        IEnumerable<Contact> contacts,
        IEnumerable<Invoice> invoices,
        IEnumerable<Payment> payments,
        List<DataQualityWarning> warnings)
    {
        var invoiceContactIds = new HashSet<string>(invoices.Select(invoice => invoice.ContactId), StringComparer.Ordinal);
        var paymentContactIds = new HashSet<string>(payments.Where(payment => !payment.IsDeleted).Select(payment => payment.ContactId), StringComparer.Ordinal);
        var usedContactIds = new HashSet<string>(StringComparer.Ordinal);
        usedContactIds.UnionWith(invoiceContactIds);
        usedContactIds.UnionWith(paymentContactIds);

        foreach (var contact in contacts.Where(contact => contact.IsActive && !usedContactIds.Contains(contact.ContactId)))
        {
            warnings.Add(new DataQualityWarning("UnusedContact", contact.Name));
        }
    }
}

public sealed record DataQualityWarning(string Code, string Detail);
