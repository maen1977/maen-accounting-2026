using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Computes a simple financial health score (0–100) and related indicators
/// for personal finances from recent movements: savings rate, fixed-cost
/// ratio, and cash runway. Indicators are keyed so the UI can display them
/// bilingually via UiText.
/// </summary>
public static class FinancialHealthCalculator
{
    public static FinancialHealthSummary Assess(
        IEnumerable<ProfitEntry> entries,
        IReadOnlyList<FinancialPlan> recentPlans)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(recentPlans);
        var entriesList = entries.Where(entry => !entry.IsDeleted).ToArray();

        var byMonth = entriesList
            .GroupBy(entry => (entry.EntryDate.Year, entry.EntryDate.Month))
            .OrderByDescending(group => (group.Key.Year, group.Key.Month))
            .Take(3)
            .ToArray();

        long income = 0;
        long spending = 0;
        long fixedSpending = 0;
        foreach (var group in byMonth)
        {
            var monthIncome = group.Sum(entry => entry.SalesMinor);
            var monthSpending = group.Sum(entry => checked(entry.CostMinor + entry.ExpensesMinor));
            income += monthIncome;
            spending += monthSpending;
            if (group.Key == byMonth[0].Key && recentPlans.Count > 0)
            {
                var fixedCategories = new HashSet<string>(
                    recentPlans[0].CategoryLimits
                        .Where(limit => limit.MonthlyLimitMinor > 0)
                        .Select(static limit => limit.Category),
                    StringComparer.OrdinalIgnoreCase);
                fixedSpending = group
                    .Where(entry => entry.Category is not null && fixedCategories.Contains(entry.Category))
                    .Sum(entry => checked(entry.CostMinor + entry.ExpensesMinor));
            }
        }

        var savingsRatePercent = income <= 0 ? 0
            : checked((double)(income - spending) / income) * 100;
        var fixedCostRatioPercent = income <= 0 ? 0
            : checked((double)fixedSpending / income) * 100;

        var averageMonthlySpending = byMonth.Length == 0 ? 0
            : spending / byMonth.Length;
        var lastMonthIncome = byMonth.Length == 0 ? 0 : byMonth[0].Sum(static entry => entry.SalesMinor);
        var runwayMonths = averageMonthlySpending > 0
            ? checked((double)lastMonthIncome / averageMonthlySpending)
            : 0;

        var score = ComputeScore(savingsRatePercent, fixedCostRatioPercent, runwayMonths, byMonth.Length);
        var flags = new List<FinancialHealthFlag>();

        if (savingsRatePercent < 10 && income > 0)
        {
            flags.Add(new FinancialHealthFlag("LowSavingsRate", Math.Round(savingsRatePercent, 1)));
        }

        if (fixedCostRatioPercent > 60)
        {
            flags.Add(new FinancialHealthFlag("HighFixedCosts", Math.Round(fixedCostRatioPercent, 1)));
        }

        if (runwayMonths > 0 && runwayMonths < 1)
        {
            flags.Add(new FinancialHealthFlag("ShortRunway", Math.Round(runwayMonths, 1)));
        }

        if (byMonth.Length > 0 && income == 0 && spending > 0)
        {
            flags.Add(new FinancialHealthFlag("SpendingWithoutIncome", 0));
        }

        return new FinancialHealthSummary(
            score,
            Math.Round(savingsRatePercent, 1),
            Math.Round(fixedCostRatioPercent, 1),
            Math.Round(runwayMonths, 1),
            flags.ToArray());
    }

    private static int ComputeScore(
        double savingsRatePercent,
        double fixedCostRatioPercent,
        double runwayMonths,
        int monthsConsidered)
    {
        if (monthsConsidered == 0)
        {
            return 50;
        }

        double score = 50;
        score += Math.Clamp(savingsRatePercent, -50, 50);
        score -= Math.Clamp(fixedCostRatioPercent - 40, 0, 25);
        score += Math.Clamp(runwayMonths * 8, 0, 25);
        return (int)Math.Clamp(score, 0, 100);
    }
}

public sealed record FinancialHealthSummary(
    int Score,
    double SavingsRatePercent,
    double FixedCostRatioPercent,
    double RunwayMonths,
    IReadOnlyList<FinancialHealthFlag> Flags)
{
    public string ScoreLevel => Score switch
    {
        >= 80 => "Excellent",
        >= 60 => "Good",
        >= 40 => "Fair",
        >= 20 => "Weak",
        _ => "Critical"
    };
}

public sealed record FinancialHealthFlag(string Code, double Value);
