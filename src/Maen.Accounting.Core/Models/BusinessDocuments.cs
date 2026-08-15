namespace Maen.Accounting.Core.Models;

public enum ContactType
{
    Customer = 1,
    Supplier = 2
}

public sealed record Contact(
    string ContactId,
    string UserId,
    ContactType Type,
    string Name,
    string Phone = "",
    string Email = "",
    string Address = "",
    bool IsActive = true,
    DateTimeOffset? CreatedAtUtc = null,
    DateTimeOffset? UpdatedAtUtc = null,
    int Version = 1,
    string DeviceId = "");

public enum InvoiceType
{
    Sales = 1,
    Purchase = 2
}

public enum InvoiceStatus
{
    Draft = 1,
    Posted = 2,
    Voided = 3
}

public sealed record InvoiceLine(
    string LineId,
    string Description,
    long AmountMinor);

public sealed record Invoice(
    string InvoiceId,
    string UserId,
    string Number,
    InvoiceType Type,
    DateOnly IssueDate,
    DateOnly DueDate,
    string ContactId,
    IReadOnlyList<InvoiceLine> Lines,
    long TaxMinor = 0,
    InvoiceStatus Status = InvoiceStatus.Draft,
    string Notes = "",
    DateTimeOffset? CreatedAtUtc = null,
    DateTimeOffset? UpdatedAtUtc = null,
    int Version = 1,
    string DeviceId = "")
{
    public long SubtotalMinor => Lines.Sum(static line => line.AmountMinor);

    public long TotalMinor => checked(SubtotalMinor + TaxMinor);
}

public enum PaymentType
{
    CustomerReceipt = 1,
    SupplierPayment = 2
}

public sealed record Payment(
    string PaymentId,
    string UserId,
    string Number,
    PaymentType Type,
    DateOnly PaymentDate,
    string ContactId,
    long AmountMinor,
    string Notes = "",
    PaymentType? LinkedTo = null,
    DateTimeOffset? CreatedAtUtc = null,
    DateTimeOffset? UpdatedAtUtc = null,
    int Version = 1,
    string DeviceId = "",
    string AccountCode = "1000");
