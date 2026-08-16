using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Models a user's recurring monthly financial plan (expected income and spending limits)
/// and tracks how actual entries are progressing against it.
/// Pure calculation — no UI, persistence, or localization dependency.
/// </summary>
public static class PersonalFinancialPlanCalculator
{
    public static PersonalPlanProgress TrackPlan(
        FinancialPlan plan,
        IEnumerable<ProfitEntry> entries,
        DateOnly asOfDate)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(entries);

        var monthEntries = entries
            .Where(entry => !entry.IsDeleted
                && entry.EntryDate.Year == asOfDate.Year
                && entry.EntryDate.Month == asOfDate.Month)
            .ToArray();

        var actualIncomeMinor = monthEntries.Sum(static entry => entry.SalesMinor);
        var actualSpendingMinor = monthEntries
            .Where(entry => entry.MovementType is PersonalMovementTypes.Purchase
                or PersonalMovementTypes.Expense
                or PersonalMovementTypes.Withdrawal)
            .Sum(static entry => checked(entry.CostMinor + entry.ExpensesMinor) > 0
                ? checked(entry.CostMinor + entry.ExpensesMinor)
                : entry.EffectiveAmountMinor);

        var categoryProgress = plan.CategoryLimits
            .Select(limit =>
            {
                var spent = monthEntries
                    .Where(entry => string.Equals(entry.Category?.Trim(), limit.Category?.Trim(), StringComparison.OrdinalIgnoreCase))
                    .Where(entry => entry.MovementType is PersonalMovementTypes.Purchase or PersonalMovementTypes.Expense)
                    .Sum(static entry => checked(entry.CostMinor + entry.ExpensesMinor) > 0
                        ? checked(entry.CostMinor + entry.ExpensesMinor)
                        : entry.EffectiveAmountMinor);

                return new PlanCategoryProgress(
                    limit.Category,
                    limit.MonthlyLimitMinor,
                    spent);
            })
            .Where(static item => item.LimitMinor > 0 || item.SpentMinor > 0)
            .OrderByDescending(static item => item.UtilizationPercent)
            .ToArray();

        return new PersonalPlanProgress(
            ExpectedIncomeMinor: plan.MonthlyIncomeMinor,
            ExpectedSpendingLimitMinor: plan.MonthlySpendingLimitMinor,
            ExpectedSavingsTargetMinor: plan.MonthlySavingsTargetMinor,
            ActualIncomeMinor: actualIncomeMinor,
            ActualSpendingMinor: actualSpendingMinor,
            IncomeUtilizationPercent: plan.MonthlyIncomeMinor <= 0 ? 0
                : checked((double)actualIncomeMinor / plan.MonthlyIncomeMinor) * 100,
            SavingsProgressPercent: plan.MonthlySavingsTargetMinor <= 0 ? 0
                : checked((double)actualSpendingMinor / (plan.MonthlyIncomeMinor - plan.MonthlySavingsTargetMinor)) * 100,
            CategoryProgress: categoryProgress);
    }

    public static bool IsValid(FinancialPlan plan)
    {
        if (plan is null) return false;
        if (plan.MonthlyIncomeMinor < 0) return false;
        if (plan.MonthlySpendingLimitMinor < 0) return false;
        if (plan.MonthlySavingsTargetMinor < 0) return false;
        if (plan.MonthlySavingsTargetMinor > plan.MonthlyIncomeMinor) return false;
        if (plan.CategoryLimits
                .Where(static limit => limit.MonthlyLimitMinor < 0)
                .Any())
        {
            return false;
        }

        return true;
    }
}

public sealed record FinancialPlan(
    long MonthlyIncomeMinor,
    long MonthlySpendingLimitMinor,
    long MonthlySavingsTargetMinor,
    IReadOnlyList<PlanCategoryLimit> CategoryLimits)
{
    public static FinancialPlan Empty => new(0, 0, 0, Array.Empty<PlanCategoryLimit>());
}

public sealed record PlanCategoryLimit(string Category, long MonthlyLimitMinor);

public sealed record PersonalPlanProgress(
    long ExpectedIncomeMinor,
    long ExpectedSpendingLimitMinor,
    long ExpectedSavingsTargetMinor,
    long ActualIncomeMinor,
    long ActualSpendingMinor,
    double IncomeUtilizationPercent,
    double SavingsProgressPercent,
    IReadOnlyList<PlanCategoryProgress> CategoryProgress);

public sealed record PlanCategoryProgress(
    string Category,
    long LimitMinor,
    long SpentMinor)
{
    public double UtilizationPercent => LimitMinor <= 0 ? (SpentMinor > 0 ? 100 : 0)
        : checked((double)SpentMinor / LimitMinor) * 100;

    public bool IsExceeded => LimitMinor > 0 && SpentMinor > LimitMinor;
}
