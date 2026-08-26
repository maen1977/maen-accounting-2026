using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;
using SQLite;

namespace Maen.Accounting.App.Data;

[Table("payments")]
public sealed class PaymentRow
{
    [PrimaryKey]
    public string PaymentId { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string Number { get; set; } = string.Empty;
    public int Type { get; set; }
    public string PaymentDate { get; set; } = string.Empty;
    public string ContactId { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public string Notes { get; set; } = string.Empty;
    public long CreatedAtUtcTicks { get; set; }
    public long UpdatedAtUtcTicks { get; set; }
    public int Version { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string AccountCode { get; set; } = "1000";
    public bool IsDeleted { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;

    public static PaymentRow FromModel(Payment payment) => new()
    {
        PaymentId = payment.PaymentId,
        UserId = payment.UserId,
        Number = payment.Number,
        Type = (int)payment.Type,
        PaymentDate = payment.PaymentDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        ContactId = payment.ContactId,
        AmountMinor = payment.AmountMinor,
        Notes = payment.Notes,
        CreatedAtUtcTicks = (payment.CreatedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime.Ticks,
        UpdatedAtUtcTicks = (payment.UpdatedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime.Ticks,
        Version = payment.Version,
        DeviceId = payment.DeviceId,
        AccountCode = string.IsNullOrWhiteSpace(payment.AccountCode) ? "1000" : payment.AccountCode,
        IsDeleted = payment.IsDeleted,
        IntegrityHash = DataIntegrityService.ComputePaymentHash(
            payment.PaymentId,
            payment.UserId,
            payment.Version,
            (int)payment.Type,
            payment.AmountMinor,
            payment.IsDeleted)
    };

    public Payment ToModel() => new(
        PaymentId ?? string.Empty,
        UserId ?? string.Empty,
        Number ?? string.Empty,
        Enum.IsDefined(typeof(PaymentType), Type) ? (PaymentType)Type : PaymentType.CustomerReceipt,
        DateOnly.ParseExact(PaymentDate ?? string.Empty, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        ContactId ?? string.Empty,
        AmountMinor,
        Notes ?? string.Empty,
        CreatedAtUtc: new DateTimeOffset(CreatedAtUtcTicks, TimeSpan.Zero),
        UpdatedAtUtc: new DateTimeOffset(UpdatedAtUtcTicks, TimeSpan.Zero),
        Version: Version,
        DeviceId: DeviceId ?? string.Empty,
        AccountCode: string.IsNullOrWhiteSpace(AccountCode) ? "1000" : AccountCode,
        IsDeleted: IsDeleted);
}
