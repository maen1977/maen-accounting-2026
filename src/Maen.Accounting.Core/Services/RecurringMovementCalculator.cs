namespace Maen.Accounting.Core.Services;

/// <summary>
/// Handles personal recurring movements (e.g. monthly salary, monthly rent):
/// decides which movements are due as of a given date, advances occurrences,
/// and builds the ProfitEntry a movement would generate.
/// </summary>
public static class RecurringMovementCalculator
{
    public const int MaxMissedDays = 7;

    /// <summary>
    /// Returns recurring movements that are due on or before <paramref name="asOf"/>
    /// but were not yet applied. A movement is skipped if its due date is more than
    /// <see cref="MaxMissedDays"/> days old, so stale rules do not flood the ledger.
    /// </summary>
    public static IReadOnlyList<RecurringMovement> DueMovements(
        IEnumerable<RecurringMovement> movements,
        DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(movements);

        var due = new List<RecurringMovement>();
        foreach (var movement in movements.Where(static item => item.IsActive))
        {
            var dueDate = movement.NextOccurrence;
            if (dueDate > asOf)
            {
                continue;
            }

            var daysLate = asOf.DayNumber - dueDate.DayNumber;
            if (daysLate > MaxMissedDays)
            {
                continue;
            }

            due.Add(movement);
        }

        return due.OrderBy(static item => item.NextOccurrence).ToArray();
    }

    public static DateOnly AdvanceOccurrence(DateOnly current, string cycle) =>
        cycle switch
        {
            "daily" => current.AddDays(1),
            "weekly" => current.AddDays(7),
            "monthly" => AddMonthsSafe(current, 1),
            "yearly" => AddMonthsSafe(current, 12),
            _ => current.AddDays(1)
        };

    private static DateOnly AddMonthsSafe(DateOnly date, int months)
    {
        var targetMonth = date.Month + months;
        var years = (targetMonth - 1) / 12;
        var month = (targetMonth - 1) % 12 + 1;
        var day = Math.Min(date.Day, DateOnly.FromDateTime(new DateTime(date.Year + years, month, 1)).AddMonths(1).AddDays(-1).Day);
        return new DateOnly(date.Year + years, month, day);
    }

    /// <summary>
    /// Builds a draft ProfitEntry a recurring movement would record, using the
    /// personal movement conventions: income entries carry SalesMinor, expenses
    /// carry ExpensesMinor.
    /// </summary>
    public static (long SalesMinor, long CostMinor, long ExpensesMinor) BuildAmounts(RecurringMovement movement)
    {
        ArgumentNullException.ThrowIfNull(movement);
        var amount = Math.Abs(movement.AmountMinor);
        return movement.Kind.Equals("income", StringComparison.OrdinalIgnoreCase)
            ? (amount, 0L, 0L)
            : (0L, 0L, amount);
    }

    public static int DaysUntil(RecurringMovement movement, DateOnly today) =>
        movement.NextOccurrence.DayNumber - today.DayNumber;
}

public sealed record RecurringMovement(
    string RecurringId,
    string UserId,
    string Title,
    string Category,
    long AmountMinor,
    string Kind,
    DateOnly StartDate,
    string Cycle,
    DateOnly NextOccurrence,
    bool IsActive,
    string Notes = "");
