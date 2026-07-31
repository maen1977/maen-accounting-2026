using Maen.Accounting.Core.Services;
using SQLite;

namespace Maen.Accounting.App.Data;

public sealed class UserDatabaseFactory
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _activeUserId;
    private SQLiteAsyncConnection? _connection;

    public async Task<SQLiteAsyncConnection> GetAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await _gate.WaitAsync();
        try
        {
            if (_connection is not null && string.Equals(_activeUserId, userId, StringComparison.Ordinal))
            {
                return _connection;
            }

            var path = Path.Combine(FileSystem.AppDataDirectory, UserIsolation.DatabaseFileName(userId));
            var flags = SQLiteOpenFlags.ReadWrite |
                        SQLiteOpenFlags.Create |
                        SQLiteOpenFlags.SharedCache |
                        SQLiteOpenFlags.FullMutex;
            var connection = new SQLiteAsyncConnection(path, flags);
            await connection.CreateTableAsync<ProfitEntryRow>();
            await connection.ExecuteAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS ux_profit_entries_user_date " +
                "ON profit_entries(UserId, EntryDate) WHERE IsDeleted = 0;");

            _activeUserId = userId;
            _connection = connection;
            return connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void ClearActiveConnection()
    {
        _activeUserId = null;
        _connection = null;
    }
}
