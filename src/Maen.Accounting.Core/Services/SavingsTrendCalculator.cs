using Maen.Accounting.Core.Models;
namespace Maen.Accounting.Core.Services;
/// <summary>
/// Tracks actual savings (income minus spending) over the last N months and
/// compares each month against the financial plan's savings target —
/// shows whether the user is on track to meet their savings goal.
/// Pure calculation — no UI, persistence, or localization dependency.
/// </summary>
public static class SavingsTrendCalculator
{
    public static SavingsTrend Build(
        IEnumerable<ProfitEntry> entries,
        DateOnly asOfDate,
        int trailingMonths,
        FinancialPlan? plan = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (trailingMonths <= 0)
        {
            trailingMonths = 6;
        }
        var activeEntries = entries
            .Where(static entry => !entry.IsDeleted)
            .ToArray();
        var snapshots = new SavingsTrendPoint[trailingMonths];
        for (var offset = trailingMonths - 1; offset >= 0; offset--)
        {
            var target = asOfDate.AddMonths(-offset);
            var monthEntries = activeEntries
                .Where(entry => entry.EntryDate.Year == target.Year && entry.EntryDate.Month == target.Month)
                .ToArray();
            var income = monthEntries.Sum(static entry => entry.SalesMinor);
            var spending = monthEntries
                .Where(entry => entry.MovementType is PersonalMovementTypes.Purchase
                    or PersonalMovementTypes.Expense
                    or PersonalMovementTypes.Withdrawal)
                .Sum(static entry => checked(entry.CostMinor + entry.ExpensesMinor) > 0
                    ? checked(entry.CostMinor + entry.ExpensesMinor)
                    : entry.EffectiveAmountMinor);
            var saved = checked(income - spending);
            var targetMinor = plan?.MonthlySavingsTargetMinor ?? 0;
            snapshots[trailingMonths - 1 - offset] = new SavingsTrendPoint(
                target.Year,
                target.Month,
                income,
                spending,
                saved,
                targetMinor,
                targetMinor > 0 && saved >= targetMinor);
        }
        var totalSaved = snapshots.Sum(static point => point.SavedMinor);
        var targetTotal = snapshots.Count(static point => point.TargetMinor > 0);
        var monthsOnTarget = snapshots.Count(static point => point.IsOnTarget);
        return new SavingsTrend(
            asOfDate.Year,
            asOfDate.Month,
            snapshots,
            totalSaved,
            targetTotal,
            monthsOnTarget);
    }
}
public sealed record SavingsTrend(
    int ReferenceYear,
    int ReferenceMonth,
    IReadOnlyList<SavingsTrendPoint> Points,
    long TotalSavedMinor,
    int MonthsOnTargetEligible,
    int MonthsOnTarget)
{
    public int ConsecutiveOnTargetStreak =>
        Points
            .AsEnumerable()
            .Reverse()
            .TakeWhile(static point => point.IsOnTarget)
            .Count();
}
public sealed record SavingsTrendPoint(
    int Year,
    int Month,
    long IncomeMinor,
    long SpendingMinor,
    long SavedMinor,
    long TargetMinor,
    bool IsOnTarget);
