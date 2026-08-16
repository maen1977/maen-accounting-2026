using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Tracks named savings goals against actual personal movements: how much of
/// the target has been saved in the goal's category, whether the user is on
/// track relative to the deadline, and estimated completion date. Amounts are
/// in minor currency units (see <see cref="Money"/>).
/// </summary>
public static class SavingsGoalCalculator
{
    public static IReadOnlyList<SavingsGoalProgress> Track(
        IEnumerable<SavingsGoal> goals,
        IEnumerable<ProfitEntry> entries,
        DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(entries);
        var goalList = goals.Where(goal => !goal.IsClosed).ToArray();

        var depositsByCategory = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var spendingByCategory = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.Where(entry => !entry.IsDeleted))
        {
            if (string.IsNullOrEmpty(entry.Category))
            {
                continue;
            }

            if (entry.SalesMinor > 0)
            {
                depositsByCategory.TryGetValue(entry.Category, out var current);
                depositsByCategory[entry.Category] = checked(current + entry.SalesMinor);
            }

            var spent = checked(entry.CostMinor + entry.ExpensesMinor);
            if (spent > 0)
            {
                spendingByCategory.TryGetValue(entry.Category, out var current);
                spendingByCategory[entry.Category] = checked(current + spent);
            }
        }

        var results = new List<SavingsGoalProgress>(goalList.Length);
        foreach (var goal in goalList)
        {
            depositsByCategory.TryGetValue(goal.Category, out var deposits);
            spendingByCategory.TryGetValue(goal.Category, out var spent);
            var saved = checked(goal.AlreadySavedMinor + deposits - spent);
            if (saved < 0)
            {
                saved = 0;
            }

            var isComplete = saved >= goal.TargetMinor;
            double progressPercent = goal.TargetMinor <= 0 ? (saved > 0 ? 100 : 0)
                : checked((double)saved / goal.TargetMinor) * 100;

            var daysRemaining = goal.Deadline.DayNumber - asOf.DayNumber;
            var remainingMinor = Math.Max(goal.TargetMinor - saved, 0);
            var remainingDays = Math.Max(daysRemaining, 0);

            var isOnTrack = isComplete || (daysRemaining <= 0 ? remainingMinor == 0
                : remainingDays > 0 && remainingMinor > 0
                    ? (double)remainingMinor / remainingDays <= (goal.TargetMinor > 0 ? (double)goal.TargetMinor / Math.Max(goal.Deadline.DayNumber - goal.StartDate.DayNumber, 1) : 0)
                    : remainingMinor == 0);

            results.Add(new SavingsGoalProgress(
                goal, saved, isComplete,
                Math.Min(progressPercent, 100),
                isOnTrack,
                remainingMinor,
                Math.Max(daysRemaining, 0)));
        }

        return results.OrderBy(result => result.DaysRemaining)
            .ThenByDescending(result => result.IsOnTrack)
            .ToArray();
    }
}

public sealed record SavingsGoal(
    string GoalId,
    string UserId,
    string Title,
    string Category,
    long TargetMinor,
    long AlreadySavedMinor,
    DateOnly StartDate,
    DateOnly Deadline,
    bool IsClosed = false);

public sealed record SavingsGoalProgress(
    SavingsGoal Goal,
    long SavedMinor,
    bool IsComplete,
    double ProgressPercent,
    bool IsOnTrack,
    long RemainingMinor,
    int DaysRemaining)
{
    public string SavedText => Money.Format(SavedMinor);
    public string TargetText => Money.Format(Goal.TargetMinor);
    public string RemainingText => Money.Format(RemainingMinor);
}
