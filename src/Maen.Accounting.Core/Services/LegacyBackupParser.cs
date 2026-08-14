using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

public sealed record BackupImportResult(
    int Version,
    string Email,
    IReadOnlyList<ProfitEntry> Entries);

public static class LegacyBackupParser
{
    public static BackupImportResult Parse(
        string json,
        string expectedUserId,
        string expectedEmail,
        string deviceId,
        DateTimeOffset nowUtc,
        string expectedScope = "business")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        if (expectedScope is not ("personal" or "business"))
        {
            throw new ArgumentException("The account scope is invalid.", nameof(expectedScope));
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var version = root.TryGetProperty("version", out var versionElement)
            ? versionElement.GetInt32()
            : 1;
        if (version is not (1 or 2 or 3 or 4))
        {
            throw new InvalidDataException($"Unsupported backup version: {version}.");
        }

        var backupScope = root.TryGetProperty("accountScope", out var scopeElement)
            ? scopeElement.GetString() ?? string.Empty
            : "business";
        if (!string.Equals(backupScope, expectedScope, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The backup belongs to a different account type.");
        }

        var backupEmail = root.TryGetProperty("backupEmail", out var emailElement)
            ? emailElement.GetString() ?? string.Empty
            : root.TryGetProperty("email", out var alternateEmail)
                ? alternateEmail.GetString() ?? string.Empty
                : string.Empty;

        if (!string.Equals(
                UserIsolation.NormalizeEmail(backupEmail),
                UserIsolation.NormalizeEmail(expectedEmail),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The backup belongs to a different email account.");
        }

        if (!root.TryGetProperty("entries", out var entriesElement) ||
            entriesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The backup contains no entries array.");
        }

        var entries = new List<ProfitEntry>();
        foreach (var item in entriesElement.EnumerateArray())
        {
            entries.Add(version >= 3
                ? ParseVersionThree(item, expectedUserId, deviceId, nowUtc)
                : ParseLegacy(item, expectedUserId, deviceId, nowUtc));
        }

        return new BackupImportResult(version, backupEmail, entries);
    }

    private static ProfitEntry ParseLegacy(
        JsonElement item,
        string userId,
        string deviceId,
        DateTimeOffset nowUtc)
    {
        var dateText = item.GetProperty("entry_date").GetString()
            ?? throw new InvalidDataException("A legacy entry has no date.");
        var date = DateOnly.ParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var entryId = $"legacy_{StableId(userId, dateText)}";

        return new ProfitEntry(
            entryId,
            userId,
            date,
            Money.FromDecimal(item.GetProperty("sales").GetDecimal()),
            Money.FromDecimal(item.GetProperty("cost").GetDecimal()),
            Money.FromDecimal(item.GetProperty("expenses").GetDecimal()),
            item.TryGetProperty("notes", out var notes) ? notes.GetString() ?? string.Empty : string.Empty,
            false,
            nowUtc,
            nowUtc,
            1,
            deviceId);
    }

    private static ProfitEntry ParseVersionThree(
        JsonElement item,
        string userId,
        string deviceId,
        DateTimeOffset nowUtc)
    {
        var entryUserId = item.GetProperty("userId").GetString() ?? string.Empty;
        UserIsolation.EnsureOwner(userId, entryUserId);
        var entryId = item.GetProperty("entryId").GetString();
        if (string.IsNullOrWhiteSpace(entryId))
        {
            throw new InvalidDataException("A version three entry has no entryId.");
        }

        var date = DateOnly.ParseExact(
            item.GetProperty("entryDate").GetString() ?? string.Empty,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture);

        return new ProfitEntry(
            entryId,
            userId,
            date,
            item.GetProperty("salesMinor").GetInt64(),
            item.GetProperty("costMinor").GetInt64(),
            item.GetProperty("expensesMinor").GetInt64(),
            item.TryGetProperty("notes", out var notes) ? notes.GetString() ?? string.Empty : string.Empty,
            item.TryGetProperty("isDeleted", out var deleted) && deleted.GetBoolean(),
            ParseDate(item, "createdAtUtc", nowUtc),
            ParseDate(item, "updatedAtUtc", nowUtc),
            item.TryGetProperty("version", out var version) ? version.GetInt32() : 1,
            item.TryGetProperty("deviceId", out var sourceDevice)
                ? sourceDevice.GetString() ?? deviceId
                : deviceId);
    }

    private static DateTimeOffset ParseDate(JsonElement item, string property, DateTimeOffset fallback) =>
        item.TryGetProperty(property, out var element) &&
        DateTimeOffset.TryParse(element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : fallback;

    private static string StableId(string userId, string dateText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}|{dateText}"));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..24];
    }
}
