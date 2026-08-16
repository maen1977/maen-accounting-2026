using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Validates and applies lifecycle transitions on business documents:
/// posting, voiding invoices, deleting payments and invoices, and
/// deactivating contacts. Keeps every document consistent with its version
/// and status rules.
/// </summary>
public static class BusinessDocumentLifecycleService
{
    public static Result PostInvoice(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.Draft)
        {
            return Result.Fail("Only draft invoices can be posted");
        }

        if (invoice.Lines.Count == 0 || invoice.TotalMinor <= 0)
        {
            return Result.Fail("An invoice must have at least one line with a positive total");
        }

        if (invoice.DueDate < invoice.IssueDate)
        {
            return Result.Fail("The due date cannot be earlier than the issue date");
        }

        return Result.Success();
    }

    public static Result VoidInvoice(Invoice invoice, IEnumerable<Payment> payments)
    {
        if (invoice.Status != InvoiceStatus.Posted)
        {
            return Result.Fail("Only posted invoices can be voided");
        }

        var relatedPayments = payments.Count(payment => payment.ContactId == invoice.ContactId);
        if (relatedPayments > 0)
        {
            return Result.Fail("Delete the related payments before voiding this invoice");
        }

        return Result.Success();
    }

    public static Result DeleteInvoice(Invoice invoice, IEnumerable<Payment> payments)
    {
        if (invoice.Status == InvoiceStatus.Posted)
        {
            return Result.Fail("Void this invoice before deleting it");
        }

        var relatedPayments = payments.Count(payment => payment.ContactId == invoice.ContactId);
        if (relatedPayments > 0)
        {
            return Result.Fail("Delete the related payments before deleting this invoice");
        }

        return Result.Success();
    }

    public static Result DeletePayment(Payment payment, IEnumerable<Invoice> invoices)
    {
        if (payment.Type == PaymentType.CustomerReceipt)
        {
            var postedSales = invoices.Any(invoice =>
                invoice.Type == InvoiceType.Sales
                && invoice.Status == InvoiceStatus.Posted
                && invoice.ContactId == payment.ContactId);
            if (postedSales)
            {
                return Result.Fail("Remove this receipt before deleting it would affect posted sales reconciliation");
            }
        }

        return Result.Success();
    }

    public static Result DeactivateContact(Contact contact, IEnumerable<Invoice> invoices)
    {
        var hasPosted = invoices.Any(invoice =>
            invoice.ContactId == contact.ContactId && invoice.Status == InvoiceStatus.Posted);
        if (hasPosted)
        {
            return Result.Fail("This contact has posted invoices and cannot be deactivated");
        }

        return Result.Success();
    }

    public static Invoice BuildNextVersion(Invoice invoice, Action<InvoiceBuilder> configure)
    {
        var builder = new InvoiceBuilder(invoice);
        configure(builder);
        var next = builder.Build();
        return next with
        {
            Version = invoice.Version + 1,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    public readonly struct Result
    {
        private Result(bool ok, string? reason) { Ok = ok; Reason = reason ?? string.Empty; }

        public bool Ok { get; }
        public string Reason { get; }

        public static Result Success() => new(true, null);
        public static Result Fail(string reason) => new(false, reason);
    }

    public sealed class InvoiceBuilder
    {
        private readonly Invoice _source;
        private List<InvoiceLine>? _lines;
        private long? _taxMinor;
        private DateOnly? _issueDate;
        private DateOnly? _dueDate;
        private string? _number;
        private string? _notes;

        public InvoiceBuilder(Invoice source) { _source = source; }

        public void SetLines(IEnumerable<InvoiceLine> lines) { _lines = [..lines]; }
        public void AddLine(string description, long amountMinor)
        {
            _lines ??= [.._source.Lines];
            _lines.Add(new InvoiceLine(Guid.NewGuid().ToString(), description, amountMinor));
        }
        public void SetTaxMinor(long taxMinor) { _taxMinor = taxMinor; }
        public void SetIssueDate(DateOnly date) { _issueDate = date; }
        public void SetDueDate(DateOnly date) { _dueDate = date; }
        public void SetNumber(string number) { _number = number; }
        public void SetNotes(string notes) { _notes = notes; }

        public Invoice Build() => _source with
        {
            Lines = _lines ?? _source.Lines,
            TaxMinor = _taxMinor ?? _source.TaxMinor,
            IssueDate = _issueDate ?? _source.IssueDate,
            DueDate = _dueDate ?? _source.DueDate,
            Number = _number ?? _source.Number,
            Notes = _notes ?? _source.Notes,
        };
    }
}
