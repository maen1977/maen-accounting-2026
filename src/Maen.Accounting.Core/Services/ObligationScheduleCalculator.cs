using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Tracks recurring personal obligations (loan installments, rent, subscriptions, bills)
/// and classifies upcoming due dates as overdue, due soon, or future — pure calculation.
/// </summary>
public static class ObligationScheduleCalculator
{
    public static IReadOnlyList<ObligationEvent> UpcomingEvents(
        IEnumerable<Obligation> obligations,
        DateOnly asOfDate,
        int lookAheadDays = 30)
    {
        ArgumentNullException.ThrowIfNull(obligations);

        var events = new List<ObligationEvent>();

        foreach (var obligation in obligations.Where(static item => item.IsActive))
        {
            for (var occurrence = obligation.StartDate;
                 occurrence <= asOfDate.AddDays(lookAheadDays);
                 occurrence = AdvanceOccurrence(occurrence, obligation.Cycle))
            {
                var isPaid = obligation.PaidOccurrences
                    .Any(paid => SamePeriod(paid, occurrence, obligation.Cycle));

                events.Add(new ObligationEvent(
                    ObligationId: obligation.ObligationId,
                    Title: obligation.Title,
                    Category: obligation.Category,
                    AmountMinor: obligation.AmountMinor,
                    DueDate: occurrence,
                    Cycle: obligation.Cycle,
                    IsPaid: isPaid));
            }
        }

        return events
            .OrderBy(static item => item.DueDate)
            .ThenBy(static item => item.ObligationId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsValid(Obligation obligation)
    {
        if (obligation is null) return false;
        if (string.IsNullOrWhiteSpace(obligation.Title)) return false;
        if (obligation.AmountMinor <= 0) return false;
        if (obligation.Cycle is not ("daily" or "weekly" or "monthly" or "yearly"))
        {
            return false;
        }

        return true;
    }

    private static DateOnly AdvanceOccurrence(DateOnly current, string cycle) =>
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

    private static bool SamePeriod(DateOnly paid, DateOnly due, string cycle) =>
        cycle switch
        {
            "daily" => paid == due,
            "weekly" => paid.AddDays(-(int)paid.DayOfWeek) == due.AddDays(-(int)due.DayOfWeek),
            "monthly" => paid.Year == due.Year && paid.Month == due.Month,
            "yearly" => paid.Year == due.Year,
            _ => paid == due
        };
}

public sealed record Obligation(
    string ObligationId,
    string UserId,
    string Title,
    string Category,
    long AmountMinor,
    DateOnly StartDate,
    string Cycle,
    bool IsActive,
    IReadOnlyList<DateOnly> PaidOccurrences,
    DateTimeOffset? CreatedAtUtc = null,
    DateTimeOffset? UpdatedAtUtc = null,
    int Version = 1,
    string DeviceId = "")
{
    public static Obligation Empty(string id, string userId) => new(
        id,
        userId,
        "",
        "",
        0,
        DateOnly.MinValue,
        "monthly",
        true,
        Array.Empty<DateOnly>());
}

public sealed record ObligationEvent(
    string ObligationId,
    string Title,
    string Category,
    long AmountMinor,
    DateOnly DueDate,
    string Cycle,
    bool IsPaid);
