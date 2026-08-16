using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Evaluates a user's monthly spending budget against actual outflows: remaining
/// allowance, usage percent, and a projected end-of-month spend built from the
/// current daily run-rate. Surfaces alert thresholds at 80% and 100% usage.
/// </summary>
public static class BudgetPlannerCalculator
{
    public const double WarningThresholdPercent = 80.0;
    public const double ExceededThresholdPercent = 100.0;

    public static BudgetStatus Assess(
        long monthlyCapMinor,
        IReadOnlyList<ProfitEntry> outflows,
        DateOnly asOf)
    {
        if (monthlyCapMinor <= 0)
        {
            return new BudgetStatus(
                CapMinor: 0,
                SpentMinor: 0,
                RemainingMinor: 0,
                UsagePercent: 0,
                ProjectedMonthTotalMinor: 0,
                DailyRunRateMinor: 0,
                DaysRemaining: 0,
                Alert: BudgetAlert.None);
        }

        var month = new DateOnly(asOf.Year, asOf.Month, 1);
        var spent = outflows
            .Where(entry => !entry.IsDeleted
                && entry.IsOutflow
                && entry.EntryDate.Year == month.Year
                && entry.EntryDate.Month == month.Month)
            .Sum(entry => checked(entry.CostMinor + entry.ExpensesMinor));

        var remaining = monthlyCapMinor - spent;
        var usage = monthlyCapMinor > 0 ? Math.Min(100.0, (double)spent / monthlyCapMinor * 100.0) : 0.0;

        var daysElapsed = Math.Max(1, asOf.Day - 1 + 1);
        var daysInMonth = DateTime.DaysInMonth(asOf.Year, asOf.Month);
        var daysRemaining = Math.Max(0, daysInMonth - asOf.Day);
        var runRate = (double)spent / daysElapsed;
        var projectedTotal = (long)Math.Round(runRate * daysInMonth);

        var alert = usage >= ExceededThresholdPercent ? BudgetAlert.Exceeded
            : usage >= WarningThresholdPercent ? BudgetAlert.Warning
            : BudgetAlert.None;

        return new BudgetStatus(
            CapMinor: monthlyCapMinor,
            SpentMinor: spent,
            RemainingMinor: remaining,
            UsagePercent: Math.Round(usage, 1),
            ProjectedMonthTotalMinor: projectedTotal,
            DailyRunRateMinor: (long)Math.Round(runRate),
            DaysRemaining: daysRemaining,
            Alert: alert);
    }

    public static long ProjectedOverageMinor(BudgetStatus status) =>
        status.ProjectedMonthTotalMinor > status.CapMinor
            ? checked(status.ProjectedMonthTotalMinor - status.CapMinor)
            : 0;
}

public sealed record BudgetStatus(
    long CapMinor,
    long SpentMinor,
    long RemainingMinor,
    double UsagePercent,
    long ProjectedMonthTotalMinor,
    long DailyRunRateMinor,
    int DaysRemaining,
    BudgetAlert Alert)
{
    public string UsageText => $"{UsagePercent:0.#}%";
    public string SpentText => Money.Format(SpentMinor);
    public string CapText => Money.Format(CapMinor);
    public string RemainingText => Money.Format(Math.Max(0, RemainingMinor));
    public string ProjectedText => Money.Format(ProjectedMonthTotalMinor);
}

public enum BudgetAlert
{
    None,
    Warning,
    Exceeded
}
