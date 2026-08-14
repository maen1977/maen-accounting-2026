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
    private ContactItemViewModel? _selectedInvoiceContact;
    private ContactItemViewModel? _selectedPaymentContact;
    private DateTime _invoiceDate = DateTime.Today;
    private DateTime _paymentDate = DateTime.Today;

    public BusinessViewModel(BusinessRepository repository, DeviceIdentityService deviceIdentity)
    {
        _repository = repository;
        _deviceIdentity = deviceIdentity;
    }

    public static IReadOnlyList<ContactTypeOption> ContactTypes { get; } =
    [new(ContactType.Customer, "عميل"), new(ContactType.Supplier, "مورد")];

    public static IReadOnlyList<InvoiceTypeOption> InvoiceTypes { get; } =
    [new(InvoiceType.Sales, "فاتورة مبيعات"), new(InvoiceType.Purchase, "فاتورة مشتريات")];

    public static IReadOnlyList<PaymentTypeOption> PaymentTypes { get; } =
    [new(PaymentType.CustomerReceipt, "قبض من عميل"), new(PaymentType.SupplierPayment, "دفعة لمورد")];

    public ObservableCollection<ContactItemViewModel> Contacts { get; } = [];
    public ObservableCollection<InvoiceItemViewModel> Invoices { get; } = [];
    public ObservableCollection<PaymentItemViewModel> Payments { get; } = [];

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
    public DateTime PaymentDate { get => _paymentDate; set => SetProperty(ref _paymentDate, value); }
    public ContactTypeOption SelectedContactType { get => _selectedContactType; set => SetProperty(ref _selectedContactType, value); }
    public InvoiceTypeOption SelectedInvoiceType { get => _selectedInvoiceType; set => SetProperty(ref _selectedInvoiceType, value); }
    public PaymentTypeOption SelectedPaymentType { get => _selectedPaymentType; set => SetProperty(ref _selectedPaymentType, value); }
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
            var contacts = await _repository.GetContactsAsync(session.UserId);
            var invoices = await _repository.GetInvoicesAsync(session.UserId);
            var payments = await _repository.GetPaymentsAsync(session.UserId);
            Contacts.Clear();
            foreach (var contact in contacts) Contacts.Add(new ContactItemViewModel(contact));
            Invoices.Clear();
            foreach (var invoice in invoices) Invoices.Add(new InvoiceItemViewModel(invoice, contacts));
            Payments.Clear();
            foreach (var payment in payments) Payments.Add(new PaymentItemViewModel(payment, contacts));
            SelectedInvoiceContact ??= Contacts.FirstOrDefault();
            SelectedPaymentContact ??= Contacts.FirstOrDefault();
        });
    }

    public async Task SaveContactAsync()
    {
        var session = RequireSession();
        if (string.IsNullOrWhiteSpace(ContactNameInput)) throw new InvalidOperationException("اسم العميل أو المورد مطلوب.");
        await RunBusyAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            await _repository.UpsertContactAsync(session.UserId, new AccountingContact(
                $"contact-{Guid.NewGuid():N}", session.UserId, SelectedContactType.Type, ContactNameInput.Trim(), ContactPhoneInput.Trim(), CreatedAtUtc: now, UpdatedAtUtc: now, DeviceId: _deviceIdentity.GetOrCreate()));
            ContactNameInput = string.Empty;
            ContactPhoneInput = string.Empty;
            StatusMessage = "تم حفظ العميل أو المورد.";
            await ReloadCoreAsync();
        });
    }

    public async Task SaveInvoiceAsync()
    {
        var session = RequireSession();
        if (SelectedInvoiceContact is null) throw new InvalidOperationException("اختر العميل أو المورد.");
        if (!Money.TryParse(InvoiceAmountInput, out var amount) || amount <= 0) throw new InvalidOperationException("أدخل مبلغ الفاتورة بشكل صحيح.");
        if (!Money.TryParse(InvoiceTaxInput, out var tax) || tax < 0) throw new InvalidOperationException("أدخل الضريبة بشكل صحيح.");
        if (string.IsNullOrWhiteSpace(InvoiceNumberInput) || string.IsNullOrWhiteSpace(InvoiceDescriptionInput)) throw new InvalidOperationException("رقم الفاتورة ووصفها مطلوبان.");
        await RunBusyAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            var invoice = new Invoice(
                $"invoice-{Guid.NewGuid():N}", session.UserId, InvoiceNumberInput.Trim(), SelectedInvoiceType.Type,
                DateOnly.FromDateTime(InvoiceDate), DateOnly.FromDateTime(InvoiceDate), SelectedInvoiceContact.ContactId,
                [new InvoiceLine($"line-{Guid.NewGuid():N}", InvoiceDescriptionInput.Trim(), amount)], tax,
                InvoiceStatus.Posted, CreatedAtUtc: now, UpdatedAtUtc: now, DeviceId: _deviceIdentity.GetOrCreate());
            await _repository.UpsertInvoiceAsync(session.UserId, invoice);
            InvoiceNumberInput = string.Empty;
            InvoiceAmountInput = string.Empty;
            InvoiceTaxInput = "0";
            InvoiceDescriptionInput = string.Empty;
            StatusMessage = "تم حفظ الفاتورة وترحيل قيدها.";
            await ReloadCoreAsync();
        });
    }

    public async Task SavePaymentAsync()
    {
        var session = RequireSession();
        if (SelectedPaymentContact is null) throw new InvalidOperationException("اختر العميل أو المورد.");
        if (!Money.TryParse(PaymentAmountInput, out var amount) || amount <= 0) throw new InvalidOperationException("أدخل مبلغ الدفعة بشكل صحيح.");
        if (string.IsNullOrWhiteSpace(PaymentNumberInput)) throw new InvalidOperationException("رقم الدفعة مطلوب.");
        await RunBusyAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            var payment = new Payment(
                $"payment-{Guid.NewGuid():N}", session.UserId, PaymentNumberInput.Trim(), SelectedPaymentType.Type,
                DateOnly.FromDateTime(PaymentDate), SelectedPaymentContact.ContactId, amount,
                CreatedAtUtc: now, UpdatedAtUtc: now, DeviceId: _deviceIdentity.GetOrCreate());
            await _repository.UpsertPaymentAsync(session.UserId, payment);
            PaymentNumberInput = string.Empty;
            PaymentAmountInput = string.Empty;
            StatusMessage = "تم حفظ الدفعة وترحيل قيدها.";
            await ReloadCoreAsync();
        });
    }

    private async Task ReloadCoreAsync()
    {
        var session = RequireSession();
        var contacts = await _repository.GetContactsAsync(session.UserId);
        var invoices = await _repository.GetInvoicesAsync(session.UserId);
        var payments = await _repository.GetPaymentsAsync(session.UserId);
        Contacts.Clear();
        foreach (var contact in contacts) Contacts.Add(new ContactItemViewModel(contact));
        Invoices.Clear();
        foreach (var invoice in invoices) Invoices.Add(new InvoiceItemViewModel(invoice, contacts));
        Payments.Clear();
        foreach (var payment in payments) Payments.Add(new PaymentItemViewModel(payment, contacts));
        SelectedInvoiceContact ??= Contacts.FirstOrDefault();
        SelectedPaymentContact ??= Contacts.FirstOrDefault();
    }

    private AuthSession RequireSession() => _session ?? throw new InvalidOperationException("لا توجد جلسة مستخدم نشطة.");

    private async Task RunBusyAsync(Func<Task> action)
    {
        await _gate.WaitAsync();
        try { IsBusy = true; await action(); }
        finally { IsBusy = false; _gate.Release(); }
    }
}

public sealed record ContactTypeOption(ContactType Type, string Label);
public sealed record InvoiceTypeOption(InvoiceType Type, string Label);
public sealed record PaymentTypeOption(PaymentType Type, string Label);

public sealed class ContactItemViewModel(AccountingContact contact)
{
    public string ContactId => contact.ContactId;
    public string Name => contact.Name;
    public string Phone => contact.Phone;
    public string TypeText => contact.Type == ContactType.Customer ? "عميل" : "مورد";
    public string DisplayText => $"{Name} — {TypeText}";
}

public sealed class InvoiceItemViewModel(Invoice invoice, IReadOnlyList<AccountingContact> contacts)
{
    public string Number => invoice.Number;
    public string DateText => invoice.IssueDate.ToString("yyyy-MM-dd");
    public string TypeText => invoice.Type == InvoiceType.Sales ? "مبيعات" : "مشتريات";
    public string ContactText => contacts.FirstOrDefault(contact => contact.ContactId == invoice.ContactId)?.Name ?? "غير معروف";
    public string TotalText => Money.Format(invoice.TotalMinor);
    public string StatusText => invoice.Status == InvoiceStatus.Posted ? "مرحّلة" : "مسودة";
}

public sealed class PaymentItemViewModel(Payment payment, IReadOnlyList<AccountingContact> contacts)
{
    public string Number => payment.Number;
    public string DateText => payment.PaymentDate.ToString("yyyy-MM-dd");
    public string TypeText => payment.Type == PaymentType.CustomerReceipt ? "قبض" : "دفع";
    public string ContactText => contacts.FirstOrDefault(contact => contact.ContactId == payment.ContactId)?.Name ?? "غير معروف";
    public string AmountText => Money.Format(payment.AmountMinor);
}
