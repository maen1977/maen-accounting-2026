using System.Collections.ObjectModel;
using Maen.Accounting.App.Data;
using Maen.Accounting.App.Infrastructure;
using Maen.Accounting.App.Services;
using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.ViewModels;

public sealed class AccountingViewModel : ObservableObject
{
    private readonly AccountingRepository _repository;
    private readonly DeviceIdentityService _deviceIdentity;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AuthSession? _session;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private DateTime _journalDate = DateTime.Today;
    private string _journalAmountInput = string.Empty;
    private string _journalDescription = string.Empty;
    private string _accountCodeInput = string.Empty;
    private string _accountNameInput = string.Empty;
    private AccountTypeOption _selectedAccountType = AccountTypes[0];
    private Account? _editingAccount;
    private JournalEntry? _editingJournalEntry;
    private AccountItemViewModel? _selectedDebitAccount;
    private AccountItemViewModel? _selectedCreditAccount;

    public AccountingViewModel(
        AccountingRepository repository,
        DeviceIdentityService deviceIdentity)
    {
        _repository = repository;
        _deviceIdentity = deviceIdentity;
    }

    public ObservableCollection<AccountItemViewModel> Accounts { get; } = [];
    public ObservableCollection<JournalEntryItemViewModel> JournalEntries { get; } = [];
    public ObservableCollection<TrialBalanceItemViewModel> TrialBalanceRows { get; } = [];

    public static IReadOnlyList<AccountTypeOption> AccountTypes { get; } =
    [
        new(AccountType.Asset),
        new(AccountType.Liability),
        new(AccountType.Equity),
        new(AccountType.Revenue),
        new(AccountType.Expense),
    ];

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public DateTime JournalDate
    {
        get => _journalDate;
        set => SetProperty(ref _journalDate, value);
    }

    public string JournalAmountInput
    {
        get => _journalAmountInput;
        set => SetProperty(ref _journalAmountInput, value);
    }

    public string JournalDescription
    {
        get => _journalDescription;
        set => SetProperty(ref _journalDescription, value);
    }

    public string AccountCodeInput
    {
        get => _accountCodeInput;
        set => SetProperty(ref _accountCodeInput, value);
    }

    public string AccountNameInput
    {
        get => _accountNameInput;
        set => SetProperty(ref _accountNameInput, value);
    }

    public AccountTypeOption SelectedAccountType
    {
        get => _selectedAccountType;
        set => SetProperty(ref _selectedAccountType, value);
    }

    public AccountItemViewModel? SelectedDebitAccount
    {
        get => _selectedDebitAccount;
        set => SetProperty(ref _selectedDebitAccount, value);
    }

    public AccountItemViewModel? SelectedCreditAccount
    {
        get => _selectedCreditAccount;
        set => SetProperty(ref _selectedCreditAccount, value);
    }

    public string TotalDebitText { get; private set; } = Money.Format(0);
    public string TotalCreditText { get; private set; } = Money.Format(0);
    public string BalanceStatusText { get; private set; } = UiText.Get("T260");
    public string PostedEntriesSummaryText => UiText.Format("T259", PostedEntriesText);
    public string PostedEntriesText { get; private set; } = "0";
    public string AccountFormTitle => _editingAccount is null ? UiText.Get("T859") : UiText.Get("T844");
    public string AccountSaveButtonText => _editingAccount is null ? UiText.Get("T062") : UiText.Get("T831");
    public string JournalFormTitle => _editingJournalEntry is null ? UiText.Get("T075") : UiText.Get("T845");
    public string JournalSaveButtonText => _editingJournalEntry is null ? UiText.Get("T054") : UiText.Get("T831");

    public async Task InitializeAsync(AuthSession session)
    {
        _session = session;
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        await RunBusyAsync(ReloadCoreAsync);
    }

    public async Task SaveAccountAsync()
    {
        var session = RequireSession();
        var code = (AccountCodeInput ?? string.Empty).Trim().ToUpperInvariant();
        var name = InputSanitizer.SanitizeName(AccountNameInput ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(UiText.Get("T314"));
        }

        await RunBusyAsync(async () =>
        {
            var accounts = await _repository.GetAccountsAsync(session.UserId);
            var duplicate = accounts.FirstOrDefault(account =>
                string.Equals(account.Code, code, StringComparison.OrdinalIgnoreCase)
                && account.AccountId != _editingAccount?.AccountId);
            if (duplicate is not null)
            {
                throw new InvalidOperationException(UiText.Get("T843"));
            }

            var now = DateTimeOffset.UtcNow;
            var account = _editingAccount is null
                ? new Account(
                    $"account-{Guid.NewGuid():N}", session.UserId, code, name, SelectedAccountType.Type,
                    IsSystem: false, IsActive: true, SortOrder: 1000,
                    CreatedAtUtc: now, UpdatedAtUtc: now, DeviceId: _deviceIdentity.GetOrCreate())
                : _editingAccount with
                {
                    Code = code,
                    Name = name,
                    Type = SelectedAccountType.Type,
                    UpdatedAtUtc = now,
                    Version = checked(_editingAccount.Version + 1),
                    DeviceId = _deviceIdentity.GetOrCreate(),
                };

            if (account.IsSystem)
            {
                throw new InvalidOperationException(UiText.Get("T848"));
            }

            await _repository.UpsertAccountAsync(session.UserId, account);
            ClearAccountInputs();
            _editingAccount = null;
            OnPropertyChanged(nameof(AccountFormTitle));
            OnPropertyChanged(nameof(AccountSaveButtonText));
            StatusMessage = UiText.Get("T849");
            await ReloadCoreAsync();
        });
    }

    public void BeginEditAccount(AccountItemViewModel item)
    {
        if (item.IsSystem) return;
        _editingAccount = item.Model;
        AccountCodeInput = item.Model.Code;
        AccountNameInput = item.Model.Name;
        SelectedAccountType = AccountTypes.FirstOrDefault(type => type.Type == item.Model.Type) ?? AccountTypes[0];
        OnPropertyChanged(nameof(AccountFormTitle));
        OnPropertyChanged(nameof(AccountSaveButtonText));
    }

    public async Task DeleteAccountAsync(AccountItemViewModel item)
    {
        if (item.IsSystem)
        {
            throw new InvalidOperationException(UiText.Get("T848"));
        }

        var session = RequireSession();
        await RunBusyAsync(async () =>
        {
            await _repository.DeleteAccountAsync(session.UserId, item.AccountId);
            if (_editingAccount?.AccountId == item.AccountId)
            {
                _editingAccount = null;
                ClearAccountInputs();
                OnPropertyChanged(nameof(AccountFormTitle));
                OnPropertyChanged(nameof(AccountSaveButtonText));
            }
            StatusMessage = UiText.Get("T858");
            await ReloadCoreAsync();
        });
    }

    private void ClearAccountInputs()
    {
        AccountCodeInput = string.Empty;
        AccountNameInput = string.Empty;
        SelectedAccountType = AccountTypes[0];
    }

    public async Task SaveJournalEntryAsync()
    {
        var session = RequireSession();
        if (!Money.TryParse(JournalAmountInput, out var amount) || amount <= 0)
        {
            throw new InvalidOperationException(UiText.Get("T246"));
        }

        if (SelectedDebitAccount is null || SelectedCreditAccount is null)
        {
            throw new InvalidOperationException(UiText.Get("T247"));
        }

        if (SelectedDebitAccount.AccountId == SelectedCreditAccount.AccountId)
        {
            throw new InvalidOperationException(UiText.Get("T248"));
        }

        if (string.IsNullOrWhiteSpace(JournalDescription))
        {
            throw new InvalidOperationException(UiText.Get("T249"));
        }

                await RunBusyAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            var debitLineId = _editingJournalEntry?.Lines.FirstOrDefault(line => line.DebitMinor > 0)?.LineId
                ?? "debit-" + Guid.NewGuid().ToString("N");
            var creditLineId = _editingJournalEntry?.Lines.FirstOrDefault(line => line.CreditMinor > 0)?.LineId
                ?? "credit-" + Guid.NewGuid().ToString("N");
            var lines = new[]
            {
                new JournalLine(debitLineId, SelectedDebitAccount.AccountId, DebitMinor: amount),
                new JournalLine(creditLineId, SelectedCreditAccount.AccountId, CreditMinor: amount),
            };
            var entry = _editingJournalEntry is null
                ? new JournalEntry(
                    $"journal-{Guid.NewGuid():N}",
                    session.UserId,
                    DateOnly.FromDateTime(JournalDate),
                    $"JV-{JournalDate:yyyyMMdd}-{Guid.NewGuid():N}"[..20],
                    JournalDescription.Trim(),
                    lines,
                    JournalEntryStatus.Posted,
                    CreatedAtUtc: now,
                    UpdatedAtUtc: now,
                    DeviceId: _deviceIdentity.GetOrCreate())
                : _editingJournalEntry with
                {
                    EntryDate = DateOnly.FromDateTime(JournalDate),
                    Description = JournalDescription.Trim(),
                    Lines = lines,
                    Status = JournalEntryStatus.Posted,
                    UpdatedAtUtc = now,
                    Version = checked(_editingJournalEntry.Version + 1),
                    DeviceId = _deviceIdentity.GetOrCreate(),
                };

            var accounts = await _repository.GetAccountsAsync(session.UserId);
            JournalEntryValidator.EnsurePostable(entry, accounts.Select(static account => account.AccountId).ToHashSet(StringComparer.Ordinal));
            await _repository.UpsertJournalEntryAsync(session.UserId, entry);
            ClearJournalInputs();
            _editingJournalEntry = null;
            OnPropertyChanged(nameof(JournalFormTitle));
            OnPropertyChanged(nameof(JournalSaveButtonText));
            await ReloadCoreAsync();
            StatusMessage = UiText.Get("T242");
        });

    }

    public void BeginEditJournalEntry(JournalEntryItemViewModel item)
    {
        if (!item.IsEditable) return;
        _editingJournalEntry = item.Model;
        JournalDate = item.Model.EntryDate.ToDateTime(TimeOnly.MinValue);
        JournalDescription = item.Model.Description;
        var debit = item.Model.Lines.FirstOrDefault(line => line.DebitMinor > 0);
        var credit = item.Model.Lines.FirstOrDefault(line => line.CreditMinor > 0);
        SelectedDebitAccount = Accounts.FirstOrDefault(account => account.AccountId == debit?.AccountId);
        SelectedCreditAccount = Accounts.FirstOrDefault(account => account.AccountId == credit?.AccountId);
        JournalAmountInput = Money.Format(debit?.DebitMinor ?? credit?.CreditMinor ?? 0);
        OnPropertyChanged(nameof(JournalFormTitle));
        OnPropertyChanged(nameof(JournalSaveButtonText));
    }

    public async Task DeleteJournalEntryAsync(JournalEntryItemViewModel item)
    {
        if (!item.IsEditable)
        {
            throw new InvalidOperationException(UiText.Get("T850"));
        }

        var session = RequireSession();
        await RunBusyAsync(async () =>
        {
            await _repository.DeleteJournalEntryAsync(session.UserId, item.EntryId);
            if (_editingJournalEntry?.EntryId == item.EntryId)
            {
                _editingJournalEntry = null;
                ClearJournalInputs();
                OnPropertyChanged(nameof(JournalFormTitle));
                OnPropertyChanged(nameof(JournalSaveButtonText));
            }
            StatusMessage = UiText.Get("T851");
            await ReloadCoreAsync();
        });
    }

    private void ClearJournalInputs()
    {
        JournalAmountInput = string.Empty;
        JournalDescription = string.Empty;
        JournalDate = DateTime.Today;
    }

    public async Task<int> MigrateLegacyEntriesAsync()
    {
        var session = RequireSession();
        return await RunBusyAsync(async () =>
        {
            var count = await _repository.MigrateLegacyProfitEntriesAsync(session.UserId, _deviceIdentity.GetOrCreate());
            await ReloadCoreAsync();
            StatusMessage = count == 0
                ? UiText.Get("T243")
                : UiText.Format("T244", count);
            return count;
        });
    }

    private async Task ReloadCoreAsync()
    {
        var session = RequireSession();
        var accounts = await _repository.GetAccountsAsync(session.UserId);
        var entries = await _repository.GetJournalEntriesAsync(session.UserId);
        var trialBalance = TrialBalanceCalculator.Build(accounts, entries);

        Accounts.Clear();
        foreach (var account in accounts)
        {
            Accounts.Add(new AccountItemViewModel(account));
        }

        JournalEntries.Clear();
        foreach (var entry in entries)
        {
            JournalEntries.Add(new JournalEntryItemViewModel(entry));
        }

        SelectedDebitAccount = Accounts.FirstOrDefault(account => account.AccountId == SelectedDebitAccount?.AccountId)
            ?? Accounts.FirstOrDefault(account => account.Code == DefaultChartOfAccounts.CashCode);
        SelectedCreditAccount = Accounts.FirstOrDefault(account => account.AccountId == SelectedCreditAccount?.AccountId)
            ?? Accounts.FirstOrDefault(account => account.Code == DefaultChartOfAccounts.SalesRevenueCode);

        TrialBalanceRows.Clear();
        foreach (var row in trialBalance.Accounts.Where(HasActivity))
        {
            TrialBalanceRows.Add(new TrialBalanceItemViewModel(row));
        }

        TotalDebitText = Money.Format(trialBalance.TotalDebitMinor);
        TotalCreditText = Money.Format(trialBalance.TotalCreditMinor);
        BalanceStatusText = trialBalance.IsBalanced ? UiText.Get("T200") : UiText.Get("T199");
        PostedEntriesText = entries.Count(static entry => entry.Status == JournalEntryStatus.Posted).ToString();
        OnPropertyChanged(nameof(TotalDebitText));
        OnPropertyChanged(nameof(TotalCreditText));
        OnPropertyChanged(nameof(BalanceStatusText));
        OnPropertyChanged(nameof(PostedEntriesText));
        OnPropertyChanged(nameof(PostedEntriesSummaryText));
    }

    private static bool HasActivity(AccountBalance balance) =>
        balance.TotalDebitMinor != 0 || balance.TotalCreditMinor != 0;

    private AuthSession RequireSession() =>
        _session ?? throw new InvalidOperationException(UiText.Get("T245"));

    private async Task RunBusyAsync(Func<Task> action)
    {
        await _gate.WaitAsync();
        try
        {
            IsBusy = true;
            await action();
        }
        finally
        {
            IsBusy = false;
            _gate.Release();
        }
    }

    private async Task<T> RunBusyAsync<T>(Func<Task<T>> action)
    {
        await _gate.WaitAsync();
        try
        {
            IsBusy = true;
            return await action();
        }
        finally
        {
            IsBusy = false;
            _gate.Release();
        }
    }
}

public sealed class AccountItemViewModel(Account account)
{
    public Account Model => account;
    public string AccountId => account.AccountId;
    public string Code => account.Code;
    public string Name => account.Name;
    public AccountType Type => account.Type;
    public bool IsSystem => account.IsSystem;
    public bool IsUserManaged => !account.IsSystem;
    public string DisplayText => $"{account.Code} — {account.Name}";
    public string TypeText => account.Type switch
    {
        AccountType.Asset => UiText.Get("T250"),
        AccountType.Liability => UiText.Get("T251"),
        AccountType.Equity => UiText.Get("T252"),
        AccountType.Revenue => UiText.Get("T253"),
        AccountType.Expense => UiText.Get("T254"),
        _ => UiText.Get("T255")
    };
}

public sealed class JournalEntryItemViewModel(JournalEntry entry)
{
    public JournalEntry Model => entry;
    public string EntryId => entry.EntryId;
    public string EntryNumber => entry.EntryNumber;
    public string DateText => entry.EntryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    public string Description => entry.Description;
    public string DebitText => Money.Format(entry.TotalDebitMinor);
    public string CreditText => Money.Format(entry.TotalCreditMinor);
    public bool IsEditable => string.IsNullOrWhiteSpace(entry.Reference);
    public string SourceText => IsEditable ? UiText.Get("T852") : UiText.Get("T853");
}

public sealed record AccountTypeOption(AccountType Type)
{
    public string Label => Type switch
    {
        AccountType.Asset => UiText.Get("T250"),
        AccountType.Liability => UiText.Get("T251"),
        AccountType.Equity => UiText.Get("T252"),
        AccountType.Revenue => UiText.Get("T253"),
        AccountType.Expense => UiText.Get("T254"),
        _ => UiText.Get("T255"),
    };
}

public sealed class TrialBalanceItemViewModel(AccountBalance balance)
{
    public string Code => balance.Account.Code;
    public string Name => balance.Account.Name;
    public string DebitText => Money.Format(balance.DebitBalanceMinor);
    public string CreditText => Money.Format(balance.CreditBalanceMinor);
    public string BalanceText => Money.Format(Math.Abs(balance.NetMinor));
    public string NatureText => balance.NetMinor >= 0 ? UiText.Get("T306") : UiText.Get("T307");
    public string BalanceSummaryText => UiText.Format("T256", BalanceText);
    public string DebitSummaryText => UiText.Format("T257", DebitText);
    public string CreditSummaryText => UiText.Format("T258", CreditText);
}
