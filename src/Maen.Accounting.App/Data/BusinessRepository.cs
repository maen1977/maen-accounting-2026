using Maen.Accounting.Core.Models;
using AccountingContact = Maen.Accounting.Core.Models.Contact;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.Data;

public sealed class BusinessRepository
{
    private readonly UserDatabaseFactory _databaseFactory;
    private readonly AccountingRepository _accountingRepository;

    public BusinessRepository(
        UserDatabaseFactory databaseFactory,
        AccountingRepository accountingRepository)
    {
        _databaseFactory = databaseFactory;
        _accountingRepository = accountingRepository;
    }

    public async Task<IReadOnlyList<AccountingContact>> GetContactsAsync(string userId, ContactType? type = null)
    {
        var database = await _databaseFactory.GetAsync(userId);
        var rows = await database.Table<ContactRow>()
            .Where(row => row.UserId == userId && row.IsActive)
            .ToListAsync();
        return rows
            .Select(static row => row.ToModel())
            .Where(contact => type is null || contact.Type == type.Value)
            .OrderBy(static contact => contact.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task UpsertContactAsync(string userId, AccountingContact contact)
    {
        UserIsolation.EnsureOwner(userId, contact.UserId);
        if (string.IsNullOrWhiteSpace(contact.Name))
        {
            throw new ArgumentException("اسم العميل أو المورد مطلوب.");
        }

        var database = await _databaseFactory.GetAsync(userId);
        await database.InsertOrReplaceAsync(ContactRow.FromModel(contact));
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoicesAsync(string userId)
    {
        var database = await _databaseFactory.GetAsync(userId);
        var invoiceRows = await database.Table<InvoiceRow>()
            .Where(row => row.UserId == userId)
            .ToListAsync();
        var lineRows = await database.Table<InvoiceLineRow>()
            .Where(row => row.UserId == userId)
            .ToListAsync();
        var linesByInvoice = lineRows
            .GroupBy(static row => row.InvoiceId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<InvoiceLine>)group.Select(static row => row.ToModel()).ToArray(),
                StringComparer.Ordinal);

        return invoiceRows
            .Select(row =>
            {
                linesByInvoice.TryGetValue(row.InvoiceId, out var lines);
                return row.ToModel(lines ?? Array.Empty<InvoiceLine>());
            })
            .OrderByDescending(static invoice => invoice.IssueDate)
            .ThenByDescending(static invoice => invoice.Number, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task UpsertInvoiceAsync(string userId, Invoice invoice)
    {
        UserIsolation.EnsureOwner(userId, invoice.UserId);
        if (invoice.Status == InvoiceStatus.Posted)
        {
            BusinessDocumentValidator.EnsureValidInvoice(invoice);
        }

        var database = await _databaseFactory.GetAsync(userId);
        await database.RunInTransactionAsync(connection =>
        {
            connection.InsertOrReplace(InvoiceRow.FromModel(invoice));
            connection.Execute("DELETE FROM invoice_lines WHERE InvoiceId = ? AND UserId = ?", invoice.InvoiceId, userId);
            foreach (var line in invoice.Lines)
            {
                connection.InsertOrReplace(InvoiceLineRow.FromModel(invoice.InvoiceId, userId, line));
            }
        });

        if (invoice.Status == InvoiceStatus.Posted)
        {
            await _accountingRepository.UpsertJournalEntryAsync(
                userId,
                DocumentJournalFactory.CreateInvoiceJournal(invoice));
        }
    }

    public async Task<IReadOnlyList<Payment>> GetPaymentsAsync(string userId)
    {
        var database = await _databaseFactory.GetAsync(userId);
        var rows = await database.Table<PaymentRow>()
            .Where(row => row.UserId == userId)
            .ToListAsync();
        return rows
            .Select(static row => row.ToModel())
            .OrderByDescending(static payment => payment.PaymentDate)
            .ThenByDescending(static payment => payment.Number, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task UpsertPaymentAsync(string userId, Payment payment)
    {
        UserIsolation.EnsureOwner(userId, payment.UserId);
        BusinessDocumentValidator.EnsureValidPayment(payment);
        var database = await _databaseFactory.GetAsync(userId);
        await database.InsertOrReplaceAsync(PaymentRow.FromModel(payment));
        await _accountingRepository.UpsertJournalEntryAsync(
            userId,
            DocumentJournalFactory.CreatePaymentJournal(payment));
    }
}
