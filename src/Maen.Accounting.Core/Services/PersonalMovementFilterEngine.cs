using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Applies category and date-range filters to personal movements alongside
/// keyword search, so the dashboard list can show an exact subset of records.
/// </summary>
public static class PersonalMovementFilterEngine
{
    public static FilteredMovements Apply(
        IEnumerable<ProfitEntry> entries,
        string searchText = "",
        string? category = null,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var query = entries.Where(static entry => !entry.IsDeleted);
        var trimmed = searchText.Trim();

        if (trimmed.Length > 0)
        {
            query = query.Where(entry =>
                entry.Notes.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                || entry.Category.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                || entry.EntryDate.ToString("yyyy-MM-dd").Contains(trimmed));
        }

        if (category is not null && category.Length > 0)
        {
            query = query.Where(entry =>
                entry.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        if (from.HasValue)
        {
            var fromDate = from.Value;
            query = query.Where(entry => entry.EntryDate >= fromDate);
        }

        if (to.HasValue)
        {
            var toDate = to.Value;
            query = query.Where(entry => entry.EntryDate <= toDate);
        }

        var filtered = query.OrderByDescending(static entry => entry.EntryDate).ToArray();
        return new FilteredMovements(
            filtered,
            filtered.Sum(static entry => entry.EffectiveAmountMinor > 0 ? entry.EffectiveAmountMinor : 0),
            filtered.Sum(static entry => checked(entry.CostMinor + entry.ExpensesMinor)));
    }
}

public sealed record FilteredMovements(
    ProfitEntry[] Entries,
    long DepositsMinor,
    long SpendingMinor)
{
    public long NetMinor => checked(DepositsMinor - SpendingMinor);
}
