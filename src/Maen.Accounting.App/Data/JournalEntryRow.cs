using Maen.Accounting.Core.Models;
using SQLite;

namespace Maen.Accounting.App.Data;

[Table("journal_entries")]
public sealed class JournalEntryRow
{
    [PrimaryKey]
    public string EntryId { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string EntryDate { get; set; } = string.Empty;

    [Indexed]
    public string EntryNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public int Status { get; set; }
    public long TotalDebitMinor { get; set; }
    public long TotalCreditMinor { get; set; }
    public long CreatedAtUtcTicks { get; set; }
    public long UpdatedAtUtcTicks { get; set; }
    public int Version { get; set; }
    public string DeviceId { get; set; } = string.Empty;

    public static JournalEntryRow FromModel(JournalEntry entry) => new()
    {
        EntryId = entry.EntryId,
        UserId = entry.UserId,
        EntryDate = entry.EntryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        EntryNumber = entry.EntryNumber,
        Description = entry.Description,
        Reference = entry.Reference,
        Status = (int)entry.Status,
        TotalDebitMinor = entry.TotalDebitMinor,
        TotalCreditMinor = entry.TotalCreditMinor,
        CreatedAtUtcTicks = (entry.CreatedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime.Ticks,
        UpdatedAtUtcTicks = (entry.UpdatedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime.Ticks,
        Version = entry.Version,
        DeviceId = entry.DeviceId
    };

    public JournalEntry ToModel(IReadOnlyList<JournalLine> lines) => new(
        EntryId,
        UserId,
        DateOnly.ParseExact(EntryDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        EntryNumber,
        Description,
        lines,
        Enum.IsDefined(typeof(JournalEntryStatus), Status) ? (JournalEntryStatus)Status : JournalEntryStatus.Draft,
        Reference,
        new DateTimeOffset(CreatedAtUtcTicks, TimeSpan.Zero),
        new DateTimeOffset(UpdatedAtUtcTicks, TimeSpan.Zero),
        Version,
        DeviceId);
}
