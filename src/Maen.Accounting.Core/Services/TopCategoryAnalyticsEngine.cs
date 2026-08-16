using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Category-level analytics over actual movements: top spending categories for a
/// given month, each with its amount, share of total spending, and peak/low months.
/// </summary>
public static class TopCategoryAnalyticsEngine
{
    public static IReadOnlyList<CategoryAnalytics> Analyze(
        IEnumerable<ProfitEntry> entries,
        DateOnly month)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var groups = entries
            .Where(entry => !entry.IsDeleted
                && entry.IsOutflow
                && entry.EntryDate.Year == month.Year
                && entry.EntryDate.Month == month.Month)
            .GroupBy(entry => string.IsNullOrWhiteSpace(entry.Category) ? "uncategorized" : entry.Category);

        var total = groups.Sum(group => group.Sum(entry => checked(entry.CostMinor + entry.ExpensesMinor)));
        if (total <= 0)
        {
            return [];
        }

        // Keep full outflow entries grouped by category for peak/low-month computation
        // across ALL available spending months, while month-filtered amounts drive the
        // rankings for the analyzed month.
        var allGroups = entries
            .Where(entry => !entry.IsDeleted && entry.IsOutflow)
            .GroupBy(entry => string.IsNullOrWhiteSpace(entry.Category) ? "uncategorized" : entry.Category)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var analytics = groups
            .Select(group =>
            {
                var amount = group.Sum(entry => checked(entry.CostMinor + entry.ExpensesMinor));

                var byMonth = allGroups[group.Key]
                    .GroupBy(entry => new DateOnly(entry.EntryDate.Year, entry.EntryDate.Month, 1))
                    .Select(inner => new MonthlyAmount(
                        inner.Key,
                        inner.Sum(entry => checked(entry.CostMinor + entry.ExpensesMinor)),
                        0))
                    .OrderByDescending(inner => inner.SpendingMinor)
                    .ToArray();

                var peak = byMonth.Length > 0 ? byMonth[0].Month : month;
                var low = byMonth.Length > 0 ? byMonth[^1].Month : month;

                return new CategoryAnalytics(
                    Category: group.Key,
                    AmountMinor: amount,
                    SharePercent: Math.Round((double)amount / total * 100, 1),
                    PeakMonth: peak,
                    LowMonth: low);
            })
            .OrderByDescending(item => item.AmountMinor)
            .ToArray();

        return analytics;
    }

    public static IReadOnlyList<MonthlyAmount> MonthlyTotals(
        IEnumerable<ProfitEntry> entries,
        int months)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var totals = entries
            .Where(entry => !entry.IsDeleted)
            .GroupBy(entry => new DateOnly(entry.EntryDate.Year, entry.EntryDate.Month, 1))
            .Select(group => new MonthlyAmount(
                Month: group.Key,
                IncomeMinor: group.Sum(entry => entry.SalesMinor),
                SpendingMinor: group.Sum(entry => checked(entry.CostMinor + entry.ExpensesMinor))))
            .OrderByDescending(item => item.Month)
            .Take(months)
            .OrderBy(item => item.Month)
            .ToArray();

        return totals;
    }
}

public sealed record CategoryAnalytics(
    string Category,
    long AmountMinor,
    double SharePercent,
    DateOnly PeakMonth,
    DateOnly LowMonth)
{
    public string AmountText => Money.Format(AmountMinor);
    public string PeriodText => $"{PeakMonth:yyyy-MM} — {LowMonth:yyyy-MM}";
    public string ShareText => $"{SharePercent:0.#}%";
}

public sealed record MonthlyAmount(DateOnly Month, long SpendingMinor, long IncomeMinor = 0)
{
    public long NetMinor => checked(IncomeMinor - SpendingMinor);
    public string MonthLabel => Month.ToString("yyyy-MM");
}
