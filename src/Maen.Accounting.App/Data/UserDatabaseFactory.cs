using Maen.Accounting.Core.Services;
using SQLite;

namespace Maen.Accounting.App.Data;

public sealed class UserDatabaseFactory
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _activeDatabaseKey;
    private SQLiteAsyncConnection? _connection;

    public async Task<SQLiteAsyncConnection> GetAsync(string userId, string storageScope = "business")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var normalizedScope = UserIsolation.NormalizeStorageScope(storageScope);
        var databaseKey = $"{userId}:{normalizedScope}";
        await _gate.WaitAsync();
        try
        {
            if (_connection is not null && string.Equals(_activeDatabaseKey, databaseKey, StringComparison.Ordinal))
            {
                return _connection;
            }

            var path = Path.Combine(
                FileSystem.AppDataDirectory,
                UserIsolation.DatabaseFileName(userId, normalizedScope));
            var flags = SQLiteOpenFlags.ReadWrite |
                        SQLiteOpenFlags.Create |
                        SQLiteOpenFlags.SharedCache |
                        SQLiteOpenFlags.FullMutex;
            var connection = new SQLiteAsyncConnection(path, flags);
            await connection.CreateTableAsync<ProfitEntryRow>();
            await connection.CreateTableAsync<AccountRow>();
            await connection.CreateTableAsync<JournalEntryRow>();
            await connection.CreateTableAsync<JournalLineRow>();
            await connection.CreateTableAsync<ContactRow>();
            await connection.CreateTableAsync<InvoiceRow>();
            await connection.CreateTableAsync<InvoiceLineRow>();
            await connection.CreateTableAsync<PaymentRow>();
            await connection.CreateTableAsync<SchemaMigrationRow>();
            await ApplySchemaMigrationsAsync(connection);

            _activeDatabaseKey = databaseKey;
            _connection = connection;
            return connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task ApplySchemaMigrationsAsync(SQLiteAsyncConnection connection)
    {
        var appliedVersions = (await connection.Table<SchemaMigrationRow>().ToListAsync())
            .Select(static row => row.Version)
            .ToHashSet();

        var migrationActions = new Dictionary<int, Func<Task>>
        {
            [1] = () => EnsureProfitEntryColumnsAsync(connection),
            [2] = () => EnsurePaymentColumnsAsync(connection),
            [3] = () => EnsureIndexesAsync(connection),
            [4] = () => EnsureBusinessEntitySyncIndexesAsync(connection)
        };

        foreach (var migration in SchemaMigrationCatalog.All)
        {
            if (appliedVersions.Contains(migration.Version)) continue;
            if (!migrationActions.TryGetValue(migration.Version, out var apply))
            {
                throw new InvalidOperationException($"Missing implementation for schema migration {migration.Version}.");
            }

            await apply();
            await connection.InsertAsync(new SchemaMigrationRow
            {
                Version = migration.Version,
                Name = migration.Name,
                AppliedAtUtcTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks
            });
        }
    }

    private static Task EnsureIndexesAsync(SQLiteAsyncConnection connection) => connection.ExecuteAsync(
        "DROP INDEX IF EXISTS ux_profit_entries_user_date;" +
        "CREATE INDEX IF NOT EXISTS ix_profit_entries_user_date " +
        "ON profit_entries(UserId, EntryDate) WHERE IsDeleted = 0;" +
        "CREATE UNIQUE INDEX IF NOT EXISTS ux_accounts_user_code " +
        "ON accounts(UserId, Code);" +
        "CREATE INDEX IF NOT EXISTS ix_journal_entries_user_date " +
        "ON journal_entries(UserId, EntryDate);" +
        "CREATE UNIQUE INDEX IF NOT EXISTS ux_journal_entries_user_number " +
        "ON journal_entries(UserId, EntryNumber);" +
        "CREATE INDEX IF NOT EXISTS ix_journal_lines_user_account " +
        "ON journal_lines(UserId, AccountId);" +
        "CREATE UNIQUE INDEX IF NOT EXISTS ux_contacts_user_name_type " +
        "ON contacts(UserId, Name, Type);" +
        "CREATE INDEX IF NOT EXISTS ix_invoices_user_date " +
        "ON invoices(UserId, IssueDate);" +
        "CREATE UNIQUE INDEX IF NOT EXISTS ux_invoices_user_number " +
        "ON invoices(UserId, Number);" +
        "CREATE INDEX IF NOT EXISTS ix_invoice_lines_user_invoice " +
        "ON invoice_lines(UserId, InvoiceId);" +
        "CREATE INDEX IF NOT EXISTS ix_payments_user_date " +
        "ON payments(UserId, PaymentDate);");

    private static Task EnsureBusinessEntitySyncIndexesAsync(SQLiteAsyncConnection connection) => connection.ExecuteAsync(
        "CREATE INDEX IF NOT EXISTS ix_contacts_user_updated " +
        "ON contacts(UserId, UpdatedAtUtcTicks);" +
        "CREATE INDEX IF NOT EXISTS ix_invoices_user_updated " +
        "ON invoices(UserId, UpdatedAtUtcTicks);" +
        "CREATE INDEX IF NOT EXISTS ix_payments_user_updated " +
        "ON payments(UserId, UpdatedAtUtcTicks);");

    private static async Task EnsurePaymentColumnsAsync(SQLiteAsyncConnection connection)
    {
        var columns = await connection.QueryAsync<SqliteColumnInfo>("PRAGMA table_info(payments);");
        var existing = columns
            .Select(static column => column.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(nameof(PaymentRow.AccountCode)))
        {
            await connection.ExecuteAsync("ALTER TABLE payments ADD COLUMN AccountCode TEXT NOT NULL DEFAULT '1000';");
        }
    }

    private static async Task EnsureProfitEntryColumnsAsync(SQLiteAsyncConnection connection)
    {
        var columns = await connection.QueryAsync<SqliteColumnInfo>("PRAGMA table_info(profit_entries);");
        var existing = columns
            .Select(static column => column.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var additions = new (string Name, string Definition)[]
        {
            (nameof(ProfitEntryRow.AmountMinor), "INTEGER NOT NULL DEFAULT 0"),
            (nameof(ProfitEntryRow.MovementType), "TEXT NOT NULL DEFAULT 'other'"),
            (nameof(ProfitEntryRow.Category), "TEXT NOT NULL DEFAULT ''"),
            (nameof(ProfitEntryRow.Wallet), "TEXT NOT NULL DEFAULT 'main'"),
            (nameof(ProfitEntryRow.Counterparty), "TEXT NOT NULL DEFAULT ''")
        };

        foreach (var (name, definition) in additions)
        {
            if (!existing.Contains(name))
            {
                await connection.ExecuteAsync($"ALTER TABLE profit_entries ADD COLUMN {name} {definition};");
            }
        }
    }

    private sealed class SqliteColumnInfo
    {
        public string Name { get; set; } = string.Empty;
    }

    public void ClearActiveConnection()
    {
        _activeDatabaseKey = null;
        _connection = null;
    }
}
