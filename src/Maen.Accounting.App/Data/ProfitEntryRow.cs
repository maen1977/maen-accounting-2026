using Maen.Accounting.Core.Models;
using SQLite;

namespace Maen.Accounting.App.Data;

[Table("profit_entries")]
public sealed class ProfitEntryRow
{
    [PrimaryKey]
    public string EntryId { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string EntryDate { get; set; } = string.Empty;

    public long SalesMinor { get; set; }
    public long CostMinor { get; set; }
    public long ExpensesMinor { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public long CreatedAtUtcTicks { get; set; }
    public long UpdatedAtUtcTicks { get; set; }
    public int Version { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public string MovementType { get; set; } = "other";
    public string Category { get; set; } = string.Empty;
    public string Wallet { get; set; } = "main";
    public string Counterparty { get; set; } = string.Empty;

    public static ProfitEntryRow FromModel(ProfitEntry entry) => new()
    {
        EntryId = entry.EntryId,
        UserId = entry.UserId,
        EntryDate = entry.EntryDate.ToString("yyyy-MM-dd"),
        SalesMinor = entry.SalesMinor,
        CostMinor = entry.CostMinor,
        ExpensesMinor = entry.ExpensesMinor,
        Notes = entry.Notes,
        IsDeleted = entry.IsDeleted,
        CreatedAtUtcTicks = entry.CreatedAtUtc.UtcDateTime.Ticks,
        UpdatedAtUtcTicks = entry.UpdatedAtUtc.UtcDateTime.Ticks,
        Version = entry.Version,
        DeviceId = entry.DeviceId,
        AmountMinor = entry.AmountMinor,
        MovementType = entry.MovementType,
        Category = entry.Category,
        Wallet = entry.Wallet,
        Counterparty = entry.Counterparty
    };

    public ProfitEntry ToModel() => new(
        EntryId,
        UserId,
        DateOnly.ParseExact(EntryDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        SalesMinor,
        CostMinor,
        ExpensesMinor,
        Notes,
        IsDeleted,
        new DateTimeOffset(CreatedAtUtcTicks, TimeSpan.Zero),
        new DateTimeOffset(UpdatedAtUtcTicks, TimeSpan.Zero),
        Version,
        DeviceId,
        AmountMinor,
        string.IsNullOrWhiteSpace(MovementType) ? "other" : MovementType,
        Category,
        string.IsNullOrWhiteSpace(Wallet) ? "main" : Wallet,
        Counterparty);
}
