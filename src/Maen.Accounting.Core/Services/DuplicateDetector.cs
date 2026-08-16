using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Flags a newly-created entry as a possible duplicate when another recent active
/// entry matches on amount, date, category and counterparty within a sliding window,
/// so the UI can warn the user without ever blocking the save.
/// </summary>
public static class DuplicateDetector
{
    public const int DefaultWindowDays = 3;

    public static bool IsLikelyDuplicate(
        ProfitEntry candidate,
        IEnumerable<ProfitEntry> existing,
        int windowDays = DefaultWindowDays)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(existing);

        var candidateEffective = candidate.EffectiveAmountMinor;
        if (candidateEffective <= 0)
        {
            return false;
        }

        foreach (var entry in existing)
        {
            if (entry.IsDeleted || string.Equals(entry.EntryId, candidate.EntryId, StringComparison.Ordinal))
            {
                continue;
            }

            if (Math.Abs(entry.EntryDate.DayNumber - candidate.EntryDate.DayNumber) > windowDays)
            {
                continue;
            }

            if (entry.EffectiveAmountMinor != candidateEffective)
            {
                continue;
            }

            var categoryMatches = string.Equals(
                NormalizeText(entry.Category), NormalizeText(candidate.Category), StringComparison.Ordinal);
            var counterpartyMatches = string.Equals(
                NormalizeText(entry.Counterparty), NormalizeText(candidate.Counterparty), StringComparison.Ordinal);

            if (categoryMatches && counterpartyMatches)
            {
                return true;
            }
        }

        return false;
    }

    public static int CountMatches(
        ProfitEntry candidate,
        IEnumerable<ProfitEntry> existing,
        int windowDays = DefaultWindowDays)
    {
        var count = 0;
        foreach (var entry in existing)
        {
            if (MatchesSingle(entry, candidate, windowDays))
            {
                count++;
            }
        }

        return count;
    }

    private static bool MatchesSingle(ProfitEntry entry, ProfitEntry candidate, int windowDays)
    {
        if (entry.IsDeleted || string.Equals(entry.EntryId, candidate.EntryId, StringComparison.Ordinal))
        {
            return false;
        }

        if (Math.Abs(entry.EntryDate.DayNumber - candidate.EntryDate.DayNumber) > windowDays)
        {
            return false;
        }

        if (entry.EffectiveAmountMinor != candidate.EffectiveAmountMinor)
        {
            return false;
        }

        return string.Equals(NormalizeText(entry.Category), NormalizeText(candidate.Category), StringComparison.Ordinal)
            && string.Equals(NormalizeText(entry.Counterparty), NormalizeText(candidate.Counterparty), StringComparison.Ordinal);
    }

    private static string NormalizeText(string? text) =>
        (text ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
}
