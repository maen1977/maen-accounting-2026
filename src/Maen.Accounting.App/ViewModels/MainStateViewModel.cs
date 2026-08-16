using System.Collections.ObjectModel;
using Maen.Accounting.App;
using Maen.Accounting.App.Data;
using Maen.Accounting.App.Infrastructure;
using Maen.Accounting.App.Services;
using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.ViewModels;

public sealed class MainStateViewModel : ObservableObject
{
    private enum ReportScope
    {
        Month,
        Quarter,
        Year
    }

    private readonly ProfitEntryRepository _repository;
    private readonly AccountingRepository _accountingRepository;
    private readonly BusinessRepository _businessRepository;
    private readonly BackupService _backupService;
    private readonly FirestoreSyncService _syncService;
    private readonly BusinessFirestoreSyncService _businessSyncService;
    private readonly DeviceIdentityService _deviceIdentity;
    private readonly AuthSessionStore _sessionStore;
    private readonly AppPreferencesService _preferences;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PersonalLedgerSummary _ledgerSummary = PersonalLedgerSummaryCalculator.Summarize(
        Array.Empty<ProfitEntry>(),
        DateOnly.FromDateTime(DateTime.Today));

    private AuthSession? _session;
    private ProfitEntry? _editingEntry;
    private DateTime _entryDate = DateTime.Today;
    private string _salesInput = "0";
    private string _costInput = "0";
    private string _expensesInput = "0";
    private string _notesInput = string.Empty;
    private DateTime _reportMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private ReportScope _reportScope = ReportScope.Month;
    private string _searchText = string.Empty;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _syncStatus = UiText.Get("T130");
    private string _backupStatus = UiText.Get("T131");
    private string _amountInput = string.Empty;
    private string _selectedMovementType = UiText.Get("T332");
    private string _selectedDirection = UiText.Get("T151");
    private string _categoryInput = string.Empty;
    private string _walletInput = string.Empty;
    private string _counterpartyInput = string.Empty;
    private string _monthlyBudgetInput = Preferences.Default.Get("maen_personal_monthly_budget_v1", string.Empty);
    private BusinessSyncResult? _lastBusinessSyncResult;

    public MainStateViewModel(
        ProfitEntryRepository repository,
        AccountingRepository accountingRepository,
        BusinessRepository businessRepository,
        BackupService backupService,
        FirestoreSyncService syncService,
        BusinessFirestoreSyncService businessSyncService,
        DeviceIdentityService deviceIdentity,
        AuthSessionStore sessionStore,
        AppPreferencesService preferences)
    {
        _repository = repository;
        _accountingRepository = accountingRepository;
        _businessRepository = businessRepository;
        _backupService = backupService;
        _syncService = syncService;
        _businessSyncService = businessSyncService;
        _deviceIdentity = deviceIdentity;
        _sessionStore = sessionStore;
        _preferences = preferences;
    }

    public bool IsBusinessExperience => _preferences.Experience == AccountExperience.Business;

    public ObservableCollection<ProfitEntryItemViewModel> Entries { get; } = [];
    public ObservableCollection<ProfitEntryItemViewModel> RecentEntries { get; } = [];
    public ObservableCollection<ProfitEntryItemViewModel> ReportEntries { get; } = [];
    public ObservableCollection<ReportDayItemViewModel> ReportDays { get; } = [];
    public ObservableCollection<PersonalCategoryItemViewModel> CurrentMonthCategorySpending { get; } = [];

    public event EventHandler? EntryEditorRequested;
    public event EventHandler? SignedOut;

    public string UserEmail => _session?.Email ?? string.Empty;
    public bool IsCloudAccount => _session is { IsLocal: false };
    public string AccountModeText => IsCloudAccount ? UiText.Get("T126") : UiText.Get("T127");

    public DateTime EntryDate { get => _entryDate; set => SetProperty(ref _entryDate, value); }
    public string SalesInput { get => _salesInput; set { if (SetProperty(ref _salesInput, value)) RefreshPreview(); } }
    public string CostInput { get => _costInput; set { if (SetProperty(ref _costInput, value)) RefreshPreview(); } }
    public string ExpensesInput { get => _expensesInput; set { if (SetProperty(ref _expensesInput, value)) RefreshPreview(); } }
    public string NotesInput { get => _notesInput; set => SetProperty(ref _notesInput, value); }
    public DateTime ReportMonth
    {
        get => _reportMonth;
        set
        {
            _reportScope = ReportScope.Month;
            var normalized = new DateTime(value.Year, value.Month, 1);
            SetProperty(ref _reportMonth, normalized);
            RebuildReport();
        }
    }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) RebuildReport(); } }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string SyncStatus { get => _syncStatus; private set => SetProperty(ref _syncStatus, value); }
    public string BackupStatus { get => _backupStatus; private set => SetProperty(ref _backupStatus, value); }
    public string SaveButtonText => _editingEntry is null ? UiText.Get("T128") : UiText.Get("T129");
    public IReadOnlyList<string> MovementTypes => new[]
    {
        UiText.Get("T332"), UiText.Get("T334"), UiText.Get("T333"),
        UiText.Get("T145"), UiText.Get("T144"),
        UiText.Get("T335"), UiText.Get("T147"), UiText.Get("T336"),
        UiText.Get("T364"), UiText.Get("T365"), UiText.Get("T148")
    };
    public IReadOnlyList<string> Directions => new[] { UiText.Get("T151"), UiText.Get("T150") };
    public string AmountInput { get => _amountInput; set { if (SetProperty(ref _amountInput, value)) UpdatePersonalAmounts(); } }
    public string SelectedMovementType { get => _selectedMovementType; set { if (SetProperty(ref _selectedMovementType, value)) UpdatePersonalAmounts(); } }
    public string SelectedDirection { get => _selectedDirection; set { if (SetProperty(ref _selectedDirection, value)) UpdatePersonalAmounts(); } }
    public string CategoryInput { get => _categoryInput; set => SetProperty(ref _categoryInput, value); }
    public string WalletInput { get => _walletInput; set => SetProperty(ref _walletInput, value); }
    public string CounterpartyInput { get => _counterpartyInput; set => SetProperty(ref _counterpartyInput, value); }
    public string DirectionSummaryText => IsTransferMovement() ? UiText.Get("T352") : SelectedDirection;
    public string MonthlyBudgetInput { get => _monthlyBudgetInput; set => SetProperty(ref _monthlyBudgetInput, value); }
    private long MonthlyBudgetMinor => Money.TryParse(_monthlyBudgetInput, out var amount) && amount >= 0 ? amount : 0;
    public string MonthlyBudgetText => MonthlyBudgetMinor <= 0 ? UiText.Get("T182") : Money.Format(MonthlyBudgetMinor);
    public double MonthlyBudgetProgress => MonthlyBudgetMinor <= 0
        ? 0
        : Math.Clamp((double)(_ledgerSummary.CurrentMonth.CostMinor + _ledgerSummary.CurrentMonth.ExpensesMinor) / MonthlyBudgetMinor, 0, 1);
    public string MonthlyBudgetPercentText => MonthlyBudgetMinor <= 0
        ? "0%"
        : $"{Math.Round((double)(_ledgerSummary.CurrentMonth.CostMinor + _ledgerSummary.CurrentMonth.ExpensesMinor) / MonthlyBudgetMinor * 100):0}%";
    public string MonthlyBudgetStatusText
    {
        get
        {
            if (MonthlyBudgetMinor <= 0) return UiText.Get("T182");
            var summary = _ledgerSummary.CurrentMonth;
            var progress = (double)(summary.CostMinor + summary.ExpensesMinor) / MonthlyBudgetMinor;
            if (progress >= 1) return UiText.Get("T183");
            if (progress >= 0.8) return UiText.Get("T184");
            return UiText.Get("T185");
        }
    }
    public string MonthlyBudgetStatusColor => MonthlyBudgetMinor <= 0
        ? "#64748B"
        : MonthlyBudgetProgress >= 1 ? "#C2413A" : MonthlyBudgetProgress >= 0.8 ? "#A16207" : "#137A53";

    public void SaveMonthlyBudget()
    {
        if (string.IsNullOrWhiteSpace(MonthlyBudgetInput))
        {
            Preferences.Default.Remove("maen_personal_monthly_budget_v1");
        }
        else if (!Money.TryParse(MonthlyBudgetInput, out var amount) || amount < 0)
        {
            throw new InvalidOperationException(UiText.Get("T187"));
        }
        else
        {
            Preferences.Default.Set("maen_personal_monthly_budget_v1", MonthlyBudgetInput.Trim());
        }

        RaiseBudgetProperties();
    }

    public string PreviewNetText
    {
        get
        {
            Money.TryParse(SalesInput, out var sales);
            Money.TryParse(CostInput, out var cost);
            Money.TryParse(ExpensesInput, out var expenses);
            return Money.Format(checked(sales - cost - expenses));
        }
    }

    public string ReportPeriodText => _reportScope switch
    {
        ReportScope.Quarter => UiText.Format("T330", ((ReportMonth.Month - 1) / 3) + 1, ReportMonth.Year),
        ReportScope.Year => ReportMonth.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => ReportMonth.ToString(
            "MMMM yyyy",
            UiText.Language == AppLanguage.English ? System.Globalization.CultureInfo.InvariantCulture : System.Globalization.CultureInfo.GetCultureInfo("ar-EG"))
    };
    public string ReportNetText => Money.Format(SummarizeReport().NetProfitMinor);
    public string ReportMarginText
    {
        get
        {
            var summary = SummarizeReport();
            if (summary.SalesMinor <= 0) return "0%";
            return $"{Math.Round((double)summary.NetProfitMinor / summary.SalesMinor * 100):0}%";
        }
    }
    public Color ReportNetColor => SummarizeReport().NetProfitMinor >= 0
        ? Color.FromArgb("#168A64")
        : Color.FromArgb("#D45B5B");

    public string CurrentMonthNetText => Money.Format(_ledgerSummary.CurrentMonth.NetProfitMinor);
    public string CurrentMonthGrossText => Money.Format(_ledgerSummary.CurrentMonth.GrossProfitMinor);
    public string CurrentMonthAverageNetText => Money.Format(_ledgerSummary.CurrentMonth.AverageNetMinor);
    public string CurrentMonthSalesText => Money.Format(_ledgerSummary.CurrentMonth.SalesMinor);
    public string CurrentMonthExpensesText
    {
        get
        {
            var summary = _ledgerSummary.CurrentMonth;
            return Money.Format(checked(summary.CostMinor + summary.ExpensesMinor));
        }
    }
    public string CurrentMonthIncomeText => Money.Format(_ledgerSummary.CurrentMonthIncomeMinor);
    public string CurrentMonthSalaryText => Money.Format(_ledgerSummary.CurrentMonthSalaryMinor);
    public string CurrentMonthFreelanceText => Money.Format(_ledgerSummary.CurrentMonthFreelanceMinor);
    public string CurrentMonthPurchasesText => Money.Format(_ledgerSummary.CurrentMonthPurchasesMinor);
    public string CurrentMonthWithdrawalsText => Money.Format(_ledgerSummary.CurrentMonthWithdrawalsMinor);
    public string CurrentMonthDebtPaymentsText => Money.Format(_ledgerSummary.CurrentMonthDebtPaymentsMinor);
    public string CurrentMonthAvailableBalanceText => Money.Format(_ledgerSummary.CurrentMonthAvailableBalanceMinor);
    public string BankBalanceText => Money.Format(_ledgerSummary.BankBalanceMinor);
    public string CurrentMonthBankDepositsText => Money.Format(_ledgerSummary.CurrentMonthBankDepositsMinor);
    public string CurrentMonthBankWithdrawalsText => Money.Format(_ledgerSummary.CurrentMonthBankWithdrawalsMinor);
    public string CurrentMonthEntryCountText => _ledgerSummary.CurrentMonth.EntriesCount.ToString();
    public double CurrentMonthSpendingProgress
    {
        get
        {
            var summary = _ledgerSummary.CurrentMonth;
            var moneyOut = checked(summary.CostMinor + summary.ExpensesMinor);
            if (summary.SalesMinor <= 0) return moneyOut > 0 ? 1 : 0;
            return Math.Clamp((double)moneyOut / summary.SalesMinor, 0, 1);
        }
    }
    public string CurrentMonthSpendingPercentText => $"{Math.Round(CurrentMonthSpendingProgress * 100):0}%";
    public string CurrentMonthHealthText
    {
        get
        {
            var summary = _ledgerSummary.CurrentMonth;
            if (summary.EntriesCount == 0) return UiText.Get("T161");
            if (summary.NetProfitMinor < 0) return UiText.Get("T159");
            return UiText.Get("T158");
        }
    }
    public string CurrentMonthHealthColor => _ledgerSummary.CurrentMonth.NetProfitMinor < 0 ? "#C2413A" : "#137A53";
    public string CurrentYearNetText => Money.Format(_ledgerSummary.CurrentYear.NetProfitMinor);
    public string TotalExpensesText => Money.Format(_ledgerSummary.Overall.ExpensesMinor);
    public string ReportGrossText => Money.Format(SummarizeReport().GrossProfitMinor);
    public string ReportAverageNetText => Money.Format(SummarizeReport().AverageNetMinor);
    public string ReportSalesText => Money.Format(SummarizeReport().SalesMinor);
    public string ReportExpensesText => Money.Format(SummarizeReport().ExpensesMinor);
    public string ReportCountText => SummarizeReport().EntriesCount.ToString();
    public string AccountingAccountsText { get; private set; } = "0";
    public string PostedJournalCountText { get; private set; } = "0";
    public string PostedInvoiceTotalText { get; private set; } = Money.Format(0);
    public string PaymentsTotalText { get; private set; } = Money.Format(0);
    public string TrialBalanceDebitText { get; private set; } = Money.Format(0);
    public string TrialBalanceCreditText { get; private set; } = Money.Format(0);
    public string TrialBalanceStatusText { get; private set; } = UiText.Get("T200");

    public void SelectCurrentQuarter()
    {
        var today = DateTime.Today;
        var quarterStartMonth = ((today.Month - 1) / 3) * 3 + 1;
        SetReportPeriod(new DateTime(today.Year, quarterStartMonth, 1), ReportScope.Quarter);
    }

    public void SelectCurrentYear()
    {
        SetReportPeriod(new DateTime(DateTime.Today.Year, 1, 1), ReportScope.Year);
    }

    public async Task RefreshReportBusinessSummaryAsync()
    {
        if (_session is not null)
        {
            await RefreshAccountingSummaryAsync(_session.UserId);
        }
    }

    public string BuildReportCsv()
    {
        if (ReportEntries.Count == 0)
        {
            throw new InvalidOperationException(UiText.Get("T388"));
        }

        return ReportCsvExporter.Build(
            ReportEntries.Select(static item => item.Model),
            new[]
            {
                UiText.Get("T378"), UiText.Get("T379"), UiText.Get("T380"), UiText.Get("T381"),
                UiText.Get("T382"), UiText.Get("T383"), UiText.Get("T384"), UiText.Get("T385"),
                UiText.Get("T386"), UiText.Get("T387")
            });
    }

    public string BuildReportShareText()
    {
        return string.Join(Environment.NewLine,
            UiText.Get("T202"),
            UiText.Format("T203", ReportPeriodText),
            UiText.Format("T204", ReportNetText),
            UiText.Format("T205", ReportSalesText),
            UiText.Format("T206", ReportExpensesText),
            UiText.Format("T207", ReportCountText),
            UiText.Format("T208", AccountingAccountsText),
            UiText.Format("T209", PostedJournalCountText),
            UiText.Format("T210", PostedInvoiceTotalText),
            UiText.Format("T211", PaymentsTotalText),
            UiText.Format("T197", TrialBalanceDebitText),
            UiText.Format("T198", TrialBalanceCreditText),
            TrialBalanceStatusText);
    }

    public async Task InitializeAsync(AuthSession session)
    {
        _session = session;
        OnPropertyChanged(nameof(UserEmail));
        OnPropertyChanged(nameof(IsCloudAccount));
        OnPropertyChanged(nameof(AccountModeText));
        await ReloadAsync();
        var info = await _backupService.ReadInfoAsync(session.UserId);
        SetBackupStatus(info);
    }

    public async Task ReloadAsync()
    {
        var session = RequireSession();
        var entries = await _repository.GetVisibleAsync(session.UserId);
        await RefreshAccountingSummaryAsync(session.UserId);
        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(new ProfitEntryItemViewModel(entry));
        }

        _ledgerSummary = PersonalLedgerSummaryCalculator.Summarize(entries, DateOnly.FromDateTime(DateTime.Today));
        RebuildRecent();
        RebuildCategorySpending();
        RebuildReport();
        RaiseSummaries();
    }

    public void BeginNewEntry()
    {
        _editingEntry = null;
        EntryDate = DateTime.Today;
        SalesInput = "0";
        CostInput = "0";
        ExpensesInput = "0";
        AmountInput = string.Empty;
        SelectedMovementType = UiText.Get("T332");
        SelectedDirection = UiText.Get("T151");
        CategoryInput = string.Empty;
        WalletInput = string.Empty;
        CounterpartyInput = string.Empty;
        NotesInput = string.Empty;
        OnPropertyChanged(nameof(SaveButtonText));
        EntryEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    public void BeginEdit(ProfitEntryItemViewModel item)
    {
        _editingEntry = item.Model;
        EntryDate = item.Model.EntryDate.ToDateTime(TimeOnly.MinValue);
        AmountInput = Money.ToDecimal(item.Model.EffectiveAmountMinor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        SelectedMovementType = DisplayMovementType(item.Model.MovementType);
        SelectedDirection = item.Model.IsIncome ? UiText.Get("T151") : UiText.Get("T150");
        CategoryInput = item.Model.Category;
        WalletInput = item.Model.Wallet == "main" ? string.Empty : item.Model.Wallet;
        CounterpartyInput = item.Model.Counterparty;
        SalesInput = Money.ToDecimal(item.Model.SalesMinor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        CostInput = Money.ToDecimal(item.Model.CostMinor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        ExpensesInput = Money.ToDecimal(item.Model.ExpensesMinor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        NotesInput = item.Model.Notes;
        OnPropertyChanged(nameof(SaveButtonText));
        EntryEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task SavePersonalCurrentAsync()
    {
        if (!Money.TryParse(AmountInput, out var amountMinor) || amountMinor <= 0)
        {
            throw new InvalidOperationException(UiText.Format("T139", UiText.Get("T149")));
        }

        var movementType = ResolveMovementType(SelectedMovementType);
        if (movementType is PersonalMovementTypes.BankDeposit or PersonalMovementTypes.BankWithdrawal)
        {
            WalletInput = "bank";
        }

        var notes = string.IsNullOrWhiteSpace(NotesInput)
            ? SelectedMovementType
            : $"{SelectedMovementType}: {NotesInput.Trim()}";
        if (movementType is PersonalMovementTypes.Transfer or PersonalMovementTypes.BankDeposit or PersonalMovementTypes.BankWithdrawal)
        {
            SalesInput = "0";
            CostInput = "0";
            ExpensesInput = "0";
        }
        else if (IsIncomeMovement(movementType) || (movementType == PersonalMovementTypes.Other && SelectedDirection == UiText.Get("T151")))
        {
            SalesInput = Money.Format(amountMinor);
            CostInput = "0";
            ExpensesInput = "0";
        }
        else if (movementType == PersonalMovementTypes.Purchase)
        {
            SalesInput = "0";
            CostInput = Money.Format(amountMinor);
            ExpensesInput = "0";
        }
        else
        {
            SalesInput = "0";
            CostInput = "0";
            ExpensesInput = Money.Format(amountMinor);
        }

        NotesInput = notes;
        await SaveCurrentAsync();
    }

    public async Task SaveCurrentAsync()
    {
        var session = RequireSession();
        var sales = ParseEntryAmount(SalesInput, UiText.Get("T005"));
        var cost = ParseEntryAmount(CostInput, UiText.Get("T059"));
        var expenses = ParseEntryAmount(ExpensesInput, UiText.Get("T047"));

        await RunBusyAsync(async () =>
        {
            var date = DateOnly.FromDateTime(EntryDate);
            var existingByDate = IsBusinessExperience
                ? await _repository.FindByDateAsync(session.UserId, date)
                : null;
            var source = _editingEntry ?? existingByDate;
            var now = DateTimeOffset.UtcNow;
            var entry = new ProfitEntry(
                source?.EntryId ?? Guid.NewGuid().ToString("N"),
                session.UserId,
                date,
                sales,
                cost,
                expenses,
                NotesInput.Trim(),
                false,
                source?.CreatedAtUtc ?? now,
                now,
                checked((source?.Version ?? 0) + 1),
                _deviceIdentity.GetOrCreate(),
                IsBusinessExperience ? source?.AmountMinor ?? 0 : ParsePersonalAmountOrFallback(sales, cost, expenses),
                IsBusinessExperience ? source?.MovementType ?? PersonalMovementTypes.Other : ResolveMovementType(SelectedMovementType),
                CategoryInput.Trim(),
                string.IsNullOrWhiteSpace(WalletInput) ? "main" : WalletInput.Trim(),
                CounterpartyInput.Trim());

            await _repository.UpsertAsync(session.UserId, entry);
            await WriteBackupAndUpdateAsync();
            await ReloadAsync();
            ResetEditor();
            StatusMessage = UiText.Get("T132");

            if (!session.IsLocal)
            {
                try
                {
                    var syncResult = await SyncInternalAsync();
                    var completedAt = syncResult.CompletedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                    StatusMessage = UiText.Format(
                        "T392",
                        syncResult.Uploaded,
                        syncResult.LocalWins,
                        syncResult.RemoteWins,
                        syncResult.TotalEntries,
                        completedAt);
                }
                catch (Exception exception)
                {
                    var detail = exception.Message.Trim();
                    StatusMessage = string.IsNullOrWhiteSpace(detail)
                        ? UiText.Get("T135")
                        : $"{UiText.Get("T135")} {detail}";
                    SyncStatus = StatusMessage;
                }
            }
        });
    }

    public async Task DeleteAsync(ProfitEntryItemViewModel item)
    {
        var session = RequireSession();
        await RunBusyAsync(async () =>
        {
            var deleted = item.Model.MarkDeleted(DateTimeOffset.UtcNow, _deviceIdentity.GetOrCreate());
            await _repository.UpsertAsync(session.UserId, deleted);
            await WriteBackupAndUpdateAsync();
            await ReloadAsync();
            StatusMessage = UiText.Get("T315");
            if (!session.IsLocal)
            {
                try
                {
                    var syncResult = await SyncInternalAsync();
                    var completedAt = syncResult.CompletedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                    StatusMessage = UiText.Format(
                        "T392",
                        syncResult.Uploaded,
                        syncResult.LocalWins,
                        syncResult.RemoteWins,
                        syncResult.TotalEntries,
                        completedAt);
                }
                catch (Exception exception)
                {
                    var detail = exception.Message.Trim();
                    StatusMessage = string.IsNullOrWhiteSpace(detail)
                        ? UiText.Get("T318")
                        : UiText.Format("T319", detail);
                    SyncStatus = StatusMessage;
                }
            }
        });
    }

    public async Task SyncAsync()
    {
        if (!IsCloudAccount)
        {
            throw new InvalidOperationException(UiText.Get("T126"));
        }

        await RunBusyAsync(async () =>
        {
            var syncResult = await SyncInternalAsync();
            StatusMessage = FormatSyncSummary(syncResult);
        });
    }

    public async Task<string> ExportAsync()
    {
        var session = RequireSession();
        var path = await _backupService.CreateExportCopyAsync(session);
        SetBackupStatus(await _backupService.ReadInfoAsync(session.UserId));
        return path;
    }

    public async Task<int> ImportAsync(string filePath)
    {
        var session = RequireSession();
        return await RunBusyAsync(async () =>
        {
            var count = await _backupService.ImportAsync(session, filePath);
            SetBackupStatus(await _backupService.ReadInfoAsync(session.UserId));
            await ReloadAsync();
            StatusMessage = UiText.Format("T320", count);
            return count;
        });
    }

    public async Task SignOutAsync()
    {
        await _sessionStore.ClearAsync();
        _session = null;
        Entries.Clear();
        RecentEntries.Clear();
        ReportEntries.Clear();
        SignedOut?.Invoke(this, EventArgs.Empty);
    }

    private async Task<SyncResult> SyncInternalAsync()
    {
        var result = await _syncService.SyncAsync();
        _lastBusinessSyncResult = string.Equals(_preferences.StorageScope, "business", StringComparison.Ordinal)
            ? await _businessSyncService.SyncAsync()
            : null;
        SyncStatus = FormatSyncSummary(result);
        await ReloadAsync();
        await WriteBackupAndUpdateAsync();
        return result;
    }

    private string FormatSyncSummary(SyncResult result)
    {
        var completedAt = result.CompletedAtUtc.ToLocalTime().ToString("g");
        var summary = UiText.Format(
            "T393",
            completedAt,
            result.Uploaded,
            result.LocalWins,
            result.RemoteWins,
            result.TotalEntries);

        if (_lastBusinessSyncResult is null)
        {
            return summary;
        }

        var business = _lastBusinessSyncResult;
        return $"{summary}{Environment.NewLine}{UiText.Format(
            "T394",
            business.ContactsTotal,
            business.InvoicesTotal,
            business.PaymentsTotal,
            business.Uploaded,
            business.LocalWins,
            business.RemoteWins,
            business.TotalRecords)}";
    }

    private async Task WriteBackupAndUpdateAsync()
    {
        var info = await _backupService.WriteAsync(RequireSession());
        SetBackupStatus(info);
    }

    private void SetBackupStatus(BackupInfo info)
    {
        BackupStatus = info.Exists
            ? UiText.Format("T138", info.UpdatedAtUtc?.ToLocalTime().ToString("g") ?? string.Empty, info.EntriesCount)
            : UiText.Get("T131");
    }

    private static long ParseEntryAmount(string? input, string fieldName)
    {
        // الحقل الفارغ في السجل اليومي يعني صفراً؛ أما الفقرات المدخلة فتُتحقق بدقة.
        if (string.IsNullOrWhiteSpace(input))
        {
            return 0;
        }

        if (!Money.TryParse(input, out var amount))
        {
            throw new InvalidOperationException(UiText.Format("T139", fieldName));
        }

        return amount;
    }

    private void ResetEditor()
    {
        _editingEntry = null;
        EntryDate = DateTime.Today;
        SalesInput = "0";
        CostInput = "0";
        ExpensesInput = "0";
        AmountInput = string.Empty;
        SelectedMovementType = UiText.Get("T332");
        SelectedDirection = UiText.Get("T151");
        CategoryInput = string.Empty;
        WalletInput = string.Empty;
        CounterpartyInput = string.Empty;
        NotesInput = string.Empty;
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(DirectionSummaryText));
    }

    private static bool IsIncomeMovement(string movementType) => movementType is
        PersonalMovementTypes.Salary or
        PersonalMovementTypes.Freelance or
        PersonalMovementTypes.Sale or
        PersonalMovementTypes.OtherIncome;

    private static string ResolveMovementType(string displayValue) => displayValue switch
    {
        var value when value == UiText.Get("T332") => PersonalMovementTypes.Salary,
        var value when value == UiText.Get("T333") => PersonalMovementTypes.Freelance,
        var value when value == UiText.Get("T334") => PersonalMovementTypes.OtherIncome,
        var value when value == UiText.Get("T143") => PersonalMovementTypes.Freelance,
        var value when value == UiText.Get("T144") => PersonalMovementTypes.Sale,
        var value when value == UiText.Get("T145") => PersonalMovementTypes.Purchase,
        var value when value == UiText.Get("T146") => PersonalMovementTypes.Expense,
        var value when value == UiText.Get("T335") => PersonalMovementTypes.Withdrawal,
        var value when value == UiText.Get("T147") => PersonalMovementTypes.Transfer,
        var value when value == UiText.Get("T336") => PersonalMovementTypes.DebtPayment,
        var value when value == UiText.Get("T364") => PersonalMovementTypes.BankDeposit,
        var value when value == UiText.Get("T365") => PersonalMovementTypes.BankWithdrawal,
        _ => PersonalMovementTypes.Other
    };

    private static string DisplayMovementType(string movementType) => movementType switch
    {
        PersonalMovementTypes.Salary => UiText.Get("T332"),
        PersonalMovementTypes.Freelance => UiText.Get("T333"),
        PersonalMovementTypes.OtherIncome => UiText.Get("T334"),
        PersonalMovementTypes.Sale => UiText.Get("T144"),
        PersonalMovementTypes.Purchase => UiText.Get("T145"),
        PersonalMovementTypes.Expense => UiText.Get("T145"),
        PersonalMovementTypes.Withdrawal => UiText.Get("T335"),
        PersonalMovementTypes.Transfer => UiText.Get("T147"),
        PersonalMovementTypes.DebtPayment => UiText.Get("T336"),
        PersonalMovementTypes.BankDeposit => UiText.Get("T364"),
        PersonalMovementTypes.BankWithdrawal => UiText.Get("T365"),
        _ => UiText.Get("T148")
    };

    private long ParsePersonalAmountOrFallback(long sales, long cost, long expenses)
    {
        return Money.TryParse(AmountInput, out var amount) && amount > 0
            ? amount
            : checked(sales + cost + expenses);
    }

    private bool IsTransferMovement() => ResolveMovementType(SelectedMovementType) == PersonalMovementTypes.Transfer;

    private static long GetBankImpactMinor(ProfitEntry entry)
    {
        if (!IsBankWallet(entry)) return 0;
        if (entry.MovementType == PersonalMovementTypes.BankDeposit || entry.IsIncome) return entry.EffectiveAmountMinor;
        if (entry.MovementType == PersonalMovementTypes.BankWithdrawal || entry.IsOutflow) return -entry.EffectiveAmountMinor;
        return 0;
    }

    private static bool IsBankWallet(ProfitEntry entry) =>
        string.Equals(entry.Wallet, "bank", StringComparison.OrdinalIgnoreCase);

    private void UpdatePersonalAmounts()
    {
        var movementType = ResolveMovementType(SelectedMovementType);
        if (string.IsNullOrWhiteSpace(AmountInput))
        {
            SalesInput = "0";
            CostInput = "0";
            ExpensesInput = "0";
        }
        else if (movementType is PersonalMovementTypes.Transfer or PersonalMovementTypes.BankDeposit or PersonalMovementTypes.BankWithdrawal)
        {
            SalesInput = "0";
            CostInput = "0";
            ExpensesInput = "0";
        }
        else if (IsIncomeMovement(movementType) || (movementType == PersonalMovementTypes.Other && SelectedDirection == UiText.Get("T151")))
        {
            SalesInput = AmountInput;
            CostInput = "0";
            ExpensesInput = "0";
        }
        else if (movementType == PersonalMovementTypes.Purchase)
        {
            SalesInput = "0";
            CostInput = AmountInput;
            ExpensesInput = "0";
        }
        else
        {
            SalesInput = "0";
            CostInput = "0";
            ExpensesInput = AmountInput;
        }

        OnPropertyChanged(nameof(DirectionSummaryText));
    }

    private void RebuildRecent()
    {
        RecentEntries.Clear();
        foreach (var entry in Entries.Take(5))
        {
            RecentEntries.Add(entry);
        }
    }

    private void RebuildCategorySpending()
    {
        CurrentMonthCategorySpending.Clear();
        var rank = 1;
        foreach (var category in _ledgerSummary.CurrentMonthCategorySpending)
        {
            CurrentMonthCategorySpending.Add(new PersonalCategoryItemViewModel(category, rank++));
        }
    }

    private void RebuildReport()
    {
        ReportEntries.Clear();
        ReportDays.Clear();
        var query = SearchText.Trim();
        var range = GetReportRange();
        var filtered = Entries.Where(item =>
                     item.Model.EntryDate >= range.FromDate &&
                     item.Model.EntryDate <= range.ToDate &&
                     (query.Length == 0 ||
                      item.DateText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                      item.Model.Notes.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var item in filtered)
        {
            ReportEntries.Add(item);
        }

        var daily = filtered
            .GroupBy(item => item.Model.EntryDate)
            .Select(group => new
            {
                Date = group.Key,
                SalesMinor = group.Sum(item => item.Model.SalesMinor),
                ExpensesMinor = group.Sum(item => item.Model.CostMinor + item.Model.ExpensesMinor),
                NetMinor = group.Sum(item => item.Model.NetProfitMinor)
            })
            .OrderBy(item => item.Date)
            .ToList();
        var maxAbsNet = daily.Count == 0 ? 1 : Math.Max(1, daily.Max(item => Math.Abs(item.NetMinor)));
        foreach (var item in daily)
        {
            ReportDays.Add(new ReportDayItemViewModel(
                item.Date,
                item.SalesMinor,
                item.ExpensesMinor,
                item.NetMinor,
                Math.Clamp((double)Math.Abs(item.NetMinor) / maxAbsNet, 0.04, 1)));
        }

        RaiseReportSummary();
    }

    private void SetReportPeriod(DateTime start, ReportScope scope)
    {
        _reportScope = scope;
        _reportMonth = new DateTime(start.Year, start.Month, 1);
        OnPropertyChanged(nameof(ReportMonth));
        RebuildReport();
    }

    private (DateOnly FromDate, DateOnly ToDate) GetReportRange()
    {
        var from = DateOnly.FromDateTime(ReportMonth);
        return _reportScope switch
        {
            ReportScope.Quarter => (from, from.AddMonths(3).AddDays(-1)),
            ReportScope.Year => (new DateOnly(ReportMonth.Year, 1, 1), new DateOnly(ReportMonth.Year, 12, 31)),
            _ => (from, from.AddMonths(1).AddDays(-1))
        };
    }

    private IEnumerable<ProfitEntry> Models() => Entries.Select(static item => item.Model);
    private IEnumerable<ProfitEntry> CurrentMonthModels()
    {
        var now = DateTime.Today;
        return Models().Where(entry => !entry.IsDeleted && entry.EntryDate.Year == now.Year && entry.EntryDate.Month == now.Month);
    }
    private FinancialSummary SummarizeCurrentMonth()
    {
        var now = DateTime.Today;
        return ProfitCalculator.Summarize(Models().Where(entry => entry.EntryDate.Year == now.Year && entry.EntryDate.Month == now.Month));
    }
    private FinancialSummary SummarizeCurrentYear()
    {
        var now = DateTime.Today;
        return ProfitCalculator.Summarize(Models().Where(entry => entry.EntryDate.Year == now.Year));
    }
    private FinancialSummary SummarizeReport() => ProfitCalculator.Summarize(ReportEntries.Select(static item => item.Model));

    private void RaiseSummaries()
    {
        OnPropertyChanged(nameof(CurrentMonthNetText));
        OnPropertyChanged(nameof(CurrentMonthGrossText));
        OnPropertyChanged(nameof(CurrentMonthAverageNetText));
        OnPropertyChanged(nameof(CurrentMonthSalesText));
        OnPropertyChanged(nameof(CurrentMonthExpensesText));
        OnPropertyChanged(nameof(CurrentMonthIncomeText));
        OnPropertyChanged(nameof(CurrentMonthSalaryText));
        OnPropertyChanged(nameof(CurrentMonthFreelanceText));
        OnPropertyChanged(nameof(CurrentMonthPurchasesText));
        OnPropertyChanged(nameof(CurrentMonthWithdrawalsText));
        OnPropertyChanged(nameof(CurrentMonthDebtPaymentsText));
        OnPropertyChanged(nameof(CurrentMonthAvailableBalanceText));
        OnPropertyChanged(nameof(BankBalanceText));
        OnPropertyChanged(nameof(CurrentMonthBankDepositsText));
        OnPropertyChanged(nameof(CurrentMonthBankWithdrawalsText));
        OnPropertyChanged(nameof(CurrentMonthEntryCountText));
        OnPropertyChanged(nameof(CurrentMonthSpendingProgress));
        OnPropertyChanged(nameof(CurrentMonthSpendingPercentText));
        OnPropertyChanged(nameof(CurrentMonthHealthText));
        OnPropertyChanged(nameof(CurrentMonthHealthColor));
        OnPropertyChanged(nameof(CurrentYearNetText));
        OnPropertyChanged(nameof(TotalExpensesText));
        RaiseBudgetProperties();
        RaiseReportSummary();
    }

    private void RaiseBudgetProperties()
    {
        OnPropertyChanged(nameof(MonthlyBudgetText));
        OnPropertyChanged(nameof(MonthlyBudgetProgress));
        OnPropertyChanged(nameof(MonthlyBudgetPercentText));
        OnPropertyChanged(nameof(MonthlyBudgetStatusText));
        OnPropertyChanged(nameof(MonthlyBudgetStatusColor));
    }

    private void RaiseReportSummary()
    {
        OnPropertyChanged(nameof(ReportNetText));
        OnPropertyChanged(nameof(ReportGrossText));
        OnPropertyChanged(nameof(ReportAverageNetText));
        OnPropertyChanged(nameof(ReportSalesText));
        OnPropertyChanged(nameof(ReportExpensesText));
        OnPropertyChanged(nameof(ReportCountText));
        OnPropertyChanged(nameof(ReportPeriodText));
        OnPropertyChanged(nameof(ReportMarginText));
        OnPropertyChanged(nameof(ReportNetColor));
    }

    private void RefreshPreview() => OnPropertyChanged(nameof(PreviewNetText));

    private async Task RefreshAccountingSummaryAsync(string userId)
    {
        var range = GetReportRange();
        var fromDate = range.FromDate;
        var toDate = range.ToDate;
        var accounts = await _accountingRepository.GetAccountsAsync(userId);
        var journalEntries = await _accountingRepository.GetJournalEntriesAsync(userId, fromDate, toDate);
        var invoices = (await _businessRepository.GetInvoicesAsync(userId))
            .Where(invoice => invoice.IssueDate >= fromDate && invoice.IssueDate <= toDate)
            .ToArray();
        var payments = (await _businessRepository.GetPaymentsAsync(userId))
            .Where(payment => payment.PaymentDate >= fromDate && payment.PaymentDate <= toDate)
            .ToArray();
        var trialBalance = await _accountingRepository.GetTrialBalanceAsync(userId, fromDate, toDate);

        AccountingAccountsText = accounts.Count.ToString();
        PostedJournalCountText = journalEntries.Count(static entry => entry.Status == JournalEntryStatus.Posted).ToString();
        PostedInvoiceTotalText = Money.Format(invoices.Where(static invoice => invoice.Status == InvoiceStatus.Posted).Sum(static invoice => invoice.TotalMinor));
        PaymentsTotalText = Money.Format(payments.Sum(static payment => payment.AmountMinor));
        TrialBalanceDebitText = Money.Format(trialBalance.TotalDebitMinor);
        TrialBalanceCreditText = Money.Format(trialBalance.TotalCreditMinor);
        TrialBalanceStatusText = trialBalance.IsBalanced ? UiText.Get("T200") : UiText.Get("T201");

        OnPropertyChanged(nameof(AccountingAccountsText));
        OnPropertyChanged(nameof(PostedJournalCountText));
        OnPropertyChanged(nameof(PostedInvoiceTotalText));
        OnPropertyChanged(nameof(PaymentsTotalText));
        OnPropertyChanged(nameof(TrialBalanceDebitText));
        OnPropertyChanged(nameof(TrialBalanceCreditText));
        OnPropertyChanged(nameof(TrialBalanceStatusText));
    }

    private AuthSession RequireSession() =>
        _session ?? throw new InvalidOperationException(UiText.Get("T140"));

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
