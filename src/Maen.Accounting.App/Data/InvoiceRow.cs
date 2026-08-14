using Maen.Accounting.Core.Models;
using SQLite;

namespace Maen.Accounting.App.Data;

[Table("invoices")]
public sealed class InvoiceRow
{
    [PrimaryKey]
    public string InvoiceId { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string Number { get; set; } = string.Empty;
    public int Type { get; set; }
    public string IssueDate { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string ContactId { get; set; } = string.Empty;
    public long SubtotalMinor { get; set; }
    public long TaxMinor { get; set; }
    public long TotalMinor { get; set; }
    public int Status { get; set; }
    public string Notes { get; set; } = string.Empty;
    public long CreatedAtUtcTicks { get; set; }
    public long UpdatedAtUtcTicks { get; set; }
    public int Version { get; set; }
    public string DeviceId { get; set; } = string.Empty;

    public static InvoiceRow FromModel(Invoice invoice) => new()
    {
        InvoiceId = invoice.InvoiceId,
        UserId = invoice.UserId,
        Number = invoice.Number,
        Type = (int)invoice.Type,
        IssueDate = invoice.IssueDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        DueDate = invoice.DueDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        ContactId = invoice.ContactId,
        SubtotalMinor = invoice.SubtotalMinor,
        TaxMinor = invoice.TaxMinor,
        TotalMinor = invoice.TotalMinor,
        Status = (int)invoice.Status,
        Notes = invoice.Notes,
        CreatedAtUtcTicks = (invoice.CreatedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime.Ticks,
        UpdatedAtUtcTicks = (invoice.UpdatedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime.Ticks,
        Version = invoice.Version,
        DeviceId = invoice.DeviceId
    };

    public Invoice ToModel(IReadOnlyList<InvoiceLine> lines) => new(
        InvoiceId,
        UserId,
        Number,
        Enum.IsDefined(typeof(InvoiceType), Type) ? (InvoiceType)Type : InvoiceType.Sales,
        DateOnly.ParseExact(IssueDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        DateOnly.ParseExact(DueDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        ContactId,
        lines,
        TaxMinor,
        Enum.IsDefined(typeof(InvoiceStatus), Status) ? (InvoiceStatus)Status : InvoiceStatus.Draft,
        Notes,
        new DateTimeOffset(CreatedAtUtcTicks, TimeSpan.Zero),
        new DateTimeOffset(UpdatedAtUtcTicks, TimeSpan.Zero),
        Version,
        DeviceId);
}

[Table("invoice_lines")]
public sealed class InvoiceLineRow
{
    [PrimaryKey]
    public string LineId { get; set; } = string.Empty;

    [Indexed]
    public string InvoiceId { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long AmountMinor { get; set; }

    public static InvoiceLineRow FromModel(string invoiceId, string userId, InvoiceLine line) => new()
    {
        LineId = line.LineId,
        InvoiceId = invoiceId,
        UserId = userId,
        Description = line.Description,
        AmountMinor = line.AmountMinor
    };

    public InvoiceLine ToModel() => new(LineId, Description, AmountMinor);
}
