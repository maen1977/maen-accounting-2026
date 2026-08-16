namespace Maen.Accounting.Core.Services;

public sealed record SyncDocumentPlan<T>(
    IReadOnlyList<T> MergedItems,
    IReadOnlyList<T> ItemsToPush,
    int LocalWins,
    int RemoteWins);

public static class SyncDocumentMergeEngine
{
    public static SyncDocumentPlan<T> BuildPlan<T>(
        string userId,
        IEnumerable<T> localItems,
        IEnumerable<T> remoteItems,
        Func<T, string> idSelector,
        Func<T, string> ownerSelector,
        Func<T, DateTimeOffset> updatedAtSelector,
        Func<T, int> versionSelector,
        Func<T, string> deviceSelector,
        IEqualityComparer<T>? equalityComparer = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(localItems);
        ArgumentNullException.ThrowIfNull(remoteItems);
        ArgumentNullException.ThrowIfNull(idSelector);
        ArgumentNullException.ThrowIfNull(ownerSelector);
        ArgumentNullException.ThrowIfNull(updatedAtSelector);
        ArgumentNullException.ThrowIfNull(versionSelector);
        ArgumentNullException.ThrowIfNull(deviceSelector);

        var local = IndexAndValidate(userId, localItems, idSelector, ownerSelector, updatedAtSelector, versionSelector, deviceSelector);
        var remote = IndexAndValidate(userId, remoteItems, idSelector, ownerSelector, updatedAtSelector, versionSelector, deviceSelector);
        var ids = local.Keys.Union(remote.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var merged = new List<T>();
        var toPush = new List<T>();
        var localWins = 0;
        var remoteWins = 0;
        var comparer = equalityComparer ?? EqualityComparer<T>.Default;

        foreach (var id in ids)
        {
            var hasLocal = local.TryGetValue(id, out var localItem);
            var hasRemote = remote.TryGetValue(id, out var remoteItem);

            if (hasLocal && !hasRemote)
            {
                merged.Add(localItem!);
                toPush.Add(localItem!);
                localWins++;
                continue;
            }

            if (!hasLocal && hasRemote)
            {
                merged.Add(remoteItem!);
                remoteWins++;
                continue;
            }

            if (Compare(localItem!, remoteItem!, updatedAtSelector, versionSelector, deviceSelector) >= 0)
            {
                merged.Add(localItem!);
                if (!comparer.Equals(localItem!, remoteItem!))
                {
                    toPush.Add(localItem!);
                }

                localWins++;
            }
            else
            {
                merged.Add(remoteItem!);
                remoteWins++;
            }
        }

        return new SyncDocumentPlan<T>(
            merged,
            toPush.GroupBy(idSelector, StringComparer.Ordinal).Select(static group => group.Last()).ToArray(),
            localWins,
            remoteWins);
    }

    private static Dictionary<string, T> IndexAndValidate<T>(
        string userId,
        IEnumerable<T> items,
        Func<T, string> idSelector,
        Func<T, string> ownerSelector,
        Func<T, DateTimeOffset> updatedAtSelector,
        Func<T, int> versionSelector,
        Func<T, string> deviceSelector)
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            UserIsolation.EnsureOwner(userId, ownerSelector(item));
            var id = idSelector(item);
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException("A synchronized record has no identifier.");
            }

            if (!result.TryGetValue(id, out var current) ||
                Compare(item, current, updatedAtSelector, versionSelector, deviceSelector) > 0)
            {
                result[id] = item;
            }
        }

        return result;
    }

    private static int Compare<T>(
        T left,
        T right,
        Func<T, DateTimeOffset> updatedAtSelector,
        Func<T, int> versionSelector,
        Func<T, string> deviceSelector)
    {
        var updated = updatedAtSelector(left).CompareTo(updatedAtSelector(right));
        if (updated != 0) return updated;

        var version = versionSelector(left).CompareTo(versionSelector(right));
        if (version != 0) return version;

        return string.Compare(deviceSelector(left), deviceSelector(right), StringComparison.Ordinal);
    }
}
