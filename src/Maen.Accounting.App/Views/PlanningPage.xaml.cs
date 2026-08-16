using Maen.Accounting.App.Data;
using Maen.Accounting.App.Infrastructure;
using Maen.Accounting.App.Services;
using Maen.Accounting.Core.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace Maen.Accounting.App.Views;

public sealed partial class PlanningPage : ContentPage
{
    private readonly PlanningRepository _repository;
    private readonly ProfitEntryRepository _profitRepository;
    private readonly DeviceIdentityService _deviceIdentity;
    private readonly AuthSessionStore _sessionStore;
    private readonly PlanningViewModel _model;

    public PlanningPage(
        PlanningRepository repository,
        ProfitEntryRepository profitRepository,
        DeviceIdentityService deviceIdentity,
        AuthSessionStore sessionStore)
    {
        InitializeComponent();
        _repository = repository;
        _profitRepository = profitRepository;
        _deviceIdentity = deviceIdentity;
        _sessionStore = sessionStore;
        _model = new PlanningViewModel(this, repository, profitRepository, sessionStore);
        BindingContext = _model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _model.ReloadAsync();
    }

    private async void OnSavePlanClicked(object? sender, EventArgs e)
    {
        await _model.SavePlanAsync(_deviceIdentity);
    }

    private async void OnAddObligationClicked(object? sender, EventArgs e)
    {
        await _model.AddObligationAsync(_deviceIdentity);
    }

    private async void OnAddDepositClicked(object? sender, EventArgs e)
    {
        await _model.AddDepositAsync(_deviceIdentity);
    }

    private async void OnToggleObligationClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.BindingContext is not ObligationItem item)
        {
            return;
        }

        await _model.ToggleObligationAsync(item, !item.IsActive);
    }

    private async void OnDeleteObligationClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.BindingContext is not ObligationItem item)
        {
            return;
        }

        await _model.ToggleObligationAsync(item, false);
    }

    private async void OnDeleteDepositClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.BindingContext is not DepositItem item)
        {
            return;
        }

        await _model.DeleteDepositAsync(item);
    }
}

public sealed class PlanningViewModel : ObservableObject
{
    private readonly Page _page;
    private readonly PlanningRepository _repository;
    private readonly ProfitEntryRepository _profitRepository;
    private readonly AuthSessionStore _sessionStore;

    public PlanningViewModel(
        Page page,
        PlanningRepository repository,
        ProfitEntryRepository profitRepository,
        AuthSessionStore sessionStore)
    {
        _page = page;
        _repository = repository;
        _profitRepository = profitRepository;
        _sessionStore = sessionStore;
    }

    public string PlanIncomeInput { get; set; } = string.Empty;
    public string PlanSpendingLimitInput { get; set; } = string.Empty;
    public string PlanSavingsTargetInput { get; set; } = string.Empty;

    public System.Collections.ObjectModel.ObservableCollection<CategoryLimitItem> PlanLimitCategories { get; } = new();

    public string ObligationTitleInput { get; set; } = string.Empty;
    public string ObligationAmountInput { get; set; } = string.Empty;
    public List<string> ObligationCycles { get; } = ["daily", "weekly", "monthly", "yearly"];
    public string ObligationCycleSelection { get; set; } = "monthly";

    public System.Collections.ObjectModel.ObservableCollection<ObligationItem> Obligations { get; } = new();
    public bool HasObligations => Obligations.Count > 0;

    public string DepositTitleInput { get; set; } = string.Empty;
    public string DepositAmountInput { get; set; } = string.Empty;
    public DateTime DepositDate { get; set; } = DateTime.Today;

    public System.Collections.ObjectModel.ObservableCollection<DepositItem> Deposits { get; } = new();
    public bool HasDeposits => Deposits.Count > 0;
    public System.Collections.ObjectModel.ObservableCollection<CashForecastSliceItem> ForecastSlices { get; } = new();
    public bool ForecastHasSlices => ForecastSlices.Count > 0;
    public bool ForecastHasWorst { get; private set; }
    public string ForecastNextMonthText { get; private set; } = string.Empty;
    public string ForecastNextMonthColor { get; private set; } = "#64748B";
    public string ForecastWorstText { get; private set; } = string.Empty;

    public async Task ReloadAsync()
    {
        await LoadPlanAsync();
        await LoadObligationsAsync();
        await LoadDepositsAsync();
        await LoadForecastAsync();
    }

    private async Task LoadPlanAsync()
    {
        var userId = await GetUserIdAsync();
        if (userId is null)
        {
            return;
        }

        try
        {
            var plans = await _repository.GetPlansAsync(userId);
            var plan = plans.FirstOrDefault(item => !item.IsDeleted);
            if (plan is not null)
            {
                PlanIncomeInput = plan.MonthlyIncomeMinor > 0 ? Money.Format(plan.MonthlyIncomeMinor) : string.Empty;
                PlanSpendingLimitInput = plan.MonthlySpendingLimitMinor > 0 ? Money.Format(plan.MonthlySpendingLimitMinor) : string.Empty;
                PlanSavingsTargetInput = plan.MonthlySavingsTargetMinor > 0 ? Money.Format(plan.MonthlySavingsTargetMinor) : string.Empty;
                var limits = PlanningRepository.ParseCategoryLimits(plan.CategoryLimitsJson);
                PlanLimitCategories.Clear();
                foreach (var limit in limits)
                {
                    PlanLimitCategories.Add(new CategoryLimitItem(
                        limit.Category,
                        limit.MonthlyLimitMinor > 0 ? Money.Format(limit.MonthlyLimitMinor) : string.Empty));
                }
            }
        }
        catch (Exception)
        {
            // Offline or empty store — keep defaults.
        }
    }

    private async Task LoadObligationsAsync()
    {
        var userId = await GetUserIdAsync();
        if (userId is null)
        {
            return;
        }

        try
        {
            var rows = await _repository.GetObligationsAsync(userId);
            Obligations.Clear();
            foreach (var row in rows.Where(item => !item.IsDeleted))
            {
                Obligations.Add(new ObligationItem(
                    row.ObligationId,
                    row.Title,
                    Money.Format(row.AmountMinor),
                    TranslateCycle(row.Cycle),
                    row.IsActive,
                    row));
            }

            OnPropertyChanged(nameof(HasObligations));
        }
        catch (Exception)
        {
            // Keep list empty.
        }
    }

    private async Task LoadDepositsAsync()
    {
        var userId = await GetUserIdAsync();
        if (userId is null)
        {
            return;
        }

        try
        {
            var rows = await _repository.GetDepositsAsync(userId);
            Deposits.Clear();
            foreach (var row in rows.Where(item => !item.IsDeleted))
            {
                var date = new DateTime(row.DepositDateTicks, DateTimeKind.Unspecified).Date;
                Deposits.Add(new DepositItem(
                    row.DepositId,
                    string.IsNullOrEmpty(row.Title) ? row.OwnerName : row.Title,
                    Money.Format(row.AmountMinor),
                    date.ToString("D"),
                    row));
            }

            OnPropertyChanged(nameof(HasDeposits));
        }
        catch (Exception)
        {
            // Keep list empty.
        }
    }

    private async Task LoadForecastAsync()
    {
        var userId = await GetUserIdAsync();
        if (userId is null)
        {
            return;
        }

        try
        {
            var entries = await _profitRepository.GetVisibleAsync(userId);
            var obligations = await _repository.GetObligationsAsync(userId);
            var asOf = DateOnly.FromDateTime(DateTime.Today);
            var forecast = CashForecastCalculator.Forecast(
                entries,
                obligations
                    .Where(row => !row.IsDeleted && row.IsActive)
                    .Select(ToObligationModel),
                asOf);
            ForecastSlices.Clear();
            foreach (var slice in forecast.Slices)
            {
                ForecastSlices.Add(new CashForecastSliceItem(
                    slice.MonthLabel,
                    slice.ObligationsText,
                    slice.NetText,
                    slice.NetColor));
            }

            ForecastHasWorst = forecast.WorstSlice is not null;
            ForecastNextMonthText = forecast.Slices.Count > 0 ? forecast.Slices[0].NetText : string.Empty;
            ForecastNextMonthColor = forecast.Slices.Count > 0
                ? (forecast.Slices[0].NetMinor >= 0 ? "#137A53" : "#C2413A")
                : "#64748B";
            ForecastWorstText = forecast.WorstSlice is null ? string.Empty : forecast.WorstSlice.NetText;

            OnPropertyChanged(nameof(ForecastSlices));
            OnPropertyChanged(nameof(ForecastHasSlices));
            OnPropertyChanged(nameof(ForecastHasWorst));
            OnPropertyChanged(nameof(ForecastNextMonthText));
            OnPropertyChanged(nameof(ForecastNextMonthColor));
            OnPropertyChanged(nameof(ForecastWorstText));
        }
        catch (Exception)
        {
            // Keep forecast empty — next reload will retry.
        }
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

    public async Task SavePlanAsync(DeviceIdentityService deviceIdentity)
    {
        var userId = await GetUserIdAsync();
        if (userId is null)
        {
            await ShowMessageAsync(UiText.Get("T140"));
            return;
        }

        var plans = await _repository.GetPlansAsync(userId);
        var existing = plans.FirstOrDefault(item => !item.IsDeleted);

        long income = ParseMinor(PlanIncomeInput);
        long spending = ParseMinor(PlanSpendingLimitInput);
        long savings = ParseMinor(PlanSavingsTargetInput);
        var limits = PlanLimitCategories
            .Select(item => new PlanCategoryLimit(item.Category, ParseMinor(item.LimitText)))
            .Where(item => item.MonthlyLimitMinor > 0)
            .ToList();

        var row = existing ?? new FinancialPlanRow
        {
            PlanId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            Version = 1,
        };
        row.MonthlyIncomeMinor = income;
        row.MonthlySpendingLimitMinor = spending;
        row.MonthlySavingsTargetMinor = savings;
        row.CategoryLimitsJson = PlanningRepository.SerializeCategoryLimits(limits);
        row.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;
        row.Version += 1;
        row.IsDeleted = false;
        row.DeviceId = deviceIdentity.GetOrCreate();

        await _repository.UpsertPlanAsync(userId, row);
        await ShowMessageAsync(UiText.Get("T457"));
        await ReloadAsync();
    }

    public async Task AddObligationAsync(DeviceIdentityService deviceIdentity)
    {
        var userId = await GetUserIdAsync();
        if (userId is null)
        {
            await ShowMessageAsync(UiText.Get("T140"));
            return;
        }

        var title = ObligationTitleInput.Trim();
        var amount = ParseMinor(ObligationAmountInput);
        if (title.Length == 0 || amount <= 0)
        {
            await ShowMessageAsync(UiText.Get("T139"));
            return;
        }

        var row = new ObligationRow
        {
            ObligationId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            Title = title,
            AmountMinor = amount,
            Cycle = ObligationCycleSelection,
            StartDateTicks = DateTime.UtcNow.Ticks,
            IsActive = true,
            PaidOccurrencesJson = "[]",
            UpdatedAtUtcTicks = DateTime.UtcNow.Ticks,
            Version = 1,
            IsDeleted = false,
            DeviceId = deviceIdentity.GetOrCreate(),
        };

        await _repository.UpsertObligationAsync(userId, row);
        ObligationTitleInput = string.Empty;
        ObligationAmountInput = string.Empty;
        OnPropertyChanged(nameof(ObligationTitleInput));
        OnPropertyChanged(nameof(ObligationAmountInput));
        await ShowMessageAsync(UiText.Get("T458"));
        await ReloadAsync();
    }

    public async Task AddDepositAsync(DeviceIdentityService deviceIdentity)
    {
        var userId = await GetUserIdAsync();
        if (userId is null)
        {
            await ShowMessageAsync(UiText.Get("T140"));
            return;
        }

        var title = DepositTitleInput.Trim();
        var amount = ParseMinor(DepositAmountInput);
        if (title.Length == 0 || amount <= 0)
        {
            await ShowMessageAsync(UiText.Get("T139"));
            return;
        }

        var row = new DepositRow
        {
            DepositId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            Title = title,
            OwnerName = title,
            AmountMinor = amount,
            DepositDateTicks = DepositDate.Date.Ticks,
            Kind = "deposit",
            IsWithdrawn = false,
            Notes = string.Empty,
            UpdatedAtUtcTicks = DateTime.UtcNow.Ticks,
            Version = 1,
            IsDeleted = false,
            DeviceId = deviceIdentity.GetOrCreate(),
        };

        await _repository.UpsertDepositAsync(userId, row);
        DepositTitleInput = string.Empty;
        DepositAmountInput = string.Empty;
        OnPropertyChanged(nameof(DepositTitleInput));
        OnPropertyChanged(nameof(DepositAmountInput));
        await ShowMessageAsync(UiText.Get("T459"));
        await ReloadAsync();
    }

    public async Task ToggleObligationAsync(ObligationItem item, bool active)
    {
        try
        {
            var userId = await GetUserIdAsync();
            if (userId is null)
            {
                return;
            }

            var row = item.Row;
            var updated = new ObligationRow
            {
                ObligationId = row.ObligationId,
                UserId = row.UserId,
                Title = row.Title,
                Category = row.Category,
                AmountMinor = row.AmountMinor,
                StartDateTicks = row.StartDateTicks,
                Cycle = row.Cycle,
                IsActive = active,
                PaidOccurrencesJson = row.PaidOccurrencesJson,
                UpdatedAtUtcTicks = DateTime.UtcNow.Ticks,
                Version = row.Version + 1,
                IsDeleted = !active,
                DeviceId = row.DeviceId,
            };
            await _repository.UpsertObligationAsync(userId, updated);
            await ShowMessageAsync(UiText.Get("T460"));
            await ReloadAsync();
        }
        catch (Exception)
        {
            // Toggle ignored — next reload restores state.
        }
    }

    public async Task DeleteDepositAsync(DepositItem item)
    {
        try
        {
            var userId = await GetUserIdAsync();
            if (userId is null)
            {
                return;
            }

            var row = item.Row;
            var updated = new DepositRow
            {
                DepositId = row.DepositId,
                UserId = row.UserId,
                Title = row.Title,
                OwnerName = row.OwnerName,
                AmountMinor = row.AmountMinor,
                DepositDateTicks = row.DepositDateTicks,
                Kind = row.Kind,
                IsWithdrawn = row.IsWithdrawn,
                Notes = row.Notes,
                UpdatedAtUtcTicks = DateTime.UtcNow.Ticks,
                Version = row.Version + 1,
                IsDeleted = true,
                DeviceId = row.DeviceId,
            };
            await _repository.UpsertDepositAsync(userId, updated);
            await ShowMessageAsync(UiText.Get("T460"));
            await ReloadAsync();
        }
        catch (Exception)
        {
            // Delete ignored — next reload restores state.
        }
    }

    private async Task ShowMessageAsync(string message)
    {
        await _page.DisplayAlertAsync(UiText.Get("T120"), message, UiText.Get("T122"));
    }

    private static long ParseMinor(string input)
    {
        if (!Money.TryParse(input.Trim(), out var minor) || minor <= 0)
        {
            return 0;
        }

        return minor;
    }

    private async Task<string?> GetUserIdAsync()
    {
        var session = await _sessionStore.LoadAsync();
        return session?.UserId;
    }

    private static string TranslateCycle(string cycle) => cycle switch
    {
        "daily" => "يومي",
        "weekly" => "أسبوعي",
        "monthly" => "شهري",
        "yearly" => "سنوي",
        _ => cycle,
    };
}

public sealed record CategoryLimitItem(string Category, string LimitText);

public sealed class ObligationItem : ObservableObject
{
    public string ObligationId { get; }
    public string Title { get; }
    public string AmountText { get; }
    public string CycleText { get; }
    public ObligationRow Row { get; }

    public ObligationItem(
        string obligationId,
        string title,
        string amountText,
        string cycleText,
        bool isActive,
        ObligationRow row)
    {
        ObligationId = obligationId;
        Title = title;
        AmountText = amountText;
        CycleText = cycleText;
        Row = row;
        _isActive = isActive;
    }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetProperty(ref _isActive, value))
            {
                // UI toggle is applied through page handlers.
            }
        }
    }
}

public sealed record DepositItem(
    string DepositId,
    string Title,
    string AmountText,
    string DateText,
    DepositRow Row);

public sealed record CashForecastSliceItem(
    string MonthLabel,
    string ObligationsText,
    string NetText,
    string NetColor);
