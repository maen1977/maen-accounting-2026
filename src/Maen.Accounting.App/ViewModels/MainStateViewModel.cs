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
    private readonly PersonalFirestoreSyncService _personalEntitySyncService;
    private readonly PlanningRepository _planningRepository;
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
    private string _attachmentBase64 = string.Empty;
    private DateTime _reportMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private ReportScope _reportScope = ReportScope.Month;
    private string _searchText = string.Empty;
    private string _movementSearchText = string.Empty;
    private string _movementSearchQueryText = string.Empty;
    private string _movementCategoryFilter = string.Empty;
    private DateTime _movementFilterFromDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime _movementFilterToDate = DateTime.Today;
    private readonly System.Collections.ObjectModel.ObservableCollection<string> _movementFilterCategories = new();
    private readonly System.Collections.ObjectModel.ObservableCollection<MovementSearchItemViewModel> _movementSearchResults = new();
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
    private string _currencyInput = string.Empty;
    private string _monthlyBudgetInput = Preferences.Default.Get("maen_personal_monthly_budget_v1", string.Empty);
    private BusinessSyncResult? _lastBusinessSyncResult;
    private PersonalEntitySyncResult? _lastPersonalEntitySyncResult;
    private PersonalPlanProgress? _planProgress;
    private IReadOnlyList<ObligationEvent> _upcomingObligations = Array.Empty<ObligationEvent>();
    private readonly System.Collections.ObjectModel.ObservableCollection<CategoryRegistryItem> _categoryRegistryItems = new();
    private PersonalAnnualReport? _annualReport;
    private double _yearOverYearChangePercent;
    private CategoryReport? _categoryReport;
    private SavingsTrend? _savingsTrend;
    private BusinessFinancialSummary? _businessFinancialPosition;
    private readonly System.Collections.ObjectModel.ObservableCollection<CategoryBreakdownItem> _categoryBreakdownItems = new();
    private readonly System.Collections.ObjectModel.ObservableCollection<SavingsTrendPointItem> _savingsTrendPoints = new();
    private readonly System.Collections.ObjectModel.ObservableCollection<OverdueInvoiceItem> _overdueSalesInvoices = new();
    private readonly System.Collections.ObjectModel.ObservableCollection<OverdueInvoiceItem> _overduePurchasesInvoices = new();
        private bool _isOverdueExpanded;
    private DebtAgingResult? _debtAging;
    private IReadOnlyList<DataQualityWarning> _personalQualityWarnings = Array.Empty<DataQualityWarning>();
    private SavingsGoalRow[] _savingsGoals = [];
    private FinancialHealthSummary? _financialHealth;
    private CashForecast? _cashForecast;
    private CategoryAnalytics[] _categoryAnalytics = [];
    private MonthlyAmount[] _monthlyAmounts = [];
    private AuditSummary? _auditSummary;
    private RecurringMovementRow[] _recurringMovements = [];
    private string _displayCurrency = "JOD";
    private BudgetStatus? _currentBudgetStatus;
    private bool _possibleDuplicateDetected;

    public string ReceivablesAgingTotalText => Money.Format(_debtAging?.Receivables.TotalMinor ?? 0);
    public string ReceivablesAgingCurrentText => Money.Format(_debtAging?.Receivables.CurrentMinor ?? 0);
    public string ReceivablesAgingOverNinetyText => Money.Format(_debtAging?.Receivables.DaysOverNinetyMinor ?? 0);
    public string PayablesAgingTotalText => Money.Format(_debtAging?.Payables.TotalMinor ?? 0);
    public string DataQualityStatusText =>
        _personalQualityWarnings.Count == 0 ? UiText.Get("T538") : UiText.Format("T543", _personalQualityWarnings.Count);
    public string DataQualityStatusColor =>
        _personalQualityWarnings.Count == 0 ? "#137A53" : "#B45309";
    public bool HasDataQualityWarnings => _personalQualityWarnings.Count > 0;
    public bool HasAgingExposure => (_debtAging?.Receivables.TotalMinor ?? 0) > 0;
    public IReadOnlyList<SavingsGoalRow> SavingsGoals => _savingsGoals;
    public FinancialHealthSummary? FinancialHealth => _financialHealth;
    public CashForecast? CashForecast => _cashForecast;
    public bool HasGoals => _savingsGoals.Length > 0;

    public bool HasCategoryAnalytics => _categoryAnalytics.Length > 0;
    public IReadOnlyList<CategoryAnalytics> CategoryAnalytics => _categoryAnalytics;
    public CategoryAnalytics? TopCategory => _categoryAnalytics.Length > 0 ? _categoryAnalytics[0] : null;
    public string TopCategoryText => TopCategory is null ? UiText.Get("T729") : $"{TopCategory.Category} — {TopCategory.AmountText} ({TopCategory.SharePercent:0.#}%)";
    public IReadOnlyList<MonthlyAmount> MonthlyAmounts => _monthlyAmounts;
    public bool HasMonthlyAmounts => _monthlyAmounts.Length > 0;
    public MonthlyAmount? PeakMonth => _monthlyAmounts
        .OrderByDescending(item => item.SpendingMinor)
        .FirstOrDefault();
    public MonthlyAmount? LowMonth => _monthlyAmounts
        .Where(static item => item.SpendingMinor > 0)
        .OrderBy(item => item.SpendingMinor)
        .FirstOrDefault();

    public AuditSummary? AuditSummary => _auditSummary;
    public bool HasAuditSummary => _auditSummary is not null;
    public string AuditVerdictText => _auditSummary?.IsHealthy == true ? UiText.Get("T734") : UiText.Get("T735");
    public string AuditVerdictColor => _auditSummary?.IsHealthy == true ? "#137A53" : "#2F7DB8";
    public string AuditedRecordsText => UiText.Format("T731") + $" {_auditSummary?.ActiveRecordCount ?? 0}";
    public string PeakMonthLabel => PeakMonth is null ? string.Empty : FormatMonth(PeakMonth.Month);
    public string PeakMonthAmountText => PeakMonth is null ? string.Empty : Money.Format(PeakMonth.SpendingMinor);
    public string LowMonthLabel => LowMonth is null ? string.Empty : FormatMonth(LowMonth.Month);
    public string LowMonthAmountText => LowMonth is null ? string.Empty : Money.Format(LowMonth.SpendingMinor);
    private static string FormatMonth(DateOnly month) => month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

    public IReadOnlyList<RecurringMovementRow> RecurringMovements => _recurringMovements;
    public bool HasRecurringMovements => _recurringMovements.Length > 0;

    public string DisplayCurrency
    {
        get => _displayCurrency;
        set
        {
            if (SetProperty(ref _displayCurrency, value ?? _displayCurrency))
            {
                Preferences.Default.Set("maen_display_currency_v1", _displayCurrency);
                RaiseCurrencyProperties();
                RaiseReportSummary();
            }
        }
    }

    public BudgetStatus? CurrentBudgetStatus => _currentBudgetStatus;
    public bool HasBudgetStatus => _currentBudgetStatus is not null;
    public string BudgetStatusAlertText => _currentBudgetStatus?.Alert switch
    {
        BudgetAlert.Exceeded => UiText.Get("T818"),
        BudgetAlert.Warning => UiText.Get("T819"),
        _ => UiText.Get("T158")
    };
    public string BudgetStatusAlertColor => _currentBudgetStatus?.Alert switch
    {
        BudgetAlert.Exceeded => "#C2413A",
        BudgetAlert.Warning => "#A16207",
        _ => "#137A53"
    };
    public string BudgetRemainingText => _currentBudgetStatus is null
        ? string.Empty
        : Money.Format(Math.Max(0, _currentBudgetStatus.RemainingMinor));
    public string BudgetProjectedText => _currentBudgetStatus is null
        ? string.Empty
        : Money.Format(_currentBudgetStatus.ProjectedMonthTotalMinor);
    public bool PossibleDuplicateDetected
    {
        get => _possibleDuplicateDetected;
        set => SetProperty(ref _possibleDuplicateDetected, value);
    }
    public string HealthScoreText => _financialHealth?.Score.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";
    public string HealthLevelText => _financialHealth?.ScoreLevel switch
    {
        "Excellent" => UiText.Get("T620"),
        "Good" => UiText.Get("T621"),
        "Fair" => UiText.Get("T622"),
        "Weak" => UiText.Get("T623"),
        "Critical" => UiText.Get("T624"),
        _ => UiText.Get("T622")
    };
    public string HealthScoreColor => _financialHealth?.Score switch
    {
        >= 80 => "#137A53",
        >= 60 => "#2F7DB8",
        >= 40 => "#A16207",
        >= 20 => "#C2413A",
        _ => "#8E2B2B"
    };
    public string SavingsRateText => _financialHealth is null ? UiText.Get("T182") : $"{_financialHealth.SavingsRatePercent:0.#}%";
    public string FixedCostRatioText => _financialHealth is null ? UiText.Get("T182") : $"{_financialHealth.FixedCostRatioPercent:0.#}%";
    public string RunwayText => _financialHealth?.RunwayMonths switch
    {
        null or 0 => UiText.Get("T625"),
        > 0 and < 1 => $"{UiText.Get("T626")} {_financialHealth.RunwayMonths:0.#}",
        _ => $"{UiText.Get("T627")} {Math.Round(_financialHealth.RunwayMonths):0}"
    };
    public IReadOnlyList<string> HealthFlags => _financialHealth?.Flags
        .Select(flag => TranslateHealthFlag(flag.Code))
        .ToArray() ?? [];
    public bool HasHealthFlags => HealthFlags.Count > 0;

    private static string TranslateHealthFlag(string code) => code switch
    {
        "LowSavingsRate" => UiText.Get("T646"),
        "HighFixedCosts" => UiText.Get("T647"),
        "ShortRunway" => UiText.Get("T648"),
        "SpendingWithoutIncome" => UiText.Get("T649"),
        _ => code,
    };
    public bool ForecastHasWorst => _cashForecast?.WorstSlice is not null;
    public string ForecastWorstText => _cashForecast?.WorstSlice is null ? string.Empty : Money.Format(_cashForecast.WorstSlice.NetMinor);
    public string ForecastNextMonthText => _cashForecast is null || _cashForecast.Slices.Count == 0 ? string.Empty : Money.Format(_cashForecast.Slices[0].NetMinor);
    public string ForecastNextMonthColor => _cashForecast is null || _cashForecast.Slices.Count == 0
        ? "#64748B" : _cashForecast.Slices[0].NetMinor >= 0 ? "#137A53" : "#C2413A";
    public bool ForecastHasSlices => _cashForecast is not null && _cashForecast.Slices.Count > 0;
    public IReadOnlyList<CashForecastSlice> ForecastSlices => _cashForecast?.Slices ?? [];

    public MainStateViewModel(
        ProfitEntryRepository repository,
        AccountingRepository accountingRepository,
        BusinessRepository businessRepository,
        BackupService backupService,
        FirestoreSyncService syncService,
        BusinessFirestoreSyncService businessSyncService,
        PersonalFirestoreSyncService personalEntitySyncService,
        PlanningRepository planningRepository,
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
        _personalEntitySyncService = personalEntitySyncService;
        _planningRepository = planningRepository;
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
    public string AttachmentBase64
    {
        get => _attachmentBase64;
        set
        {
            if (SetProperty(ref _attachmentBase64, value))
            {
                OnPropertyChanged(nameof(HasAttachment));
                OnPropertyChanged(nameof(AttachmentImage));
            }
        }
    }
    public bool HasAttachment => !string.IsNullOrEmpty(_attachmentBase64);
    public ImageSource? AttachmentImage
    {
        get
        {
            if (string.IsNullOrEmpty(_attachmentBase64))
            {
                return null;
            }

            try
            {
                var bytes = Convert.FromBase64String(_attachmentBase64);
                return ImageSource.FromStream(() => new MemoryStream(bytes));
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
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
    public string MovementSearchText { get => _movementSearchText; set { if (SetProperty(ref _movementSearchText, value)) ApplyMovementFilters(); } }
    public string MovementCategoryFilter { get => _movementCategoryFilter; set { if (SetProperty(ref _movementCategoryFilter, value)) ApplyMovementFilters(); } }
    public DateTime MovementFilterFromDate { get => _movementFilterFromDate; set { if (SetProperty(ref _movementFilterFromDate, value)) { ApplyMovementFilters(); OnPropertyChanged(nameof(HasMovementFilter)); } } }
    public DateTime MovementFilterToDate { get => _movementFilterToDate; set { if (SetProperty(ref _movementFilterToDate, value)) { ApplyMovementFilters(); OnPropertyChanged(nameof(HasMovementFilter)); } } }
    public System.Collections.ObjectModel.ObservableCollection<string> MovementFilterCategories => _movementFilterCategories;
    public System.Collections.ObjectModel.ObservableCollection<MovementSearchItemViewModel> MovementSearchResults => _movementSearchResults;
    public bool HasMovementSearchResults => _movementSearchResults.Count > 0;
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string SyncStatus { get => _syncStatus; private set => SetProperty(ref _syncStatus, value); }
    public string StartupCloudDataMessage { get; private set; } = string.Empty;
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
    public string CurrencyInput { get => _currencyInput; set => SetProperty(ref _currencyInput, value); }

    public IReadOnlyList<string> DisplayCurrencyOptions => new[] { "JOD", "SAR", "USD", "EUR", "KWD", "AED", "EGP", "IQD", "SYP", "GBP" };

    private string ResolveEntryCurrency(string userId)
    {
        var input = (CurrencyInput ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return CurrencyConverter.ResolveProfile(input) is not null ? input : DisplayCurrency;
    }
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
        if (!string.IsNullOrWhiteSpace(MonthlyBudgetInput)
            && (!Money.TryParse(MonthlyBudgetInput, out var amount) || amount < 0))
        {
            throw new InvalidOperationException(UiText.Get("T187"));
        }

        _ = SaveMonthlyBudgetAsync();
        RaiseBudgetProperties();
    }

    public string PreviewNetText
    {
        get
        {
            var sales = Money.TryParse(SalesInput, out var parsedSales) ? parsedSales : 0;
            var cost = Money.TryParse(CostInput, out var parsedCost) ? parsedCost : 0;
            var expenses = Money.TryParse(ExpensesInput, out var parsedExpenses) ? parsedExpenses : 0;
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
    public string CurrentMonthEntryCountText => _ledgerSummary.CurrentMonth.EntriesCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
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

    public bool HasAnnualReport => _annualReport is not null;
    public string AnnualIncomeText => Money.Format(_annualReport?.TotalIncomeMinor ?? 0);
    public string AnnualSpendingText => Money.Format(_annualReport?.TotalSpendingMinor ?? 0);
    public string AnnualNetText => Money.Format(_annualReport?.TotalNetMinor ?? 0);
    public string AnnualNetColor => (_annualReport?.TotalNetMinor ?? 0) >= 0 ? "#137A53" : "#C2413A";
    public string BestMonthText => FormatMonthLabel(_annualReport?.BestMonth);
    public string WorstMonthText => FormatMonthLabel(_annualReport?.WorstMonth);

    private string FormatMonthLabel(PersonalMonthlySnapshot? snapshot)
    {
        if (snapshot is null || snapshot.Month <= 0)
        {
            return UiText.Get("T168");
        }

        return $"{YearMonthName(snapshot.Year, snapshot.Month)} · {Money.Format(snapshot.NetMinor)}";
    }
    public string YearOverYearText => _annualReport is null
        ? UiText.Get("T483")
        : _yearOverYearChangePercent == 0
            ? UiText.Get("T351")
            : UiText.Format("T481", _yearOverYearChangePercent.ToString("+0;-0", System.Globalization.CultureInfo.InvariantCulture));
    public string YearOverYearColor => _yearOverYearChangePercent > 0 ? "#137A53" : _yearOverYearChangePercent < 0 ? "#C2413A" : "#64748B";

    public bool HasCategoryBreakdown => _categoryBreakdownItems.Count > 0;
    public System.Collections.ObjectModel.ObservableCollection<CategoryBreakdownItem> CategoryBreakdownItems => _categoryBreakdownItems;

    public bool HasSavingsTrend => _savingsTrendPoints.Count > 0;
    public string TrendStreakText => _savingsTrend is not null && _savingsTrend.ConsecutiveOnTargetStreak > 0
        ? UiText.Format("T484", _savingsTrend.ConsecutiveOnTargetStreak.ToString(System.Globalization.CultureInfo.CurrentCulture), UiText.Get("T477"))
        : string.Empty;
    public System.Collections.ObjectModel.ObservableCollection<SavingsTrendPointItem> SavingsTrendPoints => _savingsTrendPoints;

    private static string YearMonthName(int year, int month) =>
        new DateTime(year, Math.Clamp(month, 1, 12), 1).ToString("MMM yyyy", System.Globalization.CultureInfo.CurrentCulture);
    public string CurrentYearNetText => Money.Format(_ledgerSummary.CurrentYear.NetProfitMinor);
    public string TotalExpensesText => Money.Format(_ledgerSummary.Overall.ExpensesMinor);
    public string ReportGrossText => Money.Format(SummarizeReport().GrossProfitMinor);
    public string ReportAverageNetText => Money.Format(SummarizeReport().AverageNetMinor);
    public string ReportSalesText => Money.Format(SummarizeReport().SalesMinor);
    public string ReportExpensesText => Money.Format(SummarizeReport().ExpensesMinor);
    public string ReportCountText => SummarizeReport().EntriesCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
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
        StartupCloudDataMessage = string.Empty;
        OnPropertyChanged(nameof(StartupCloudDataMessage));
        OnPropertyChanged(nameof(UserEmail));
        OnPropertyChanged(nameof(IsCloudAccount));
        OnPropertyChanged(nameof(AccountModeText));
        await ReloadAsync();
        if (!session.IsLocal)
        {
            await SynchronizeCloudOnStartupAsync(session);
        }

        var info = await _backupService.ReadInfoAsync(session.UserId);
        SetBackupStatus(info);
    }

    private async Task SynchronizeCloudOnStartupAsync(AuthSession session)
    {
        try
        {
            var syncResult = await SyncInternalAsync();
            if (syncResult.RemoteWins > 0
                || (_lastBusinessSyncResult?.RemoteWins ?? 0) > 0
                || (_lastPersonalEntitySyncResult?.RemoteWins ?? 0) > 0)
            {
                StartupCloudDataMessage = UiText.Get("T863");
            }

            StatusMessage = FormatSyncSummary(syncResult);
        }
        catch (Exception exception)
        {
            var detail = exception.Message.Trim();
            SyncStatus = string.IsNullOrWhiteSpace(detail)
                ? UiText.Get("T135")
                : $"{UiText.Get("T135")} {detail}";
        }

        // إذا لم توجد بيانات في المسار الجديد، جرّب نسخة Flutter القديمة المرتبطة بالبريد.
        await RestoreLegacyCloudBackupAsync(session);
    }

    private async Task RestoreLegacyCloudBackupAsync(AuthSession session)
    {
        try
        {
            var restoredCount = await _syncService.RestoreLegacyBackupIfEmptyAsync(session);
            if (restoredCount <= 0)
            {
                return;
            }

            await ReloadAsync();
            await WriteBackupAndUpdateAsync();
            StatusMessage = UiText.Format("T860", restoredCount);
            StartupCloudDataMessage = StatusMessage;

            try
            {
                await SyncInternalAsync();
                StatusMessage = $"{StatusMessage}{Environment.NewLine}{SyncStatus}";
            }
            catch (Exception exception)
            {
                SyncStatus = $"{UiText.Get("T135")} {exception.Message}";
            }
        }
        catch (Exception exception)
        {
            SyncStatus = $"{UiText.Get("T135")} {exception.Message}";
        }
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
        await RebuildBusinessFinancialPositionAsync(session.UserId);
        RebuildRecent();
        RebuildCategorySpending();
        RebuildMovementFilterCategories();
        RebuildReport();
        RebuildAnnualReport(entries);
        await RebuildPlanningAsync(session.UserId, entries);
        await RebuildAnalyticsAndAuditAsync(session.UserId, entries);
        await RebuildBudgetAsync(session.UserId, entries);
        RaiseSummaries();
    }

    public PersonalPlanProgress? PlanProgress => _planProgress;

    public string PlanExpectedIncomeText => Money.Format(_planProgress?.ExpectedIncomeMinor ?? 0);
    public string PlanSpendingLimitText => Money.Format(_planProgress?.ExpectedSpendingLimitMinor ?? 0);
    public string PlanSavingsTargetText => Money.Format(_planProgress?.ExpectedSavingsTargetMinor ?? 0);
    public string PlanActualIncomeText => Money.Format(_planProgress?.ActualIncomeMinor ?? 0);
    public string PlanActualSpendingText => Money.Format(_planProgress?.ActualSpendingMinor ?? 0);
    public double PlanSpendingProgress =>
        _planProgress is null ? 0.0 : SafeProgress(_planProgress.ActualSpendingMinor, _planProgress.ExpectedSpendingLimitMinor);
    public double PlanSavingsProgress => _planProgress?.SavingsProgressPercent ?? 0.0;
    public string PlanProgressPercentText =>
        _planProgress is null ? string.Empty : $"{Math.Clamp(_planProgress.ActualSpendingMinor >= 0 && _planProgress.ExpectedSpendingLimitMinor > 0 ? _planProgress.ActualSpendingMinor / (double)_planProgress.ExpectedSpendingLimitMinor * 100.0 : 0.0, 0, 100):F0}%";
    public bool HasPlan => _planProgress is not null;

    public IReadOnlyList<ObligationEvent> UpcomingObligations => _upcomingObligations;
    public int OverdueObligationsCount => _upcomingObligations.Count(static item => !item.IsPaid && item.DueDate < DateOnly.FromDateTime(DateTime.Today));
    public System.Collections.ObjectModel.ObservableCollection<CategoryRegistryItem> CategoryRegistryItems => _categoryRegistryItems;

    public bool HasFinancialPosition => _businessFinancialPosition is not null;
    public string ReceivableNetText => Money.Format(_businessFinancialPosition?.ReceivableNetMinor ?? 0);
    public string PayableNetText => Money.Format(_businessFinancialPosition?.PayableNetMinor ?? 0);
    public string NetPositionText => Money.Format(_businessFinancialPosition?.NetPositionMinor ?? 0);
    public string NetPositionColor =>
        (_businessFinancialPosition?.NetPositionMinor ?? 0) >= 0 ? "#137A53" : "#C2413A";
    public string OverdueSalesText => Money.Format(_businessFinancialPosition?.OverdueSalesMinor ?? 0);
    public string OverduePurchasesText => Money.Format(_businessFinancialPosition?.OverduePurchasesMinor ?? 0);
    public bool HasOverdueInvoices => _overdueSalesInvoices.Count > 0 || _overduePurchasesInvoices.Count > 0;
    public bool IsOverdueExpanded { get => _isOverdueExpanded; private set => SetProperty(ref _isOverdueExpanded, value); }
    public ObservableCollection<OverdueInvoiceItem> OverdueSalesInvoices => _overdueSalesInvoices;
    public ObservableCollection<OverdueInvoiceItem> OverduePurchasesInvoices => _overduePurchasesInvoices;

    public void ToggleOverdueExpanded() => IsOverdueExpanded = !IsOverdueExpanded;

    private static double SafeProgress(long actual, long limit) =>
        limit > 0 ? Math.Clamp(actual / (double)limit, 0, 1) : 0.0;

    private async Task RebuildPlanningAsync(string userId, IReadOnlyList<ProfitEntry> entries)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var plans = await _planningRepository.GetPlansAsync(userId);
        var latestPlan = plans
            .OrderByDescending(row => row.UpdatedAtUtcTicks)
            .FirstOrDefault();

        _planProgress = latestPlan is null
            ? null
            : PersonalFinancialPlanCalculator.TrackPlan(
                ToPlanModel(latestPlan),
                entries,
                today);

        var obligations = await _planningRepository.GetObligationsAsync(userId);
        _upcomingObligations = ObligationScheduleCalculator.UpcomingEvents(
            obligations.Select(ToObligationModel),
            today,
            lookAheadDays: 45)
            .Take(10)
            .ToArray();
        await RebuildGoalsAndForecastAsync(userId, entries, today, obligations);
        await RebuildRecurringAsync(userId, entries);

        var registryItems = CategoryRegistryCalculator.ComputeRegistry(
            entries.Select(static item => new CategoryEntry(item.Category, item.EntryDate)),
            [UiText.Get("T390")]);
        _categoryRegistryItems.Clear();
        foreach (var item in registryItems)
        {
            _categoryRegistryItems.Add(item);
        }
        OnPropertyChanged(nameof(CategoryRegistryItems));
    }
    private async Task RebuildGoalsAndForecastAsync(string userId, IReadOnlyList<ProfitEntry> entries, DateOnly today, IReadOnlyList<ObligationRow> obligations)
    {
        await Task.WhenAll(
            RebuildSavingsGoalsAsync(userId),
            RebuildFinancialHealthAsync(userId, entries, today),
            RebuildCashForecastAsync(entries, today, obligations));
    }
    private async Task RebuildSavingsGoalsAsync(string userId)
    {
        var rows = await _planningRepository.GetGoalsAsync(userId);
        _savingsGoals = rows.ToArray();
        OnPropertyChanged(nameof(SavingsGoals));
        OnPropertyChanged(nameof(HasGoals));
    }
    private async Task RebuildFinancialHealthAsync(string userId, IReadOnlyList<ProfitEntry> entries, DateOnly today)
    {
        var plans = await _planningRepository.GetPlansAsync(userId);
        var latestPlan = plans
            .OrderByDescending(row => row.UpdatedAtUtcTicks)
            .FirstOrDefault();
        var recentPlans = latestPlan is null ? Array.Empty<FinancialPlan>()
            : new[] { ToPlanModel(latestPlan) };
        _financialHealth = FinancialHealthCalculator.Assess(entries, recentPlans);
        OnPropertyChanged(nameof(FinancialHealth));
        OnPropertyChanged(nameof(HealthScoreText));
        OnPropertyChanged(nameof(HealthLevelText));
        OnPropertyChanged(nameof(HealthScoreColor));
        OnPropertyChanged(nameof(SavingsRateText));
        OnPropertyChanged(nameof(FixedCostRatioText));
        OnPropertyChanged(nameof(RunwayText));
        OnPropertyChanged(nameof(HealthFlags));
    }
    private Task RebuildCashForecastAsync(IReadOnlyList<ProfitEntry> entries, DateOnly today, IReadOnlyList<ObligationRow> obligations)
    {
        _cashForecast = CashForecastCalculator.Forecast(
            entries,
            obligations.Select(ToObligationModel),
            today);
        OnPropertyChanged(nameof(CashForecast));
        OnPropertyChanged(nameof(ForecastHasWorst));
        OnPropertyChanged(nameof(ForecastWorstText));
        OnPropertyChanged(nameof(ForecastNextMonthText));
        OnPropertyChanged(nameof(ForecastNextMonthColor));
        return Task.CompletedTask;
    }

    private static FinancialPlan ToPlanModel(FinancialPlanRow row) => new(
        row.MonthlyIncomeMinor,
        row.MonthlySpendingLimitMinor,
        row.MonthlySavingsTargetMinor,
        PlanningRepository.ParseCategoryLimits(row.CategoryLimitsJson).ToArray());

    private async Task RebuildBusinessFinancialPositionAsync(string userId)
    {
        var contacts = await _businessRepository.GetContactsAsync(userId);
        var invoices = (await _businessRepository.GetInvoicesAsync(userId)).ToArray();
        var payments = (await _businessRepository.GetPaymentsAsync(userId)).ToArray();
        var asOf = DateOnly.FromDateTime(DateTime.Today);
        _businessFinancialPosition = BusinessFinancialSummaryCalculator.Summarize(contacts, invoices, payments, asOf);

        _overdueSalesInvoices.Clear();
        _overduePurchasesInvoices.Clear();
        var paymentByContact = new Dictionary<string, long>();
        foreach (var payment in payments)
        {
            paymentByContact.TryGetValue(payment.ContactId, out var current);
            paymentByContact[payment.ContactId] = checked(current + payment.AmountMinor);
        }

        foreach (var invoice in invoices.Where(invoice => invoice.Status == InvoiceStatus.Posted && invoice.DueDate < asOf && invoice.TotalMinor > 0))
        {
            var paid = paymentByContact.TryGetValue(invoice.ContactId, out var perContact)
                ? perContact
                : 0;
            var outstanding = checked(invoice.TotalMinor - paid);
            if (outstanding <= 0)
            {
                continue;
            }

            var target = invoice.Type == InvoiceType.Sales ? _overdueSalesInvoices : _overduePurchasesInvoices;
            target.Add(new OverdueInvoiceItem(invoice, contacts));
        }

        OnPropertyChanged(nameof(HasFinancialPosition));
        OnPropertyChanged(nameof(ReceivableNetText));
        OnPropertyChanged(nameof(PayableNetText));
        OnPropertyChanged(nameof(NetPositionText));
        OnPropertyChanged(nameof(NetPositionColor));
        OnPropertyChanged(nameof(OverdueSalesText));
        OnPropertyChanged(nameof(OverduePurchasesText));
        OnPropertyChanged(nameof(HasOverdueInvoices));

        _debtAging = DebtAgingCalculator.Age(invoices, payments, asOf);
        OnPropertyChanged(nameof(ReceivablesAgingTotalText));
        OnPropertyChanged(nameof(ReceivablesAgingCurrentText));
        OnPropertyChanged(nameof(ReceivablesAgingOverNinetyText));
        OnPropertyChanged(nameof(PayablesAgingTotalText));
        OnPropertyChanged(nameof(HasAgingExposure));

        var personalEntries = await _repository.GetVisibleAsync(userId);
        _personalQualityWarnings = DataQualityChecker.CheckPersonal(personalEntries, contacts, payments, invoices);
        OnPropertyChanged(nameof(DataQualityStatusText));
        OnPropertyChanged(nameof(DataQualityStatusColor));
        OnPropertyChanged(nameof(HasDataQualityWarnings));
    }

    private static Obligation ToObligationModel(ObligationRow row) => new(
        row.ObligationId,
        row.UserId,
        row.Title,
        row.Category,
        row.AmountMinor,
        DateOnly.FromDateTime(new DateTime(row.StartDateTicks, DateTimeKind.Utc)),
        row.Cycle,
        row.IsActive,
        PlanningRepository.ParsePaidOccurrences(row.PaidOccurrencesJson).ToArray());

    public async Task<SavingsGoalRow> UpsertGoalAsync(string userId, SavingsGoalRow row)
    {
        await RunBusyAsync(async () =>
        {
            var sanitized = new SavingsGoalRow
            {
                GoalId = row.GoalId,
                UserId = userId,
                Title = InputSanitizer.SanitizeName(row.Title ?? string.Empty).Trim(),
                Category = InputSanitizer.SanitizeName(row.Category ?? string.Empty).Trim(),
                TargetMinor = row.TargetMinor,
                SavedMinor = row.SavedMinor,
                StartDateTicks = row.StartDateTicks,
                DeadlineTicks = row.DeadlineTicks,
                UpdatedAtUtcTicks = DateTime.UtcNow.Ticks,
                Version = row.Version,
                DeviceId = row.DeviceId,
                IsDeleted = row.IsDeleted,
            };
            await _planningRepository.UpsertGoalAsync(userId, sanitized);
        });
        await ReloadAsync();
        return _savingsGoals.LastOrDefault(goal => string.Equals(goal.GoalId, row.GoalId, StringComparison.Ordinal)) ?? row;
    }
    public async Task DeleteGoalAsync(string userId, string goalId)
    {
        await RunBusyAsync(async () =>
        {
            var goals = (await _planningRepository.GetGoalsAsync(userId)).ToArray();
            var goal = goals.FirstOrDefault(candidate => string.Equals(candidate.GoalId, goalId, StringComparison.Ordinal));
            if (goal is not null)
            {
                goal.IsDeleted = true;
                goal.UpdatedAtUtcTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
                await _planningRepository.UpsertGoalAsync(userId, goal);
            }
        });
        await ReloadAsync();
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
        var entryCurrency = ResolveEntryCurrency(session.UserId);

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
                CounterpartyInput.Trim(),
                string.Empty,
                (_editingEntry ?? existingByDate)?.AttachmentBase64 ?? string.Empty,
                entryCurrency);

            var existingVisible = Models().Where(entry => !entry.IsDeleted).ToList();
            PossibleDuplicateDetected = DuplicateDetector.IsLikelyDuplicate(entry, existingVisible);
            if (PossibleDuplicateDetected)
            {
                StatusMessage = UiText.Get("T824");
            }

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
                    var completedAt = syncResult.CompletedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
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
                    var completedAt = syncResult.CompletedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
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
        _lastPersonalEntitySyncResult = string.Equals(_preferences.StorageScope, "personal", StringComparison.Ordinal)
            ? await _personalEntitySyncService.SyncAsync()
            : null;
        SyncStatus = FormatSyncSummary(result);
        await ReloadAsync();
        await WriteBackupAndUpdateAsync();
        return result;
    }

    private string FormatSyncSummary(SyncResult result)
    {
        var completedAt = result.CompletedAtUtc.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);
        var summary = UiText.Format(
            "T393",
            completedAt,
            result.Uploaded,
            result.LocalWins,
            result.RemoteWins,
            result.TotalEntries);

        if (_lastBusinessSyncResult is null && _lastPersonalEntitySyncResult is null)
        {
            return summary;
        }

        if (_lastPersonalEntitySyncResult is not null)
        {
            var personal = _lastPersonalEntitySyncResult;
            summary = $"{summary}{Environment.NewLine}{UiText.Format(
                "T395",
                personal.PlansTotal,
                personal.ObligationsTotal,
                personal.DepositsTotal,
                personal.Uploaded,
                personal.LocalWins,
                personal.RemoteWins,
                personal.TotalRecords)}";
        }

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
            ? UiText.Format("T138", info.UpdatedAtUtc?.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty, info.EntriesCount)
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
        CurrencyInput = string.Empty;
        AttachmentBase64 = string.Empty;
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

    internal static string DisplayMovementType(string movementType) => movementType switch
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

    private void RebuildAnnualReport(IReadOnlyList<ProfitEntry> entries)
    {
        var year = DateOnly.FromDateTime(DateTime.Today).Year;
        _annualReport = PersonalAnnualReportCalculator.Build(entries, year);
        var previousReport = PersonalAnnualReportCalculator.Build(entries, year - 1);
        _yearOverYearChangePercent = PersonalAnnualReportCalculator.YearOverYearIncomeChangePercent(
            _annualReport.Months,
            previousReport.Months);

        var categoryMonth = DateOnly.FromDateTime(DateTime.Today);
        _categoryReport = PersonalCategoryReportCalculator.Build(entries, categoryMonth);
        _categoryBreakdownItems.Clear();
        foreach (var breakdown in _categoryReport.Breakdowns.Take(8))
        {
            _categoryBreakdownItems.Add(new CategoryBreakdownItem(
                breakdown,
                _categoryReport, 
                PaletteColor.ForRank(breakdown.Rank)));
        }

        _savingsTrend = SavingsTrendCalculator.Build(
            entries,
            DateOnly.FromDateTime(DateTime.Today),
            trailingMonths: 6,
            plan: _planProgress is null ? null : ToPlanModelFromProgress(_planProgress));
        var maxAbsSaved = _savingsTrend.Points.Count == 0 ? 1 : Math.Max(1L, _savingsTrend.Points.Max(static point => Math.Abs(point.SavedMinor)));
        _savingsTrendPoints.Clear();
        foreach (var point in _savingsTrend.Points)
        {
            _savingsTrendPoints.Add(new SavingsTrendPointItem(point, maxAbsSaved));
        }
    }

    private static FinancialPlan ToPlanModelFromProgress(PersonalPlanProgress progress) => new(
        progress.ExpectedIncomeMinor,
        progress.ExpectedSpendingLimitMinor,
        progress.ExpectedSavingsTargetMinor,
        Array.Empty<PlanCategoryLimit>());

    public async Task<string> ExportPersonalReportCsvAsync()
    {
        var session = RequireSession();
        var entries = await _repository.GetVisibleAsync(session.UserId);
        var year = DateOnly.FromDateTime(DateTime.Today).Year;
        var report = PersonalAnnualReportCalculator.Build(entries, year);
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(string.Join(",", new[]
        {
            UiText.Get("T021"), UiText.Get("T465"), UiText.Get("T466"), UiText.Get("T467")
        }.Select(EscapeCsv)));
        foreach (var month in report.Months)
        {
            builder.AppendLine(string.Join(",", new object?[]
            {
                YearMonthName(month.Year, month.Month),
                Money.ToDecimal(month.IncomeMinor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                Money.ToDecimal(month.SpendingMinor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                Money.ToDecimal(month.NetMinor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            }.Select(cell => EscapeCsv(cell?.ToString() ?? string.Empty))));
        }
        builder.AppendLine(string.Join(",", new object?[]
        {
            UiText.Get("T070"),
            Money.ToDecimal(report.TotalIncomeMinor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            Money.ToDecimal(report.TotalSpendingMinor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            Money.ToDecimal(report.TotalNetMinor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
        }.Select(cell => EscapeCsv(cell?.ToString() ?? string.Empty))));
        var fileName = $"maen-accounting-annual-{year}.csv";
        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(filePath, builder.ToString(), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return filePath;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private void RebuildReport()
    {
        ReportEntries.Clear();
        ReportDays.Clear();
        ApplyMovementFilters();
        var query = _movementSearchQueryText.Trim();
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

    private void ApplyMovementFilters()
    {
        _movementSearchQueryText = MovementSearchText;
        _movementSearchResults.Clear();
        var category = string.IsNullOrWhiteSpace(MovementCategoryFilter) ? null : MovementCategoryFilter.Trim();
        var filterResult = PersonalMovementFilterEngine.Apply(
            Models(),
            MovementSearchText.Trim(),
            category,
            DateOnly.FromDateTime(MovementFilterFromDate),
            DateOnly.FromDateTime(MovementFilterToDate));
        foreach (var entry in filterResult.Entries)
        {
            _movementSearchResults.Add(new MovementSearchItemViewModel(entry));
        }
        _lastFilteredMovements = filterResult;
        OnPropertyChanged(nameof(MovementSearchResults));
        OnPropertyChanged(nameof(HasMovementSearchResults));
        OnPropertyChanged(nameof(MovementFilterDepositsText));
        OnPropertyChanged(nameof(MovementFilterSpendingText));
        OnPropertyChanged(nameof(MovementFilterNetText));
    }
    private FilteredMovements _lastFilteredMovements = new(Array.Empty<ProfitEntry>(), 0, 0);
    private static readonly DateTime _defaultFilterFromDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public string MovementFilterDepositsText => Money.Format(_lastFilteredMovements.DepositsMinor);
    public string MovementFilterSpendingText => Money.Format(_lastFilteredMovements.SpendingMinor);
    public string MovementFilterNetText => Money.Format(_lastFilteredMovements.NetMinor);
    public string MovementFilterNetColor => _lastFilteredMovements.NetMinor >= 0 ? "#137A53" : "#C2413A";
    public bool HasMovementFilter => _movementCategoryFilter.Length > 0 || _movementFilterFromDate != _defaultFilterFromDate || _movementFilterToDate != DateTime.Today;

    public void ClearMovementFilter()
    {
        MovementCategoryFilter = string.Empty;
        MovementFilterFromDate = _defaultFilterFromDate;
        MovementFilterToDate = DateTime.Today;
        OnPropertyChanged(nameof(HasMovementFilter));
    }
    public void RebuildMovementFilterCategories()
    {
        var current = MovementCategoryFilter;
        _movementFilterCategories.Clear();
        _movementFilterCategories.Add(string.Empty);
        foreach (var category in Models()
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Category))
            .Select(entry => entry.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase))
        {
            _movementFilterCategories.Add(category);
        }
        if (!_movementFilterCategories.Contains(current, StringComparer.OrdinalIgnoreCase))
        {
            MovementCategoryFilter = string.Empty;
        }
        OnPropertyChanged(nameof(HasMovementFilter));
    }
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
        RaiseAnnualReportProperties();
        RaiseBudgetProperties();
        RaisePlanningProperties();
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

    private void RaiseCurrencyProperties()
    {
        OnPropertyChanged(nameof(DisplayCurrency));
        OnPropertyChanged(nameof(HasBudgetStatus));
        OnPropertyChanged(nameof(CurrentBudgetStatus));
        OnPropertyChanged(nameof(BudgetStatusAlertText));
        OnPropertyChanged(nameof(BudgetStatusAlertColor));
        OnPropertyChanged(nameof(BudgetRemainingText));
        OnPropertyChanged(nameof(BudgetProjectedText));
        OnPropertyChanged(nameof(DisplayCurrencyOptions));
    }

    private async Task RebuildBudgetAsync(string userId, IReadOnlyList<ProfitEntry> entries)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var yearMonth = today.Year * 100 + today.Month;
        var plans = await _planningRepository.GetBudgetPlansAsync(userId);
        var activePlan = plans.FirstOrDefault(plan => plan.YearMonth == yearMonth && plan.IsActive);
        if (activePlan is null && plans.Count > 0)
        {
            activePlan = plans[0];
        }

        _currentBudgetStatus = activePlan is null
            ? null
            : BudgetPlannerCalculator.Assess(activePlan.AmountMinor, entries, today);
        RaiseCurrencyProperties();
    }

    public async Task SaveMonthlyBudgetAsync()
    {
        if (!Money.TryParse(_monthlyBudgetInput, out var amountMinor) || amountMinor < 0)
        {
            return;
        }

        var session = RequireSession();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var yearMonth = today.Year * 100 + today.Month;
        var row = new BudgetPlanRow
        {
            PlanId = Guid.NewGuid().ToString("N"),
            UserId = session.UserId,
            YearMonth = yearMonth,
            AmountMinor = amountMinor,
            IsActive = true,
            UpdatedAtUtcTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks,
            Version = 1,
            DeviceId = _deviceIdentity.GetOrCreate(),
            IsDeleted = false
        };

        await _planningRepository.UpsertBudgetPlanAsync(session.UserId, row);
        Preferences.Default.Set("maen_personal_monthly_budget_v1", _monthlyBudgetInput.Trim());
        await ReloadAsync();
    }

    private void RaisePlanningProperties()
    {
        OnPropertyChanged(nameof(PlanExpectedIncomeText));
        OnPropertyChanged(nameof(PlanSpendingLimitText));
        OnPropertyChanged(nameof(PlanSavingsTargetText));
        OnPropertyChanged(nameof(PlanActualIncomeText));
        OnPropertyChanged(nameof(PlanActualSpendingText));
        OnPropertyChanged(nameof(PlanSpendingProgress));
        OnPropertyChanged(nameof(PlanSavingsProgress));
        OnPropertyChanged(nameof(PlanProgressPercentText));
        OnPropertyChanged(nameof(HasPlan));
        OnPropertyChanged(nameof(UpcomingObligations));
        OnPropertyChanged(nameof(OverdueObligationsCount));
        OnPropertyChanged(nameof(SavingsGoals));
        OnPropertyChanged(nameof(HasGoals));
        OnPropertyChanged(nameof(FinancialHealth));
        OnPropertyChanged(nameof(ForecastHasSlices));
        OnPropertyChanged(nameof(ForecastSlices));
        OnPropertyChanged(nameof(HealthFlags));
        OnPropertyChanged(nameof(HasHealthFlags));
        OnPropertyChanged(nameof(CashForecast));
        OnPropertyChanged(nameof(HealthScoreText));
        OnPropertyChanged(nameof(HealthLevelText));
        OnPropertyChanged(nameof(HealthScoreColor));
        OnPropertyChanged(nameof(SavingsRateText));
        OnPropertyChanged(nameof(FixedCostRatioText));
        OnPropertyChanged(nameof(RunwayText));
        OnPropertyChanged(nameof(HealthFlags));
        OnPropertyChanged(nameof(ForecastHasWorst));
        OnPropertyChanged(nameof(ForecastWorstText));
        OnPropertyChanged(nameof(ForecastNextMonthText));
        OnPropertyChanged(nameof(ForecastNextMonthColor));
        OnPropertyChanged(nameof(CategoryAnalytics));
        OnPropertyChanged(nameof(HasCategoryAnalytics));
        OnPropertyChanged(nameof(TopCategory));
        OnPropertyChanged(nameof(TopCategoryText));
        OnPropertyChanged(nameof(MonthlyAmounts));
        OnPropertyChanged(nameof(HasMonthlyAmounts));
        OnPropertyChanged(nameof(PeakMonth));
        OnPropertyChanged(nameof(LowMonth));
        OnPropertyChanged(nameof(AuditSummary));
        OnPropertyChanged(nameof(HasAuditSummary));
        OnPropertyChanged(nameof(AuditVerdictText));
        OnPropertyChanged(nameof(AuditVerdictColor));
        OnPropertyChanged(nameof(AuditedRecordsText));
        OnPropertyChanged(nameof(RecurringMovements));
        OnPropertyChanged(nameof(HasRecurringMovements));
    }

    private void RaiseAnnualReportProperties()
    {
        OnPropertyChanged(nameof(HasAnnualReport));
        OnPropertyChanged(nameof(AnnualIncomeText));
        OnPropertyChanged(nameof(AnnualSpendingText));
        OnPropertyChanged(nameof(AnnualNetText));
        OnPropertyChanged(nameof(AnnualNetColor));
        OnPropertyChanged(nameof(BestMonthText));
        OnPropertyChanged(nameof(WorstMonthText));
        OnPropertyChanged(nameof(YearOverYearText));
        OnPropertyChanged(nameof(YearOverYearColor));
        OnPropertyChanged(nameof(HasCategoryBreakdown));
        OnPropertyChanged(nameof(CategoryBreakdownItems));
        OnPropertyChanged(nameof(HasSavingsTrend));
        OnPropertyChanged(nameof(TrendStreakText));
        OnPropertyChanged(nameof(SavingsTrendPoints));
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

        AccountingAccountsText = accounts.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);
        PostedJournalCountText = journalEntries.Count(static entry => entry.Status == JournalEntryStatus.Posted).ToString(System.Globalization.CultureInfo.CurrentCulture);
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

    private async Task RebuildRecurringAsync(string userId, IReadOnlyList<ProfitEntry> entries)
    {
        var rows = await _planningRepository.GetRecurringAsync(userId);
        _recurringMovements = rows.ToArray();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var models = rows
            .Select(static row => new RecurringMovement(
                row.RecurringId,
                row.UserId,
                row.Title,
                row.Category,
                row.AmountMinor,
                row.Kind,
                DateOnly.FromDateTime(new DateTime(row.StartDateTicks, DateTimeKind.Utc)),
                row.Cycle,
                DateOnly.FromDateTime(new DateTime(row.NextOccurrenceTicks, DateTimeKind.Utc)),
                row.IsActive,
                row.Notes))
            .ToArray();

        var due = RecurringMovementCalculator.DueMovements(models, today);
        if (due.Count > 0)
        {
            var newEntries = new List<ProfitEntry>();
            foreach (var movement in due)
            {
                var (sales, cost, expenses) = RecurringMovementCalculator.BuildAmounts(movement);
                var nowUtc = DateTimeOffset.UtcNow;
                var entry = new ProfitEntry(
                    Guid.NewGuid().ToString("N"),
                    userId,
                    today,
                    sales,
                    cost,
                    expenses,
                    string.IsNullOrEmpty(movement.Notes)
                        ? movement.Title
                        : $"{movement.Title} — {movement.Notes}",
                    false,
                    nowUtc,
                    nowUtc,
                    1,
                    _deviceIdentity.GetOrCreate(),
                    movement.AmountMinor,
                    movement.Kind.Equals("income", StringComparison.OrdinalIgnoreCase) ? "income" : "expense",
                    movement.Category);
                newEntries.Add(entry);
                await _planningRepository.AdvanceOccurrenceAsync(userId, movement.RecurringId, RecurringMovementCalculator.AdvanceOccurrence(movement.NextOccurrence, movement.Cycle));
            }

            await _repository.UpsertManyAsync(userId, newEntries);
            _statusMessage = UiText.Get("T712");
            OnPropertyChanged(nameof(StatusMessage));
        }

        OnPropertyChanged(nameof(RecurringMovements));
        OnPropertyChanged(nameof(HasRecurringMovements));
    }

    public async Task UpsertRecurringMovementAsync(
        string title,
        string category,
        long amountMinor,
        string kind,
        string cycle,
        DateOnly startDate,
        string notes = "")
    {
        var session = RequireSession();
        var deviceId = _deviceIdentity.GetOrCreate();
        var sanitizedTitle = InputSanitizer.SanitizeName(title);
        var row = new RecurringMovementRow
        {
            RecurringId = Guid.NewGuid().ToString("N"),
            UserId = session.UserId,
            Title = sanitizedTitle,
            Category = InputSanitizer.SanitizeName(category),
            AmountMinor = amountMinor,
            Kind = kind,
            StartDateTicks = startDate.ToDateTime(TimeOnly.MinValue).Ticks,
            Cycle = cycle,
            NextOccurrenceTicks = startDate.ToDateTime(TimeOnly.MinValue).Ticks,
            IsActive = true,
            Notes = notes,
            UpdatedAtUtcTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks,
            Version = 1,
            DeviceId = deviceId,
            IsDeleted = false
        };

        await _planningRepository.UpsertRecurringAsync(session.UserId, row);
        await ReloadAsync();
    }

    public async Task DeleteRecurringMovementAsync(string recurringId)
    {
        var session = RequireSession();
        await _planningRepository.SoftDeleteRecurringAsync(session.UserId, recurringId);
        await ReloadAsync();
    }

    private async Task RebuildAnalyticsAndAuditAsync(string userId, IReadOnlyList<ProfitEntry> entries)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var analyticsTask = Task.Run(() =>
            (Analytics: TopCategoryAnalyticsEngine.Analyze(entries, currentMonth),
             Totals: TopCategoryAnalyticsEngine.MonthlyTotals(entries, 12)));

        var syncTask = _personalEntitySyncService
            .SyncAsync()
            .ContinueWith(task => task.IsCompletedSuccessfully ? task.Result : null, TaskScheduler.Default);

        var (analytics, totals) = await analyticsTask;
        _categoryAnalytics = analytics.ToArray();
        _monthlyAmounts = totals.ToArray();
        OnPropertyChanged(nameof(CategoryAnalytics));
        OnPropertyChanged(nameof(HasCategoryAnalytics));
        OnPropertyChanged(nameof(TopCategory));
        OnPropertyChanged(nameof(TopCategoryText));
        OnPropertyChanged(nameof(MonthlyAmounts));
        OnPropertyChanged(nameof(HasMonthlyAmounts));

        try
        {
            _auditSummary = AuditTrailCalculator.Summarize(
                entries,
                verifiedHashCount: 0,
                totalRecords: entries.Count,
                lastSyncUtc: null);
            OnPropertyChanged(nameof(AuditSummary));
            OnPropertyChanged(nameof(HasAuditSummary));
            OnPropertyChanged(nameof(AuditVerdictText));
            OnPropertyChanged(nameof(AuditVerdictColor));
            OnPropertyChanged(nameof(AuditedRecordsText));
        }
        catch
        {
            _auditSummary = null;
        }

        _ = syncTask;
    }

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
