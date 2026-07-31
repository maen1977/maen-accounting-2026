using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

public sealed class SyncMergeEngineTests
{
    [Fact]
    public void Newer_remote_record_wins_without_being_reuploaded()
    {
        var older = Entry("id", "user-a", 1, new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero), "a");
        var newer = Entry("id", "user-a", 2, new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero), "b");

        var plan = SyncMergeEngine.BuildPlan("user-a", new[] { older }, new[] { newer });

        Assert.Equal(newer, Assert.Single(plan.MergedEntries));
        Assert.Empty(plan.EntriesToPush);
        Assert.Equal(1, plan.RemoteWins);
    }

    [Fact]
    public void Local_only_record_is_uploaded()
    {
        var local = Entry("id", "user-a", 1, DateTimeOffset.UtcNow, "a");
        var plan = SyncMergeEngine.BuildPlan("user-a", new[] { local }, Array.Empty<ProfitEntry>());
        Assert.Equal(local, Assert.Single(plan.EntriesToPush));
    }

    [Fact]
    public void Cross_account_record_is_rejected()
    {
        var wrong = Entry("id", "user-b", 1, DateTimeOffset.UtcNow, "a");
        Assert.Throws<InvalidOperationException>(() =>
            SyncMergeEngine.BuildPlan("user-a", new[] { wrong }, Array.Empty<ProfitEntry>()));
    }


    [Fact]
    public void Two_active_records_for_same_date_are_resolved_deterministically()
    {
        var older = Entry("old", "user-a", 1, new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero), "a");
        var newer = Entry("new", "user-a", 1, new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero), "b");

        var plan = SyncMergeEngine.BuildPlan("user-a", new[] { older }, new[] { newer });

        Assert.Single(plan.MergedEntries.Where(entry => !entry.IsDeleted));
        Assert.Equal("new", plan.MergedEntries.Single(entry => !entry.IsDeleted).EntryId);
        Assert.Contains(plan.EntriesToPush, entry => entry.EntryId == "old" && entry.IsDeleted);
    }

    private static ProfitEntry Entry(string id, string user, int version, DateTimeOffset updated, string device) => new(
        id, user, new DateOnly(2026, 7, 1), 100, 20, 10, string.Empty, false,
        updated.AddHours(-1), updated, version, device);
}
