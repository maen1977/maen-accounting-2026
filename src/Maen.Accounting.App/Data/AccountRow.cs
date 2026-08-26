using Maen.Accounting.Core.Models;
using SQLite;

namespace Maen.Accounting.App.Data;

[Table("accounts")]
public sealed class AccountRow
{
    [PrimaryKey]
    public string AccountId { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public string ParentAccountId { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public long CreatedAtUtcTicks { get; set; }
    public long UpdatedAtUtcTicks { get; set; }
    public int Version { get; set; }
    public string DeviceId { get; set; } = string.Empty;

    public static AccountRow FromModel(Account account) => new()
    {
        AccountId = account.AccountId,
        UserId = account.UserId,
        Code = account.Code,
        Name = account.Name,
        Type = (int)account.Type,
        ParentAccountId = account.ParentAccountId ?? string.Empty,
        IsSystem = account.IsSystem,
        IsActive = account.IsActive,
        SortOrder = account.SortOrder,
        CreatedAtUtcTicks = (account.CreatedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime.Ticks,
        UpdatedAtUtcTicks = (account.UpdatedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime.Ticks,
        Version = account.Version,
        DeviceId = account.DeviceId
    };

    public Account ToModel() => new(
        AccountId ?? string.Empty,
        UserId ?? string.Empty,
        Code ?? string.Empty,
        Name ?? string.Empty,
        Enum.IsDefined(typeof(AccountType), Type) ? (AccountType)Type : AccountType.Asset,
        string.IsNullOrWhiteSpace(ParentAccountId) ? null : ParentAccountId,
        IsSystem,
        IsActive,
        SortOrder,
        new DateTimeOffset(CreatedAtUtcTicks, TimeSpan.Zero),
        new DateTimeOffset(UpdatedAtUtcTicks, TimeSpan.Zero),
        Version,
        DeviceId ?? string.Empty);
}
