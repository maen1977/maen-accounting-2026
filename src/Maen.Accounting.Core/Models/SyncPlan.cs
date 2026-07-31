namespace Maen.Accounting.Core.Models;

public sealed record SyncPlan(
    IReadOnlyList<ProfitEntry> MergedEntries,
    IReadOnlyList<ProfitEntry> EntriesToPush,
    int LocalWins,
    int RemoteWins);
