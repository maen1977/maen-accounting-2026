using Maen.Accounting.Core.Models;
namespace Maen.Accounting.Core.Services;
/// <summary>
/// Breaks down a month's personal spending by category with counts, totals,
/// share of spending, and ranking — used by the category report card.
/// Pure calculation — no UI, persistence, or localization dependency.
/// </summary>
public static class PersonalCategoryReportCalculator
{
    public static CategoryReport Build(
        IEnumerable<ProfitEntry> entries,
        DateOnly month)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var monthEntries = entries
            .Where(entry => !entry.IsDeleted
                && entry.EntryDate.Year == month.Year
                && entry.EntryDate.Month == month.Month)
            .Where(entry => entry.MovementType is PersonalMovementTypes.Purchase
                or PersonalMovementTypes.Expense)
            .ToArray();
        var byCategory = monthEntries
            .GroupBy(entry => string.IsNullOrWhiteSpace(entry.Category) ? null : entry.Category.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new CategoryBreakdown(
                Category: group.Key ?? string.Empty,
                EntriesCount: group.Count(),
                SpentMinor: group.Sum(static entry =>
                    checked(entry.CostMinor + entry.ExpensesMinor) > 0
                        ? checked(entry.CostMinor + entry.ExpensesMinor)
                        : entry.EffectiveAmountMinor)))
            .Where(static breakdown => breakdown.SpentMinor > 0 || !string.IsNullOrEmpty(breakdown.Category))
            .OrderByDescending(static breakdown => breakdown.SpentMinor)
            .ThenBy(static breakdown => breakdown.Category, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var totalSpendingMinor = byCategory.Sum(static breakdown => breakdown.SpentMinor);
        var ranked = byCategory
            .Select((breakdown, index) => breakdown with { Rank = index + 1 })
            .ToArray();
        return new CategoryReport(month.Year, month.Month, ranked, totalSpendingMinor);
    }
}
public sealed record CategoryReport(
    int Year,
    int Month,
    IReadOnlyList<CategoryBreakdown> Breakdowns,
    long TotalSpendingMinor)
{
    public double SharePercentFor(CategoryBreakdown breakdown) =>
        TotalSpendingMinor <= 0 || breakdown.SpentMinor <= 0 ? 0
            : checked((double)breakdown.SpentMinor / TotalSpendingMinor) * 100;
}
public sealed record CategoryBreakdown(
    string Category,
    int EntriesCount,
    long SpentMinor,
    int Rank = 0);
