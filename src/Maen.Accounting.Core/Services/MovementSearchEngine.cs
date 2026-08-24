using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Searches personal ledger entries by keyword, date range, movement type, or category
/// without any UI, persistence, or localization dependency.
/// </summary>
public static class MovementSearchEngine
{
    public static IReadOnlyList<ProfitEntry> Search(
        IEnumerable<ProfitEntry> entries,
        MovementSearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(query);

        var keyword = (query.Keyword ?? string.Empty).Trim();

        var matches = entries
            .Where(entry => !entry.IsDeleted)
            .Where(entry => query.FromDate == default || entry.EntryDate >= query.FromDate)
            .Where(entry => query.ToDate == default || entry.EntryDate <= query.ToDate)
            .Where(entry => query.MovementTypes.Count == 0
                || query.MovementTypes.Contains(entry.MovementType, StringComparer.OrdinalIgnoreCase))
            .Where(entry => keyword.Length == 0 || MatchesKeyword(entry, keyword))
            .Where(entry => string.IsNullOrEmpty(query.EntryIdPrefix)
                || entry.EntryId.StartsWith(query.EntryIdPrefix, StringComparison.OrdinalIgnoreCase))
            .Where(entry => query.HasAttachment is null || entry.HasAttachment == query.HasAttachment)
            .Where(entry => query.IsRecurringSourced is null || IsRecurringSourced(entry) == query.IsRecurringSourced)
            .ToArray();

        return matches
            .OrderByDescending(entry => entry.EntryDate)
            .ThenByDescending(entry => entry.CreatedAtUtc)
            .ToArray();
    }

    private static bool IsRecurringSourced(ProfitEntry entry) =>
        entry.MovementType.Equals("recurring", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesKeyword(ProfitEntry entry, string keyword)
    {
        var needle = keyword.AsSpan();

        if ((entry.Notes ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((entry.Category ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((entry.Counterparty ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((entry.Wallet ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((entry.MovementType ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var effective = entry.EffectiveAmountMinor;
        if (TryParseMinorAmount(needle, out var parsed) && parsed == effective)
        {
            return true;
        }

        return false;
    }

    private static bool TryParseMinorAmount(ReadOnlySpan<char> text, out long amountMinor)
    {
        if (!long.TryParse(text, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out amountMinor))
        {
            if (decimal.TryParse(text, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var decimalAmount))
            {
                amountMinor = checked((long)(decimalAmount * 100));
                return true;
            }

            return false;
        }

        return true;
    }
}

public sealed record MovementSearchQuery(
    string Keyword = "",
    DateOnly FromDate = default,
    DateOnly ToDate = default,
    IReadOnlyCollection<string>? MovementTypes = null,
    string? EntryIdPrefix = null,
    bool? HasAttachment = null,
    bool? IsRecurringSourced = null)
{
    public IReadOnlyCollection<string> MovementTypes { get; } =
        MovementTypes ?? Array.Empty<string>();
}
