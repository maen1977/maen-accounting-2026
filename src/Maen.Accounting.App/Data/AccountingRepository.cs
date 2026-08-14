using Maen.Accounting.App.Services;
using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.Data;

public sealed class AccountingRepository
{
    private readonly UserDatabaseFactory _databaseFactory;
    private readonly ProfitEntryRepository _profitEntryRepository;
    private readonly AppPreferencesService _preferences;

    public AccountingRepository(
        UserDatabaseFactory databaseFactory,
        ProfitEntryRepository profitEntryRepository,
        AppPreferencesService preferences)
    {
        _databaseFactory = databaseFactory;
        _profitEntryRepository = profitEntryRepository;
        _preferences = preferences;
    }

    public async Task<IReadOnlyList<Account>> GetAccountsAsync(string userId)
    {
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await EnsureDefaultAccountsAsync(userId, database);
        var rows = await database.Table<AccountRow>()
            .Where(row => row.UserId == userId)
            .ToListAsync();
        return rows
            .Select(static row => row.ToModel())
            .OrderBy(static account => account.SortOrder)
            .ThenBy(static account => account.Code, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task UpsertAccountAsync(string userId, Account account)
    {
        UserIsolation.EnsureOwner(userId, account.UserId);
        if (string.IsNullOrWhiteSpace(account.Code) || string.IsNullOrWhiteSpace(account.Name))
        {
            throw new ArgumentException(UiText.Get("T314"));
        }

        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await database.InsertOrReplaceAsync(AccountRow.FromModel(account));
    }

    public async Task<IReadOnlyList<JournalEntry>> GetJournalEntriesAsync(
        string userId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null)
    {
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        var entryRows = await database.Table<JournalEntryRow>()
            .Where(row => row.UserId == userId)
            .ToListAsync();
        var lineRows = await database.Table<JournalLineRow>()
            .Where(row => row.UserId == userId)
            .ToListAsync();
        var linesByEntry = lineRows
            .GroupBy(static row => row.EntryId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<JournalLine>)group.Select(static row => row.ToModel()).ToArray(),
                StringComparer.Ordinal);

        return entryRows
            .Select(row =>
            {
                linesByEntry.TryGetValue(row.EntryId, out var lines);
                return row.ToModel(lines ?? Array.Empty<JournalLine>());
            })
            .Where(entry => fromDate is null || entry.EntryDate >= fromDate.Value)
            .Where(entry => toDate is null || entry.EntryDate <= toDate.Value)
            .OrderByDescending(static entry => entry.EntryDate)
            .ThenByDescending(static entry => entry.EntryNumber, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task UpsertJournalEntryAsync(string userId, JournalEntry entry)
    {
        UserIsolation.EnsureOwner(userId, entry.UserId);
        var accounts = await GetAccountsAsync(userId);
        var accountIds = accounts.Select(static account => account.AccountId).ToHashSet(StringComparer.Ordinal);
        if (entry.Status == JournalEntryStatus.Posted)
        {
            JournalEntryValidator.EnsurePostable(entry, accountIds);
        }

        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await database.RunInTransactionAsync(connection =>
        {
            connection.InsertOrReplace(JournalEntryRow.FromModel(entry));
            connection.Execute("DELETE FROM journal_lines WHERE EntryId = ? AND UserId = ?", entry.EntryId, userId);
            foreach (var line in entry.Lines)
            {
                connection.InsertOrReplace(JournalLineRow.FromModel(entry.EntryId, userId, line));
            }
        });
    }

    public async Task<int> MigrateLegacyProfitEntriesAsync(string userId, string deviceId = "")
    {
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await EnsureDefaultAccountsAsync(userId, database);
        var legacyEntries = await _profitEntryRepository.GetAllForSyncAsync(userId);
        var candidates = legacyEntries
            .Select(entry => LegacyProfitJournalMapper.Map(entry, deviceId))
            .Where(static entry => entry is not null)
            .Select(static entry => entry!)
            .ToArray();
        if (candidates.Length == 0)
        {
            return 0;
        }

        var existingIds = (await database.Table<JournalEntryRow>()
                .Where(row => row.UserId == userId)
                .ToListAsync())
            .Select(static row => row.EntryId)
            .ToHashSet(StringComparer.Ordinal);
        var newEntries = candidates.Where(entry => !existingIds.Contains(entry.EntryId)).ToArray();
        if (newEntries.Length == 0)
        {
            return 0;
        }

        await database.RunInTransactionAsync(connection =>
        {
            foreach (var entry in newEntries)
            {
                JournalEntryValidator.EnsurePostable(entry);
                connection.InsertOrReplace(JournalEntryRow.FromModel(entry));
                foreach (var line in entry.Lines)
                {
                    connection.InsertOrReplace(JournalLineRow.FromModel(entry.EntryId, userId, line));
                }
            }
        });
        return newEntries.Length;
    }

    public async Task<TrialBalance> GetTrialBalanceAsync(
        string userId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null)
    {
        var accounts = await GetAccountsAsync(userId);
        var entries = await GetJournalEntriesAsync(userId, fromDate, toDate);
        return TrialBalanceCalculator.Build(accounts, entries);
    }

    private static async Task EnsureDefaultAccountsAsync(
        string userId,
        SQLite.SQLiteAsyncConnection database)
    {
        var defaults = DefaultChartOfAccounts.Create(userId);
        var existingCodes = (await database.Table<AccountRow>()
                .Where(row => row.UserId == userId)
                .ToListAsync())
            .Select(static row => row.Code)
            .ToHashSet(StringComparer.Ordinal);
        var missing = defaults
            .Where(account => !existingCodes.Contains(account.Code))
            .Select(AccountRow.FromModel)
            .ToArray();
        if (missing.Length == 0)
        {
            return;
        }

        await database.RunInTransactionAsync(connection =>
        {
            foreach (var row in missing)
            {
                connection.InsertOrReplace(row);
            }
        });
    }
}
