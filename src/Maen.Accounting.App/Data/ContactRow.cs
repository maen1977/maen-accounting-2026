using Maen.Accounting.Core.Models;
using AccountingContact = Maen.Accounting.Core.Models.Contact;
using SQLite;

namespace Maen.Accounting.App.Data;

[Table("contacts")]
public sealed class ContactRow
{
    [PrimaryKey]
    public string ContactId { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    public int Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public long CreatedAtUtcTicks { get; set; }
    public long UpdatedAtUtcTicks { get; set; }
    public int Version { get; set; }
    public string DeviceId { get; set; } = string.Empty;

    public static ContactRow FromModel(AccountingContact contact) => new()
    {
        ContactId = contact.ContactId,
        UserId = contact.UserId,
        Type = (int)contact.Type,
        Name = contact.Name,
        Phone = contact.Phone,
        Email = contact.Email,
        Address = contact.Address,
        IsActive = contact.IsActive,
        CreatedAtUtcTicks = (contact.CreatedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime.Ticks,
        UpdatedAtUtcTicks = (contact.UpdatedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime.Ticks,
        Version = contact.Version,
        DeviceId = contact.DeviceId
    };

    public AccountingContact ToModel() => new(
        ContactId ?? string.Empty,
        UserId ?? string.Empty,
        Enum.IsDefined(typeof(ContactType), Type) ? (ContactType)Type : ContactType.Customer,
        Name ?? string.Empty,
        Phone ?? string.Empty,
        Email ?? string.Empty,
        Address ?? string.Empty,
        IsActive,
        new DateTimeOffset(CreatedAtUtcTicks, TimeSpan.Zero),
        new DateTimeOffset(UpdatedAtUtcTicks, TimeSpan.Zero),
        Version,
        DeviceId ?? string.Empty);
}
