using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Summarizes the audit trail of a user's personal ledger: record counts,
/// verified-integrity counts, latest edit and latest delete timestamps, and an
/// overall health verdict that combines hash verification with deletion activity.
/// </summary>
public static class AuditTrailCalculator
{
    public static AuditSummary Summarize(
        IReadOnlyList<ProfitEntry> entries,
        int verifiedHashCount,
        int totalRecords,
        DateTimeOffset? lastSyncUtc)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var nonDeleted = entries.Where(static entry => !entry.IsDeleted).ToArray();
        var latestEdit = entries
            .OrderByDescending(static entry => entry.UpdatedAtUtc)
            .Select(static entry => entry.UpdatedAtUtc)
            .FirstOrDefault();

        var deleted = entries.Where(static entry => entry.IsDeleted).ToArray();
        var latestDelete = deleted
            .OrderByDescending(static entry => entry.UpdatedAtUtc)
            .Select(static entry => entry.UpdatedAtUtc)
            .FirstOrDefault();

        var hashComputed = entries.Count(static entry => !string.IsNullOrWhiteSpace(entry.IntegrityHash));
        var hashVerifiedRatio = totalRecords <= 0 ? 0.0 : (double)verifiedHashCount / totalRecords;
        var hashCoverage = totalRecords <= 0 ? 0.0 : (double)hashComputed / totalRecords;

        var verdict = (hashVerifiedRatio >= 0.9 && hashCoverage >= 0.9)
            ? "healthy"
            : (hashCoverage >= 0.5)
                ? "partial"
                : "pending";

        return new AuditSummary(
            ActiveRecordCount: nonDeleted.Length,
            DeletedRecordCount: deleted.Length,
            TotalRecordCount: totalRecords,
            VerifiedHashCount: verifiedHashCount,
            HashCoveragePercent: Math.Round(hashCoverage * 100, 1),
            VerifiedPercent: Math.Round(hashVerifiedRatio * 100, 1),
            LatestEditUtc: latestEdit == default ? null : latestEdit,
            LatestDeleteUtc: latestDelete == default ? null : latestDelete,
            LastSyncUtc: lastSyncUtc,
            Verdict: verdict);
    }
}

public sealed record AuditSummary(
    int ActiveRecordCount,
    int DeletedRecordCount,
    int TotalRecordCount,
    int VerifiedHashCount,
    double HashCoveragePercent,
    double VerifiedPercent,
    DateTimeOffset? LatestEditUtc,
    DateTimeOffset? LatestDeleteUtc,
    DateTimeOffset? LastSyncUtc,
    string Verdict)
{
    public bool IsHealthy => Verdict == "healthy";
}
