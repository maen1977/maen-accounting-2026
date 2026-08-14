using System.Text.Json;
using Maen.Accounting.App.Data;
using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.Services;

public sealed record BackupInfo(bool Exists, string? FilePath, DateTimeOffset? UpdatedAtUtc, int EntriesCount);

public sealed class BackupService
{
    private readonly ProfitEntryRepository _repository;
    private readonly DeviceIdentityService _deviceIdentity;
    private readonly AppPreferencesService _preferences;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public BackupService(
        ProfitEntryRepository repository,
        DeviceIdentityService deviceIdentity,
        AppPreferencesService preferences)
    {
        _repository = repository;
        _deviceIdentity = deviceIdentity;
        _preferences = preferences;
    }

    public async Task<BackupInfo> WriteAsync(AuthSession session)
    {
        var entries = await _repository.GetAllForSyncAsync(session.UserId);
        var envelope = new
        {
            version = 4,
            accountScope = _preferences.StorageScope,
            userId = session.UserId,
            backupEmail = session.Email,
            updatedAtUtc = DateTimeOffset.UtcNow,
            entriesCount = entries.Count,
            entries = entries.Select(entry => new
            {
                entry.EntryId,
                entry.UserId,
                entryDate = entry.EntryDate.ToString("yyyy-MM-dd"),
                entry.SalesMinor,
                entry.CostMinor,
                entry.ExpensesMinor,
                entry.Notes,
                entry.IsDeleted,
                entry.CreatedAtUtc,
                entry.UpdatedAtUtc,
                entry.Version,
                entry.DeviceId
            })
        };

        var path = GetBackupPath(session.UserId, _preferences.StorageScope);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var json = JsonSerializer.Serialize(envelope, JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, json);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return new BackupInfo(true, path, File.GetLastWriteTimeUtc(path), entries.Count);
    }

    public Task<BackupInfo> ReadInfoAsync(string userId)
    {
        var path = GetBackupPath(userId, _preferences.StorageScope);
        if (!File.Exists(path))
        {
            return Task.FromResult(new BackupInfo(false, null, null, 0));
        }

        return ReadInfoCoreAsync(path);
    }

    public async Task<string> CreateExportCopyAsync(AuthSession session)
    {
        var info = await WriteAsync(session);
        var safeEmail = session.Email.Replace('@', '_').Replace('.', '_');
        var exportPath = Path.Combine(FileSystem.CacheDirectory, $"maen_backup_{_preferences.StorageScope}_{safeEmail}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        File.Copy(info.FilePath!, exportPath, overwrite: true);
        return exportPath;
    }

    public async Task<int> ImportAsync(AuthSession session, string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var parsed = LegacyBackupParser.Parse(
            json,
            session.UserId,
            session.Email,
            _deviceIdentity.GetOrCreate(),
            DateTimeOffset.UtcNow,
            _preferences.StorageScope);
        await _repository.UpsertManyAsync(session.UserId, parsed.Entries);
        await WriteAsync(session);
        return parsed.Entries.Count;
    }

    private static string GetBackupPath(string userId, string scope) => Path.Combine(
        FileSystem.AppDataDirectory,
        "Backups",
        UserIsolation.SafeHash(userId),
        scope == "business" ? "maen_backup.json" : $"maen_backup_{scope}.json");

    private static async Task<BackupInfo> ReadInfoCoreAsync(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var root = document.RootElement;
            if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return new BackupInfo(false, path, File.GetLastWriteTimeUtc(path), 0);
            }

            var count = root.TryGetProperty("entriesCount", out var element) && element.TryGetInt32(out var declaredCount)
                ? declaredCount
                : entries.GetArrayLength();
            return new BackupInfo(true, path, File.GetLastWriteTimeUtc(path), Math.Max(0, count));
        }
        catch
        {
            return new BackupInfo(false, path, File.GetLastWriteTimeUtc(path), 0);
        }
    }
}
