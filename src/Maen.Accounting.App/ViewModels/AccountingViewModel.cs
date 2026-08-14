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
    public ObservableCollection<TrialBalanceItemViewModel> TrialBalanceRows { get; } = [];

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

    public async Task InitializeAsync(AuthSession session)
    {
        _session = session;
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        await RunBusyAsync(ReloadCoreAsync);
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
            var entry = new JournalEntry(
                $"journal-{Guid.NewGuid():N}",
                session.UserId,
                DateOnly.FromDateTime(JournalDate),
                $"JV-{JournalDate:yyyyMMdd}-{Guid.NewGuid():N}"[..20],
                JournalDescription.Trim(),
                [
                    new JournalLine("debit-" + Guid.NewGuid().ToString("N"), SelectedDebitAccount.AccountId, DebitMinor: amount),
                    new JournalLine("credit-" + Guid.NewGuid().ToString("N"), SelectedCreditAccount.AccountId, CreditMinor: amount)
                ],
                JournalEntryStatus.Posted,
                CreatedAtUtc: now,
                UpdatedAtUtc: now,
                DeviceId: _deviceIdentity.GetOrCreate());

            var accounts = await _repository.GetAccountsAsync(session.UserId);
            JournalEntryValidator.EnsurePostable(entry, accounts.Select(static account => account.AccountId).ToHashSet(StringComparer.Ordinal));
            await _repository.UpsertJournalEntryAsync(session.UserId, entry);
            await ReloadCoreAsync();
            JournalAmountInput = string.Empty;
            JournalDescription = string.Empty;
            StatusMessage = UiText.Get("T242");
        });
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

        SelectedDebitAccount ??= Accounts.FirstOrDefault(account => account.Code == DefaultChartOfAccounts.CashCode);
        SelectedCreditAccount ??= Accounts.FirstOrDefault(account => account.Code == DefaultChartOfAccounts.SalesRevenueCode);

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
    public string AccountId => account.AccountId;
    public string Code => account.Code;
    public string Name => account.Name;
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
