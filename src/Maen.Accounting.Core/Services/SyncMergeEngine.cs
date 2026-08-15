using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

public static class SyncMergeEngine
{
    public static SyncPlan BuildPlan(
        string userId,
        IEnumerable<ProfitEntry> localEntries,
        IEnumerable<ProfitEntry> remoteEntries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(localEntries);
        ArgumentNullException.ThrowIfNull(remoteEntries);

        var local = IndexAndValidate(userId, localEntries);
        var remote = IndexAndValidate(userId, remoteEntries);
        var ids = local.Keys.Union(remote.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var merged = new List<ProfitEntry>();
        var toPush = new List<ProfitEntry>();
        var localWins = 0;
        var remoteWins = 0;

        foreach (var id in ids)
        {
            var hasLocal = local.TryGetValue(id, out var localEntry);
            var hasRemote = remote.TryGetValue(id, out var remoteEntry);

            if (hasLocal && !hasRemote)
            {
                merged.Add(localEntry!);
                toPush.Add(localEntry!);
                localWins++;
                continue;
            }

            if (!hasLocal && hasRemote)
            {
                merged.Add(remoteEntry!);
                remoteWins++;
                continue;
            }

            var comparison = Compare(localEntry!, remoteEntry!);
            if (comparison >= 0)
            {
                merged.Add(localEntry!);
                if (!Equivalent(localEntry!, remoteEntry!))
                {
                    toPush.Add(localEntry!);
                }
                localWins++;
            }
            else
            {
                merged.Add(remoteEntry!);
                remoteWins++;
            }
        }

        return new SyncPlan(merged, toPush.GroupBy(static entry => entry.EntryId, StringComparer.Ordinal).Select(static group => group.Last()).ToArray(), localWins, remoteWins);
    }

    private sealed class ProfitEntryRecencyComparer : IComparer<ProfitEntry>
    {
        public static ProfitEntryRecencyComparer Instance { get; } = new();
        public int Compare(ProfitEntry? x, ProfitEntry? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            return SyncMergeEngine.Compare(x, y);
        }
    }

    private static Dictionary<string, ProfitEntry> IndexAndValidate(
        string userId,
        IEnumerable<ProfitEntry> entries)
    {
        var result = new Dictionary<string, ProfitEntry>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            UserIsolation.EnsureOwner(userId, entry.UserId);
            if (string.IsNullOrWhiteSpace(entry.EntryId))
            {
                throw new InvalidOperationException("A synchronized record has no identifier.");
            }

            if (!result.TryGetValue(entry.EntryId, out var current) || Compare(entry, current) > 0)
            {
                result[entry.EntryId] = entry;
            }
        }

        return result;
    }

    private static int Compare(ProfitEntry left, ProfitEntry right)
    {
        var updated = left.UpdatedAtUtc.CompareTo(right.UpdatedAtUtc);
        if (updated != 0) return updated;

        var version = left.Version.CompareTo(right.Version);
        if (version != 0) return version;

        return string.Compare(left.DeviceId, right.DeviceId, StringComparison.Ordinal);
    }

    private static bool Equivalent(ProfitEntry left, ProfitEntry right) =>
        left == right;
}
