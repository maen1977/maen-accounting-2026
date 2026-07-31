using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.Data;

public sealed class ProfitEntryRepository
{
    private readonly UserDatabaseFactory _databaseFactory;

    public ProfitEntryRepository(UserDatabaseFactory databaseFactory) =>
        _databaseFactory = databaseFactory;

    public async Task<IReadOnlyList<ProfitEntry>> GetVisibleAsync(string userId)
    {
        var database = await _databaseFactory.GetAsync(userId);
        var rows = await database.Table<ProfitEntryRow>()
            .Where(row => row.UserId == userId && !row.IsDeleted)
            .OrderByDescending(row => row.EntryDate)
            .ToListAsync();
        return rows.Select(static row => row.ToModel()).ToArray();
    }

    public async Task<IReadOnlyList<ProfitEntry>> GetAllForSyncAsync(string userId)
    {
        var database = await _databaseFactory.GetAsync(userId);
        var rows = await database.Table<ProfitEntryRow>()
            .Where(row => row.UserId == userId)
            .ToListAsync();
        return rows.Select(static row => row.ToModel()).ToArray();
    }

    public async Task UpsertAsync(string userId, ProfitEntry entry)
    {
        UserIsolation.EnsureOwner(userId, entry.UserId);
        var database = await _databaseFactory.GetAsync(userId);
        await database.RunInTransactionAsync(connection =>
        {
            var sameDate = connection.Table<ProfitEntryRow>()
                .FirstOrDefault(row =>
                    row.UserId == userId &&
                    row.EntryDate == entry.EntryDate.ToString("yyyy-MM-dd") &&
                    !row.IsDeleted &&
                    row.EntryId != entry.EntryId);

            if (sameDate is not null)
            {
                sameDate.IsDeleted = true;
                sameDate.UpdatedAtUtcTicks = entry.UpdatedAtUtc.UtcDateTime.Ticks;
                sameDate.Version = checked(sameDate.Version + 1);
                sameDate.DeviceId = entry.DeviceId;
                connection.Update(sameDate);
            }

            connection.InsertOrReplace(ProfitEntryRow.FromModel(entry));
        });
    }

    public async Task UpsertManyAsync(string userId, IEnumerable<ProfitEntry> entries)
    {
        var materialized = entries.ToArray();
        foreach (var entry in materialized)
        {
            UserIsolation.EnsureOwner(userId, entry.UserId);
        }

        var database = await _databaseFactory.GetAsync(userId);
        await database.RunInTransactionAsync(connection =>
        {
            foreach (var entry in materialized)
            {
                connection.InsertOrReplace(ProfitEntryRow.FromModel(entry));
            }
        });
    }

    public async Task<ProfitEntry?> FindByDateAsync(string userId, DateOnly date)
    {
        var database = await _databaseFactory.GetAsync(userId);
        var dateText = date.ToString("yyyy-MM-dd");
        var row = await database.Table<ProfitEntryRow>()
            .FirstOrDefaultAsync(item => item.UserId == userId && item.EntryDate == dateText && !item.IsDeleted);
        return row?.ToModel();
    }
}
