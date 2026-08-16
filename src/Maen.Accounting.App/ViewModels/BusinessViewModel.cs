using System.Collections.ObjectModel;
using Maen.Accounting.App.Infrastructure;
using Maen.Accounting.App.Data;
using Maen.Accounting.App.Services;
using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;
using AccountingContact = Maen.Accounting.Core.Models.Contact;

namespace Maen.Accounting.App.ViewModels;

public sealed class BusinessViewModel : ObservableObject
{
    private readonly BusinessRepository _repository;
    private readonly DeviceIdentityService _deviceIdentity;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AuthSession? _session;
    private bool _isBusy;
    private IReadOnlyList<AccountingContact> _rawContacts = [];
    private IReadOnlyList<Invoice> _rawInvoices = [];
    private IReadOnlyList<Payment> _rawPayments = [];
    private string _statusMessage = string.Empty;
    private string _contactNameInput = string.Empty;
    private string _contactPhoneInput = string.Empty;
    private string _invoiceNumberInput = string.Empty;
    private string _invoiceAmountInput = string.Empty;
    private string _invoiceTaxInput = "0";
    private string _invoiceDescriptionInput = string.Empty;
    private string _paymentNumberInput = string.Empty;
    private string _paymentAmountInput = string.Empty;
    private ContactTypeOption _selectedContactType = ContactTypes[0];
    private InvoiceTypeOption _selectedInvoiceType = InvoiceTypes[0];
    private PaymentTypeOption _selectedPaymentType = PaymentTypes[0];
    private PaymentAccountOption _selectedPaymentAccount = PaymentAccounts[0];
    private ContactItemViewModel? _selectedInvoiceContact;
    private ContactItemViewModel? _selectedPaymentContact;
    private DateTime _invoiceDate = DateTime.Today;
    private DateTime _invoiceDueDate = DateTime.Today;
    private DateTime _paymentDate = DateTime.Today;

    public BusinessViewModel(BusinessRepository repository, DeviceIdentityService deviceIdentity)
    {
        _repository = repository;
        _deviceIdentity = deviceIdentity;
    }

    public static IReadOnlyList<ContactTypeOption> ContactTypes { get; } =
    [new(ContactType.Customer), new(ContactType.Supplier)];

    public static IReadOnlyList<InvoiceTypeOption> InvoiceTypes { get; } =
    [new(InvoiceType.Sales), new(InvoiceType.Purchase)];

    public static IReadOnlyList<PaymentTypeOption> PaymentTypes { get; } =
    [new(PaymentType.CustomerReceipt), new(PaymentType.SupplierPayment)];

    public static IReadOnlyList<PaymentAccountOption> PaymentAccounts { get; } =
    [new(DefaultChartOfAccounts.CashCode), new(DefaultChartOfAccounts.BankCode)];

    public ObservableCollection<ContactItemViewModel> Contacts { get; } = [];
    public ObservableCollection<InvoiceItemViewModel> Invoices { get; } = [];
    public ObservableCollection<PaymentItemViewModel> Payments { get; } = [];

    public BusinessFinancialSummary? FinancialPosition => _financialPosition;
    public string ReceivableNetText => Money.Format(_financialPosition?.ReceivableNetMinor ?? 0);
    public string PayableNetText => Money.Format(_financialPosition?.PayableNetMinor ?? 0);
    public string NetPositionText => Money.Format(_financialPosition?.NetPositionMinor ?? 0);
    public string NetPositionColor =>
        (_financialPosition?.NetPositionMinor ?? 0) >= 0 ? "#137A53" : "#C2413A";
    public string OverdueSalesText => Money.Format(_financialPosition?.OverdueSalesMinor ?? 0);
    public string OverduePurchasesText => Money.Format(_financialPosition?.OverduePurchasesMinor ?? 0);
    public bool HasFinancialPosition => _financialPosition is not null;

    private BusinessFinancialSummary? _financialPosition;

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ContactNameInput { get => _contactNameInput; set => SetProperty(ref _contactNameInput, value); }
    public string ContactPhoneInput { get => _contactPhoneInput; set => SetProperty(ref _contactPhoneInput, value); }
    public string InvoiceNumberInput { get => _invoiceNumberInput; set => SetProperty(ref _invoiceNumberInput, value); }
    public string InvoiceAmountInput { get => _invoiceAmountInput; set => SetProperty(ref _invoiceAmountInput, value); }
    public string InvoiceTaxInput { get => _invoiceTaxInput; set => SetProperty(ref _invoiceTaxInput, value); }
    public string InvoiceDescriptionInput { get => _invoiceDescriptionInput; set => SetProperty(ref _invoiceDescriptionInput, value); }
    public string PaymentNumberInput { get => _paymentNumberInput; set => SetProperty(ref _paymentNumberInput, value); }
    public string PaymentAmountInput { get => _paymentAmountInput; set => SetProperty(ref _paymentAmountInput, value); }
    public DateTime InvoiceDate { get => _invoiceDate; set => SetProperty(ref _invoiceDate, value); }
    public DateTime InvoiceDueDate { get => _invoiceDueDate; set => SetProperty(ref _invoiceDueDate, value); }
    public DateTime PaymentDate { get => _paymentDate; set => SetProperty(ref _paymentDate, value); }
    public ContactTypeOption SelectedContactType { get => _selectedContactType; set => SetProperty(ref _selectedContactType, value); }
    public InvoiceTypeOption SelectedInvoiceType { get => _selectedInvoiceType; set => SetProperty(ref _selectedInvoiceType, value); }
    public PaymentTypeOption SelectedPaymentType { get => _selectedPaymentType; set => SetProperty(ref _selectedPaymentType, value); }
    public PaymentAccountOption SelectedPaymentAccount { get => _selectedPaymentAccount; set => SetProperty(ref _selectedPaymentAccount, value); }
    public ContactItemViewModel? SelectedInvoiceContact { get => _selectedInvoiceContact; set => SetProperty(ref _selectedInvoiceContact, value); }
    public ContactItemViewModel? SelectedPaymentContact { get => _selectedPaymentContact; set => SetProperty(ref _selectedPaymentContact, value); }

    public async Task InitializeAsync(AuthSession session)
    {
        _session = session;
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        await RunBusyAsync(async () =>
        {
            var session = RequireSession();
            _rawContacts = await _repository.GetContactsAsync(session.UserId);
            _rawInvoices = await _repository.GetInvoicesAsync(session.UserId);
            _rawPayments = await _repository.GetPaymentsAsync(session.UserId);
            Contacts.Clear();
            foreach (var contact in _rawContacts) Contacts.Add(new ContactItemViewModel(contact));
            Invoices.Clear();
            foreach (var invoice in _rawInvoices) Invoices.Add(new InvoiceItemViewModel(invoice, _rawContacts));
            Payments.Clear();
            foreach (var payment in _rawPayments) Payments.Add(new PaymentItemViewModel(payment, _rawContacts));
            SelectedInvoiceContact ??= Contacts.FirstOrDefault();
            SelectedPaymentContact ??= Contacts.FirstOrDefault();
            SelectedPaymentAccount ??= PaymentAccounts[0];
            RebuildFinancialPosition();
        });
    }

    public async Task SaveContactAsync()
    {
        var session = RequireSession();
        if (string.IsNullOrWhiteSpace(ContactNameInput)) throw new InvalidOperationException(UiText.Get("T267"));
        await RunBusyAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            await _repository.UpsertContactAsync(session.UserId, new AccountingContact(
                $"contact-{Guid.NewGuid():N}", session.UserId, SelectedContactType.Type, ContactNameInput.Trim(), ContactPhoneInput.Trim(), CreatedAtUtc: now, UpdatedAtUtc: now, DeviceId: _deviceIdentity.GetOrCreate()));
            ContactNameInput = string.Empty;
            ContactPhoneInput = string.Empty;
            StatusMessage = UiText.Get("T268");
            await ReloadCoreAsync();
        });
    }

    public async Task SaveInvoiceAsync()
    {
        var session = RequireSession();
        if (SelectedInvoiceContact is null) throw new InvalidOperationException(UiText.Get("T269"));
        if (!Money.TryParse(InvoiceAmountInput, out var amount) || amount <= 0) throw new InvalidOperationException(UiText.Get("T270"));
        if (!Money.TryParse(InvoiceTaxInput, out var tax) || tax < 0) throw new InvalidOperationException(UiText.Get("T271"));
        if (InvoiceDueDate.Date < InvoiceDate.Date) throw new InvalidOperationException(UiText.Get("T359"));
        if (string.IsNullOrWhiteSpace(InvoiceNumberInput) || string.IsNullOrWhiteSpace(InvoiceDescriptionInput)) throw new InvalidOperationException(UiText.Get("T272"));
        await RunBusyAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            var invoice = new Invoice(
                $"invoice-{Guid.NewGuid():N}", session.UserId, InvoiceNumberInput.Trim(), SelectedInvoiceType.Type,
                DateOnly.FromDateTime(InvoiceDate), DateOnly.FromDateTime(InvoiceDueDate), SelectedInvoiceContact.ContactId,
                [new InvoiceLine($"line-{Guid.NewGuid():N}", InvoiceDescriptionInput.Trim(), amount)], tax,
                InvoiceStatus.Posted, CreatedAtUtc: now, UpdatedAtUtc: now, DeviceId: _deviceIdentity.GetOrCreate());
            await _repository.UpsertInvoiceAsync(session.UserId, invoice);
            InvoiceNumberInput = string.Empty;
            InvoiceAmountInput = string.Empty;
            InvoiceTaxInput = "0";
            InvoiceDescriptionInput = string.Empty;
            InvoiceDueDate = InvoiceDate;
            StatusMessage = UiText.Get("T273");
            await ReloadCoreAsync();
        });
    }

    public async Task SavePaymentAsync()
    {
        var session = RequireSession();
        if (SelectedPaymentContact is null) throw new InvalidOperationException(UiText.Get("T269"));
        if (!Money.TryParse(PaymentAmountInput, out var amount) || amount <= 0) throw new InvalidOperationException(UiText.Get("T274"));
        if (string.IsNullOrWhiteSpace(PaymentNumberInput)) throw new InvalidOperationException(UiText.Get("T275"));
        await RunBusyAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            var payment = new Payment(
                $"payment-{Guid.NewGuid():N}", session.UserId, PaymentNumberInput.Trim(), SelectedPaymentType.Type,
                DateOnly.FromDateTime(PaymentDate), SelectedPaymentContact.ContactId, amount,
                CreatedAtUtc: now, UpdatedAtUtc: now, DeviceId: _deviceIdentity.GetOrCreate(),
                AccountCode: SelectedPaymentAccount.Code);
            await _repository.UpsertPaymentAsync(session.UserId, payment);
            PaymentNumberInput = string.Empty;
            PaymentAmountInput = string.Empty;
            StatusMessage = UiText.Get("T276");
            await ReloadCoreAsync();
        });
    }

    private async Task ReloadCoreAsync()
    {
        var session = RequireSession();
        _rawContacts = await _repository.GetContactsAsync(session.UserId);
        _rawInvoices = await _repository.GetInvoicesAsync(session.UserId);
        _rawPayments = await _repository.GetPaymentsAsync(session.UserId);
        Contacts.Clear();
        foreach (var contact in _rawContacts) Contacts.Add(new ContactItemViewModel(contact));
        Invoices.Clear();
        foreach (var invoice in _rawInvoices) Invoices.Add(new InvoiceItemViewModel(invoice, _rawContacts));
        Payments.Clear();
        foreach (var payment in _rawPayments) Payments.Add(new PaymentItemViewModel(payment, _rawContacts));
        SelectedInvoiceContact ??= Contacts.FirstOrDefault();
        SelectedPaymentContact ??= Contacts.FirstOrDefault();
        RebuildFinancialPosition();
    }

    private void RebuildFinancialPosition()
    {
        _financialPosition = _rawContacts.Count == 0 && _rawInvoices.Count == 0 && _rawPayments.Count == 0
            ? null
            : BusinessFinancialSummaryCalculator.Summarize(
                _rawContacts,
                _rawInvoices,
                _rawPayments,
                DateOnly.FromDateTime(DateTime.Today));
        OnPropertyChanged(nameof(FinancialPosition));
        OnPropertyChanged(nameof(HasFinancialPosition));
        OnPropertyChanged(nameof(ReceivableNetText));
        OnPropertyChanged(nameof(PayableNetText));
        OnPropertyChanged(nameof(NetPositionText));
        OnPropertyChanged(nameof(NetPositionColor));
        OnPropertyChanged(nameof(OverdueSalesText));
        OnPropertyChanged(nameof(OverduePurchasesText));
    }

    private AuthSession RequireSession() => _session ?? throw new InvalidOperationException(UiText.Get("T245"));

    private async Task RunBusyAsync(Func<Task> action)
    {
        await _gate.WaitAsync();
        try { IsBusy = true; await action(); }
        finally { IsBusy = false; _gate.Release(); }
    }
}

public sealed record ContactTypeOption(ContactType Type)
{
    public string Label => Type == ContactType.Customer ? UiText.Get("T261") : UiText.Get("T262");
}

public sealed record InvoiceTypeOption(InvoiceType Type)
{
    public string Label => Type == InvoiceType.Sales ? UiText.Get("T263") : UiText.Get("T264");
}

public sealed record PaymentTypeOption(PaymentType Type)
{
    public string Label => Type == PaymentType.CustomerReceipt ? UiText.Get("T265") : UiText.Get("T266");
}

public sealed record PaymentAccountOption(string Code)
{
    public string Label => Code == DefaultChartOfAccounts.BankCode ? UiText.Get("T370") : UiText.Get("T371");
    public string DisplayText => $"{Code} — {Label}";
}

public sealed class ContactItemViewModel(AccountingContact contact)
{
    public string ContactId => contact.ContactId;
    public string Name => contact.Name;
    public string Phone => contact.Phone;
    public string TypeText => contact.Type == ContactType.Customer ? UiText.Get("T261") : UiText.Get("T262");
    public string DisplayText => $"{Name} — {TypeText}";
}

public sealed class InvoiceItemViewModel(Invoice invoice, IReadOnlyList<AccountingContact> contacts)
{
    public string Number => invoice.Number;
    public string DateText => invoice.IssueDate.ToString("yyyy-MM-dd");
    public string DueDateText => $"{UiText.Get("T360")}: {invoice.DueDate:yyyy-MM-dd}";
    public string TypeText => invoice.Type == InvoiceType.Sales ? UiText.Get("T277") : UiText.Get("T278");
    public string ContactText => contacts.FirstOrDefault(contact => contact.ContactId == invoice.ContactId)?.Name ?? UiText.Get("T279");
    public string TotalText => Money.Format(invoice.TotalMinor);
    public string StatusText => invoice.Status == InvoiceStatus.Posted ? UiText.Get("T280") : UiText.Get("T281");
}

public sealed class PaymentItemViewModel(Payment payment, IReadOnlyList<AccountingContact> contacts)
{
    public string Number => payment.Number;
    public string DateText => payment.PaymentDate.ToString("yyyy-MM-dd");
    public string TypeText => payment.Type == PaymentType.CustomerReceipt ? UiText.Get("T282") : UiText.Get("T283");
    public string ContactText => contacts.FirstOrDefault(contact => contact.ContactId == payment.ContactId)?.Name ?? UiText.Get("T279");
    public string AccountText => payment.AccountCode == DefaultChartOfAccounts.BankCode ? UiText.Get("T370") : UiText.Get("T371");
    public string AmountText => Money.Format(payment.AmountMinor);
}
