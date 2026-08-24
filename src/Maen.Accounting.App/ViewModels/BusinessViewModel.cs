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
    private string _invoiceSearchText = string.Empty;
    private string _paymentSearchText = string.Empty;
    private string _contactSearchText = string.Empty;
    private Invoice? _editingInvoice;
    private Payment? _editingPayment;
    private AccountingContact? _editingContact;

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
    public string InvoiceSearchText { get => _invoiceSearchText; set { if (SetProperty(ref _invoiceSearchText, value)) ApplyFilters(); } }
    public string PaymentSearchText { get => _paymentSearchText; set { if (SetProperty(ref _paymentSearchText, value)) ApplyFilters(); } }
    public string ContactSearchText { get => _contactSearchText; set { if (SetProperty(ref _contactSearchText, value)) ApplyFilters(); } }
    public ObservableCollection<InvoiceItemViewModel> FilteredInvoices { get; } = [];
    public ObservableCollection<PaymentItemViewModel> FilteredPayments { get; } = [];
    public ObservableCollection<ContactItemViewModel> FilteredContacts { get; } = [];
    public ObservableCollection<ReconciledInvoiceItem> ReconciledInvoices { get; } = [];
    public string InvoiceFormTitle => _editingInvoice is null ? UiText.Get("T074") : UiText.Get("T488");
    public string ContactFormTitle => _editingContact is null ? UiText.Get("T073") : UiText.Get("T837");
    public string ContactSaveButtonText => _editingContact is null ? UiText.Get("T062") : UiText.Get("T831");
    public string PaymentFormTitle => _editingPayment is null ? UiText.Get("T065") : UiText.Get("T841");
    public string PaymentSaveButtonText => _editingPayment is null ? UiText.Get("T063") : UiText.Get("T831");

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
            SelectedInvoiceContact = Contacts.FirstOrDefault(contact => contact.ContactId == SelectedInvoiceContact?.ContactId) ?? Contacts.FirstOrDefault();
            SelectedPaymentContact = Contacts.FirstOrDefault(contact => contact.ContactId == SelectedPaymentContact?.ContactId) ?? Contacts.FirstOrDefault();
            SelectedPaymentAccount ??= PaymentAccounts[0];
            RebuildFinancialPosition();
            RebuildReconciliation();
            ApplyFilters();
        });
    }

    public async Task SaveContactAsync()
    {
        var session = RequireSession();
        ContactNameInput = InputSanitizer.SanitizeName(ContactNameInput);
        ContactPhoneInput = InputSanitizer.SanitizeNotes(ContactPhoneInput);
        if (string.IsNullOrWhiteSpace(ContactNameInput)) throw new InvalidOperationException(UiText.Get("T267"));
        await RunBusyAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            if (_editingContact is null)
            {
                await _repository.UpsertContactAsync(session.UserId, new AccountingContact(
                    $"contact-{Guid.NewGuid():N}", session.UserId, SelectedContactType.Type, ContactNameInput.Trim(), ContactPhoneInput.Trim(), CreatedAtUtc: now, UpdatedAtUtc: now, DeviceId: _deviceIdentity.GetOrCreate()));
            }
            else
            {
                var edited = _editingContact with
                {
                    Type = SelectedContactType.Type,
                    Name = ContactNameInput.Trim(),
                    Phone = ContactPhoneInput.Trim(),
                    UpdatedAtUtc = now,
                    Version = checked(_editingContact.Version + 1),
                    DeviceId = _deviceIdentity.GetOrCreate(),
                };
                await _repository.UpsertContactAsync(session.UserId, edited);
            }

            ContactNameInput = string.Empty;
            ContactPhoneInput = string.Empty;
            _editingContact = null;
            OnPropertyChanged(nameof(ContactFormTitle));
            OnPropertyChanged(nameof(ContactSaveButtonText));
            StatusMessage = UiText.Get("T268");
            await ReloadCoreAsync();
        });
    }

    public void BeginEditContact(ContactItemViewModel item)
    {
        var contact = _rawContacts.FirstOrDefault(raw => raw.ContactId == item.ContactId);
        if (contact is null) return;
        _editingContact = contact;
        ContactNameInput = contact.Name;
        ContactPhoneInput = contact.Phone;
        SelectedContactType = ContactTypes.FirstOrDefault(type => type.Type == contact.Type) ?? ContactTypes[0];
        OnPropertyChanged(nameof(ContactFormTitle));
        OnPropertyChanged(nameof(ContactSaveButtonText));
    }

    public async Task SaveInvoiceAsync()
    {
        var session = RequireSession();
        if (SelectedInvoiceContact is null) throw new InvalidOperationException(UiText.Get("T269"));
        var sanitizedNumber = InputSanitizer.SanitizeName(InvoiceNumberInput);
        InvoiceNumberInput = sanitizedNumber;
        var sanitizedDescription = InputSanitizer.SanitizeNotes(InvoiceDescriptionInput);
        InvoiceDescriptionInput = sanitizedDescription;
        if (_editingInvoice is not null)
        {
            await SaveEditedInvoiceAsync(session, sanitizedNumber, sanitizedDescription);
            return;
        }
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
                [new InvoiceLine($"line-{Guid.NewGuid():N}", sanitizedDescription, amount)], tax,
                InvoiceStatus.Posted, CreatedAtUtc: now, UpdatedAtUtc: now, DeviceId: _deviceIdentity.GetOrCreate());
            BusinessDocumentValidator.EnsureValidInvoice(invoice);
            await _repository.UpsertInvoiceAsync(session.UserId, invoice);
            InvoiceNumberInput = string.Empty;
            InvoiceAmountInput = string.Empty;
            InvoiceTaxInput = "0";
            InvoiceDescriptionInput = string.Empty;
            InvoiceDueDate = InvoiceDate;
            StatusMessage = UiText.Get("T273");
            _editingInvoice = null;
            OnPropertyChanged(nameof(InvoiceFormTitle));
            await ReloadCoreAsync();
        });
    }

    private async Task SaveEditedInvoiceAsync(AuthSession session, string sanitizedNumber, string sanitizedDescription)
    {
        if (!Money.TryParse(InvoiceAmountInput, out var amount) || amount <= 0) throw new InvalidOperationException(UiText.Get("T270"));
        if (!Money.TryParse(InvoiceTaxInput, out var tax) || tax < 0) throw new InvalidOperationException(UiText.Get("T271"));
        if (InvoiceDueDate.Date < InvoiceDate.Date) throw new InvalidOperationException(UiText.Get("T359"));
        if (string.IsNullOrWhiteSpace(InvoiceNumberInput) || string.IsNullOrWhiteSpace(InvoiceDescriptionInput)) throw new InvalidOperationException(UiText.Get("T272"));
        await RunBusyAsync(async () =>
        {
            var edited = BusinessDocumentLifecycleService.BuildNextVersion(_editingInvoice!, builder =>
            {
                builder.SetNumber(sanitizedNumber);
                builder.SetIssueDate(DateOnly.FromDateTime(InvoiceDate));
                builder.SetDueDate(DateOnly.FromDateTime(InvoiceDueDate));
                builder.SetTaxMinor(tax);
                builder.SetLines([new InvoiceLine(_editingInvoice!.Lines[0].LineId, sanitizedDescription, amount)]);
            });
            BusinessDocumentValidator.EnsureValidInvoice(edited);
            await _repository.UpsertInvoiceAsync(session.UserId, edited);
            _editingInvoice = null;
            OnPropertyChanged(nameof(InvoiceFormTitle));
            ClearInvoiceInputs();
            StatusMessage = UiText.Get("T273");
            await ReloadCoreAsync();
        });
    }

    private void ClearInvoiceInputs()
    {
        InvoiceNumberInput = string.Empty;
        InvoiceAmountInput = string.Empty;
        InvoiceTaxInput = "0";
        InvoiceDescriptionInput = string.Empty;
        InvoiceDueDate = InvoiceDate;
    }

    public async Task SavePaymentAsync()
    {
        var session = RequireSession();
        if (SelectedPaymentContact is null) throw new InvalidOperationException(UiText.Get("T269"));
        if (!Money.TryParse(PaymentAmountInput, out var amount) || amount <= 0) throw new InvalidOperationException(UiText.Get("T274"));
        var sanitizedPaymentNumber = InputSanitizer.SanitizeName(PaymentNumberInput);
        PaymentNumberInput = sanitizedPaymentNumber;
        if (string.IsNullOrWhiteSpace(sanitizedPaymentNumber)) throw new InvalidOperationException(UiText.Get("T275"));
        await RunBusyAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            var payment = _editingPayment is null
                ? new Payment(
                    $"payment-{Guid.NewGuid():N}", session.UserId, sanitizedPaymentNumber, SelectedPaymentType.Type,
                    DateOnly.FromDateTime(PaymentDate), SelectedPaymentContact.ContactId, amount,
                    CreatedAtUtc: now, UpdatedAtUtc: now, DeviceId: _deviceIdentity.GetOrCreate(),
                    AccountCode: SelectedPaymentAccount.Code)
                : _editingPayment with
                {
                    Number = sanitizedPaymentNumber,
                    Type = SelectedPaymentType.Type,
                    PaymentDate = DateOnly.FromDateTime(PaymentDate),
                    ContactId = SelectedPaymentContact.ContactId,
                    AmountMinor = amount,
                    UpdatedAtUtc = now,
                    Version = checked(_editingPayment.Version + 1),
                    DeviceId = _deviceIdentity.GetOrCreate(),
                    AccountCode = SelectedPaymentAccount.Code,
                };
            BusinessDocumentValidator.EnsureValidPayment(payment);
            await _repository.UpsertPaymentAsync(session.UserId, payment);
            ClearPaymentInputs();
            _editingPayment = null;
            OnPropertyChanged(nameof(PaymentFormTitle));
            OnPropertyChanged(nameof(PaymentSaveButtonText));
            StatusMessage = UiText.Get("T276");
            await ReloadCoreAsync();
        });
    }

    public void EditPayment(PaymentItemViewModel item)
    {
        var payment = _rawPayments.FirstOrDefault(raw => raw.PaymentId == item.ModelPaymentId);
        if (payment is null) return;
        _editingPayment = payment;
        PaymentNumberInput = payment.Number;
        PaymentAmountInput = Money.Format(payment.AmountMinor);
        SelectedPaymentType = PaymentTypes.FirstOrDefault(type => type.Type == payment.Type) ?? PaymentTypes[0];
        SelectedPaymentContact = Contacts.FirstOrDefault(contact => contact.ContactId == payment.ContactId);
        SelectedPaymentAccount = PaymentAccounts.FirstOrDefault(account => account.Code == payment.AccountCode) ?? PaymentAccounts[0];
        PaymentDate = payment.PaymentDate.ToDateTime(TimeOnly.MinValue);
        OnPropertyChanged(nameof(PaymentFormTitle));
        OnPropertyChanged(nameof(PaymentSaveButtonText));
    }

    private void ClearPaymentInputs()
    {
        PaymentNumberInput = string.Empty;
        PaymentAmountInput = string.Empty;
        PaymentDate = DateTime.Today;
    }

    public void EditInvoice(InvoiceItemViewModel item)
    {
        var invoice = _rawInvoices.FirstOrDefault(raw => raw.InvoiceId == item.ModelInvoiceId);
        if (invoice is null || invoice.Status != InvoiceStatus.Draft) return;
        _editingInvoice = invoice;
        InvoiceNumberInput = invoice.Number;
        InvoiceAmountInput = Money.Format(invoice.Lines.Count > 0 ? invoice.Lines[0].AmountMinor : invoice.TotalMinor);
        InvoiceTaxInput = Money.Format(invoice.TaxMinor);
        InvoiceDescriptionInput = invoice.Lines.Count > 0 ? invoice.Lines[0].Description : string.Empty;
        SelectedInvoiceType = InvoiceTypes.FirstOrDefault(type => type.Type == invoice.Type) ?? InvoiceTypes[0];
        SelectedInvoiceContact = Contacts.FirstOrDefault(contact => contact.ContactId == invoice.ContactId);
        InvoiceDate = invoice.IssueDate.ToDateTime(TimeOnly.MinValue);
        InvoiceDueDate = invoice.DueDate.ToDateTime(TimeOnly.MinValue);
        OnPropertyChanged(nameof(InvoiceFormTitle));
    }

    public async Task VoidInvoiceAsync(InvoiceItemViewModel item)
    {
        var session = RequireSession();
        var invoice = _rawInvoices.FirstOrDefault(raw => raw.InvoiceId == item.ModelInvoiceId);
        if (invoice is null) return;
        var voidResult = BusinessDocumentLifecycleService.VoidInvoice(invoice, _rawPayments);
        if (!voidResult.Ok) throw new InvalidOperationException(voidResult.Reason);
        var voided = invoice with { Status = InvoiceStatus.Voided, Version = invoice.Version + 1, UpdatedAtUtc = DateTimeOffset.UtcNow, DeviceId = _deviceIdentity.GetOrCreate() };
        await RunBusyAsync(async () =>
        {
            await _repository.UpsertInvoiceAsync(session.UserId, voided);
            StatusMessage = UiText.Get("T523");
            await ReloadCoreAsync();
        });
    }

    public async Task DeleteInvoiceAsync(InvoiceItemViewModel item)
    {
        var session = RequireSession();
        var invoice = _rawInvoices.FirstOrDefault(raw => raw.InvoiceId == item.ModelInvoiceId);
        if (invoice is null) return;
        var deleteResult = BusinessDocumentLifecycleService.DeleteInvoice(invoice, _rawPayments);
        if (!deleteResult.Ok) throw new InvalidOperationException(deleteResult.Reason);
        await RunBusyAsync(async () =>
        {
            await _repository.DeleteInvoiceAsync(session.UserId, invoice.InvoiceId);
            StatusMessage = UiText.Get("T524");
            await ReloadCoreAsync();
        });
    }

    public async Task DeletePaymentAsync(PaymentItemViewModel item)
    {
        var session = RequireSession();
        var payment = _rawPayments.FirstOrDefault(raw => raw.PaymentId == item.ModelPaymentId);
        if (payment is null) return;
        var paymentResult = BusinessDocumentLifecycleService.DeletePayment(payment, _rawInvoices);
        if (!paymentResult.Ok) throw new InvalidOperationException(paymentResult.Reason);
        await RunBusyAsync(async () =>
        {
            await _repository.DeletePaymentAsync(session.UserId, payment.PaymentId);
            StatusMessage = UiText.Get("T524");
            await ReloadCoreAsync();
        });
    }

    public async Task ToggleContactAsync(ContactItemViewModel item)
    {
        var session = RequireSession();
        var contact = _rawContacts.FirstOrDefault(raw => raw.ContactId == item.ContactId);
        if (contact is null) return;
        var contactResult = contact.IsActive
            ? BusinessDocumentLifecycleService.DeactivateContact(contact, _rawInvoices)
            : BusinessDocumentLifecycleService.Result.Success();
        if (!contactResult.Ok) throw new InvalidOperationException(contactResult.Reason);
        await RunBusyAsync(async () =>
        {
            await _repository.UpsertContactAsync(session.UserId, contact with { IsActive = !contact.IsActive, Version = contact.Version + 1, UpdatedAtUtc = DateTimeOffset.UtcNow, DeviceId = _deviceIdentity.GetOrCreate() });
            StatusMessage = UiText.Get("T525");
            await ReloadCoreAsync();
        });
    }

    public async Task DeleteContactAsync(ContactItemViewModel item)
    {
        var session = RequireSession();
        var contact = _rawContacts.FirstOrDefault(raw => raw.ContactId == item.ContactId);
        if (contact is null) return;
        var contactResult = BusinessDocumentLifecycleService.DeactivateContact(contact, _rawInvoices);
        if (!contactResult.Ok) throw new InvalidOperationException(contactResult.Reason);
        await RunBusyAsync(async () =>
        {
            await _repository.DeleteContactAsync(session.UserId, contact.ContactId);
            if (_editingContact?.ContactId == contact.ContactId)
            {
                _editingContact = null;
                ContactNameInput = string.Empty;
                ContactPhoneInput = string.Empty;
                OnPropertyChanged(nameof(ContactFormTitle));
                OnPropertyChanged(nameof(ContactSaveButtonText));
            }
            StatusMessage = UiText.Get("T839");
            await ReloadCoreAsync();
        });
    }

    private void RebuildReconciliation()
    {
        ReconciledInvoices.Clear();
        foreach (var reconciled in InvoiceReconciliationCalculator.Reconcile(_rawInvoices, _rawPayments))
        {
            ReconciledInvoices.Add(new ReconciledInvoiceItem(reconciled, _rawContacts));
        }
    }

    private void ApplyFilters()
    {
        FilterInvoices();
        FilterPayments();
        FilterContacts();
    }

    private void FilterInvoices()
    {
        var query = _invoiceSearchText.Trim();
        FilteredInvoices.Clear();
        foreach (var item in Invoices.Where(item =>
            query.Length == 0 ||
            item.Number.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            item.ContactText.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            FilteredInvoices.Add(item);
        }
    }

    private void FilterPayments()
    {
        var query = _paymentSearchText.Trim();
        FilteredPayments.Clear();
        foreach (var item in Payments.Where(item =>
            query.Length == 0 ||
            item.Number.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            item.ContactText.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            FilteredPayments.Add(item);
        }
    }

    private void FilterContacts()
    {
        var query = _contactSearchText.Trim();
        FilteredContacts.Clear();
        foreach (var item in Contacts.Where(item =>
            query.Length == 0 || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            FilteredContacts.Add(item);
        }
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

public sealed class ReconciledInvoiceItem(ReconciledInvoice reconciled, IReadOnlyList<AccountingContact> contacts)
{
    public string Number => reconciled.Invoice.Number;
    public string DateText => reconciled.Invoice.IssueDate.ToString("yyyy-MM-dd");
    public string ContactText => contacts.FirstOrDefault(contact => contact.ContactId == reconciled.Invoice.ContactId)?.Name ?? UiText.Get("T279");
    public string TotalText => Money.Format(reconciled.Invoice.TotalMinor);
    public string PaidText => Money.Format(reconciled.PaidMinor);
    public string RemainingText => Money.Format(reconciled.OutstandingMinor);
    public bool IsFullyPaid => reconciled.IsFullyPaid;
    public string RemainingColor => reconciled.HasOutstanding ? "#C2413A" : "#10B981";
    public string StatusText => reconciled.IsFullyPaid ? UiText.Get("T512") : UiText.Get("T513");
}

public sealed class OverdueInvoiceItem(Invoice invoice, IReadOnlyList<AccountingContact> contacts)
{
    public string Number => invoice.Number;
    public string DueDateText => invoice.DueDate.ToString("yyyy-MM-dd");
    public string ContactText => contacts.FirstOrDefault(contact => contact.ContactId == invoice.ContactId)?.Name ?? UiText.Get("T279");
    public string AmountText => Money.Format(invoice.TotalMinor);
    public string TypeText => invoice.Type == InvoiceType.Sales ? UiText.Get("T277") : UiText.Get("T278");
    public string AgeText
    {
        get
        {
            var days = DateOnly.FromDateTime(DateTime.Today).DayNumber - invoice.DueDate.DayNumber;
            if (days <= 0)
            {
                return UiText.Get("T485");
            }

            return UiText.Format("T486", days.ToString());
        }
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
    public string ToggleLabel => UiText.Get("T529");
    public bool IsActive => contact.IsActive;
}

public sealed class InvoiceItemViewModel(Invoice invoice, IReadOnlyList<AccountingContact> contacts)
{
    public string ModelInvoiceId => invoice.InvoiceId;
    public bool IsDraft => invoice.Status == InvoiceStatus.Draft;
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
    public string ModelPaymentId => payment.PaymentId;
    public string Number => payment.Number;
    public string DateText => payment.PaymentDate.ToString("yyyy-MM-dd");
    public string TypeText => payment.Type == PaymentType.CustomerReceipt ? UiText.Get("T282") : UiText.Get("T283");
    public string ContactText => contacts.FirstOrDefault(contact => contact.ContactId == payment.ContactId)?.Name ?? UiText.Get("T279");
    public string AccountText => payment.AccountCode == DefaultChartOfAccounts.BankCode ? UiText.Get("T370") : UiText.Get("T371");
    public string AmountText => Money.Format(payment.AmountMinor);
}
