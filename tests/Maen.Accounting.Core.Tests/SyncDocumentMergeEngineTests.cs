using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

public sealed class SyncDocumentMergeEngineTests
{
    [Fact]
    public void NewLocalDocument_IsQueuedForUploadAndCountedAsLocalWin()
    {
        var local = Document("c-1", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), 1, "device-a", "local");

        var plan = SyncDocumentMergeEngine.BuildPlan(
            "user-1",
            new[] { local },
            Array.Empty<TestDocument>(),
            static item => item.Id,
            static item => item.UserId,
            static item => item.UpdatedAtUtc,
            static item => item.Version,
            static item => item.DeviceId);

        Assert.Single(plan.MergedItems);
        Assert.Single(plan.ItemsToPush);
        Assert.Equal(1, plan.LocalWins);
        Assert.Equal(0, plan.RemoteWins);
    }

    [Fact]
    public void NewRemoteDocument_IsMergedWithoutUploadAndCountedAsRemoteWin()
    {
        var remote = Document("c-1", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), 1, "device-b", "remote");

        var plan = SyncDocumentMergeEngine.BuildPlan(
            "user-1",
            Array.Empty<TestDocument>(),
            new[] { remote },
            static item => item.Id,
            static item => item.UserId,
            static item => item.UpdatedAtUtc,
            static item => item.Version,
            static item => item.DeviceId);

        Assert.Single(plan.MergedItems);
        Assert.Empty(plan.ItemsToPush);
        Assert.Equal("remote", plan.MergedItems[0].Payload);
        Assert.Equal(0, plan.LocalWins);
        Assert.Equal(1, plan.RemoteWins);
    }

    [Fact]
    public void NewerLocalDocument_WinsAndIsUploaded()
    {
        var local = Document("c-1", DateTimeOffset.Parse("2026-01-03T00:00:00Z"), 2, "device-a", "local");
        var remote = Document("c-1", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), 9, "device-b", "remote");

        var plan = SyncDocumentMergeEngine.BuildPlan(
            "user-1",
            new[] { local },
            new[] { remote },
            static item => item.Id,
            static item => item.UserId,
            static item => item.UpdatedAtUtc,
            static item => item.Version,
            static item => item.DeviceId);

        Assert.Equal("local", plan.MergedItems[0].Payload);
        Assert.Single(plan.ItemsToPush);
        Assert.Equal(1, plan.LocalWins);
        Assert.Equal(0, plan.RemoteWins);
    }

    [Fact]
    public void NewerRemoteDocument_WinsWithoutUpload()
    {
        var local = Document("c-1", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), 9, "device-a", "local");
        var remote = Document("c-1", DateTimeOffset.Parse("2026-01-03T00:00:00Z"), 1, "device-b", "remote");

        var plan = SyncDocumentMergeEngine.BuildPlan(
            "user-1",
            new[] { local },
            new[] { remote },
            static item => item.Id,
            static item => item.UserId,
            static item => item.UpdatedAtUtc,
            static item => item.Version,
            static item => item.DeviceId);

        Assert.Equal("remote", plan.MergedItems[0].Payload);
        Assert.Empty(plan.ItemsToPush);
        Assert.Equal(0, plan.LocalWins);
        Assert.Equal(1, plan.RemoteWins);
    }

    [Fact]
    public void EqualTimestampAndVersion_UsesDeviceIdAsDeterministicTieBreaker()
    {
        var local = Document("c-1", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), 1, "device-z", "local");
        var remote = Document("c-1", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), 1, "device-a", "remote");

        var plan = SyncDocumentMergeEngine.BuildPlan(
            "user-1",
            new[] { local },
            new[] { remote },
            static item => item.Id,
            static item => item.UserId,
            static item => item.UpdatedAtUtc,
            static item => item.Version,
            static item => item.DeviceId);

        Assert.Equal("local", plan.MergedItems[0].Payload);
        Assert.Single(plan.ItemsToPush);
    }

    [Fact]
    public void ForeignOwner_IsRejected()
    {
        var foreign = Document("c-1", DateTimeOffset.UtcNow, 1, "device-a", "foreign") with { UserId = "other-user" };

        Assert.Throws<InvalidOperationException>(() => SyncDocumentMergeEngine.BuildPlan(
            "user-1",
            new[] { foreign },
            Array.Empty<TestDocument>(),
            static item => item.Id,
            static item => item.UserId,
            static item => item.UpdatedAtUtc,
            static item => item.Version,
            static item => item.DeviceId));
    }

    private static TestDocument Document(
        string id,
        DateTimeOffset updatedAtUtc,
        int version,
        string deviceId,
        string payload) =>
        new(id, "user-1", updatedAtUtc, version, deviceId, payload);

    private sealed record TestDocument(
        string Id,
        string UserId,
        DateTimeOffset UpdatedAtUtc,
        int Version,
        string DeviceId,
        string Payload);
}
