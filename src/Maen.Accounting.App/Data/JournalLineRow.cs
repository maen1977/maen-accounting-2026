using Maen.Accounting.Core.Models;
using SQLite;

namespace Maen.Accounting.App.Data;

[Table("journal_lines")]
public sealed class JournalLineRow
{
    [PrimaryKey]
    public string LineId { get; set; } = string.Empty;

    [Indexed]
    public string EntryId { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string AccountId { get; set; } = string.Empty;

    public long DebitMinor { get; set; }
    public long CreditMinor { get; set; }
    public string Description { get; set; } = string.Empty;

    public static JournalLineRow FromModel(string entryId, string userId, JournalLine line) => new()
    {
        LineId = line.LineId,
        EntryId = entryId,
        UserId = userId,
        AccountId = line.AccountId,
        DebitMinor = line.DebitMinor,
        CreditMinor = line.CreditMinor,
        Description = line.Description
    };

    public JournalLine ToModel() => new(
        LineId ?? string.Empty,
        AccountId ?? string.Empty,
        DebitMinor,
        CreditMinor,
        Description ?? string.Empty);
}
