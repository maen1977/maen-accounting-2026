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
            await connection.CreateTableAsync<AccountRow>();
            await connection.CreateTableAsync<JournalEntryRow>();
            await connection.CreateTableAsync<JournalLineRow>();
            await connection.CreateTableAsync<ContactRow>();
            await connection.CreateTableAsync<InvoiceRow>();
            await connection.CreateTableAsync<InvoiceLineRow>();
            await connection.CreateTableAsync<PaymentRow>();
            await connection.ExecuteAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS ux_profit_entries_user_date " +
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
