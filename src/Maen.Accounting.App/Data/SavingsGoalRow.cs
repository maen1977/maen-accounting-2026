using Maen.Accounting.Core.Services;
using SQLite;

namespace Maen.Accounting.App.Data;

[Table("savings_goals")]
public sealed class SavingsGoalRow
{
    [PrimaryKey]
    public string GoalId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public long TargetMinor { get; set; }
    public long SavedMinor { get; set; }
    public long StartDateTicks { get; set; }
    public long DeadlineTicks { get; set; }
    public long UpdatedAtUtcTicks { get; set; }
    public int Version { get; set; } = 1;
    public string DeviceId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }

    public SavingsGoal ToModel() => new(
        GoalId,
        UserId,
        InputSanitizer.SanitizeName(Title),
        Category,
        TargetMinor,
        SavedMinor,
        DateFromTicks(StartDateTicks),
        DateFromTicks(DeadlineTicks),
        IsDeleted);

    public static SavingsGoalRow FromModel(SavingsGoal goal)
    {
        var now = DateTimeOffset.UtcNow;
        return new SavingsGoalRow
        {
            GoalId = goal.GoalId,
            UserId = goal.UserId,
            Title = InputSanitizer.SanitizeName(goal.Title),
            Category = goal.Category,
            TargetMinor = goal.TargetMinor,
            SavedMinor = goal.AlreadySavedMinor,
            StartDateTicks = goal.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).Ticks,
            DeadlineTicks = goal.Deadline.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).Ticks,
            UpdatedAtUtcTicks = now.UtcTicks,
            Version = 1,
            DeviceId = string.Empty,
            IsDeleted = goal.IsClosed
        };
    }

    private static DateOnly DateFromTicks(long ticks) =>
        ticks <= 0 ? DateOnly.MinValue : new DateOnly(DateTimeOffset.FromUnixTimeMilliseconds(ticks / TimeSpan.TicksPerMillisecond).DateTime.Year, DateTimeOffset.FromUnixTimeMilliseconds(ticks / TimeSpan.TicksPerMillisecond).DateTime.Month, DateTimeOffset.FromUnixTimeMilliseconds(ticks / TimeSpan.TicksPerMillisecond).DateTime.Day);
}
