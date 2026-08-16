using Maen.Accounting.App.Data;
using Maen.Accounting.App.Infrastructure;
using Maen.Accounting.App.Services;
using Maen.Accounting.Core.Services;
using Microsoft.Maui.Controls;

namespace Maen.Accounting.App.Views;

public sealed partial class SavingsGoalsPage : ContentPage
{
    private readonly ProfitEntryRepository _profitRepository;
    private readonly PlanningRepository _repository;
    private readonly DeviceIdentityService _deviceIdentity;
    private readonly AuthSessionStore _sessionStore;
    private readonly SavingsGoalsViewModel _model;

    public SavingsGoalsPage(
        ProfitEntryRepository profitRepository,
        PlanningRepository repository,
        DeviceIdentityService deviceIdentity,
        AuthSessionStore sessionStore)
    {
        InitializeComponent();
        _profitRepository = profitRepository;
        _repository = repository;
        _deviceIdentity = deviceIdentity;
        _sessionStore = sessionStore;
        _model = new SavingsGoalsViewModel(this, profitRepository, repository, sessionStore);
        BindingContext = _model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _model.ReloadAsync();
    }

    private async void OnSaveGoalClicked(object? sender, EventArgs e)
    {
        await _model.SaveGoalAsync(_deviceIdentity);
    }

    private async void OnDeleteGoalClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.BindingContext is not GoalProgressItem item)
        {
            return;
        }

        await _model.DeleteGoalAsync(item);
    }
}

public sealed class SavingsGoalsViewModel : ObservableObject
{
    private readonly Page _page;
    private readonly ProfitEntryRepository _profitRepository;
    private readonly PlanningRepository _repository;
    private readonly AuthSessionStore _sessionStore;

    public SavingsGoalsViewModel(
        Page page,
        ProfitEntryRepository profitRepository,
        PlanningRepository repository,
        AuthSessionStore sessionStore)
    {
        _page = page;
        _profitRepository = profitRepository;
        _repository = repository;
        _sessionStore = sessionStore;
    }

    public string GoalTitleInput { get; set; } = string.Empty;
    public string GoalCategoryInput { get; set; } = string.Empty;
    public string GoalTargetInput { get; set; } = string.Empty;
    public string GoalAlreadySavedInput { get; set; } = string.Empty;
    public DateTime GoalDeadline { get; set; } = DateTime.Today.AddMonths(6);

    public System.Collections.ObjectModel.ObservableCollection<GoalProgressItem> GoalProgressItems { get; } = [];
    public bool HasGoalProgressItems => GoalProgressItems.Count > 0;

    public async Task ReloadAsync()
    {
        var userId = await GetUserIdAsync();
        if (userId is null)
        {
            return;
        }

        try
        {
            var rows = await _repository.GetGoalsAsync(userId);
            var entries = await _profitRepository.GetVisibleAsync(userId);
            var asOf = DateOnly.FromDateTime(DateTime.Today);
            var goals = rows
                .Where(row => !row.IsDeleted)
                .Select(row => row.ToModel())
                .ToArray();
            var progressList = SavingsGoalCalculator.Track(goals, entries, asOf);
            var rowById = rows.ToDictionary(row => row.GoalId, row => row);

            GoalProgressItems.Clear();
            foreach (var progress in progressList)
            {
                rowById.TryGetValue(progress.Goal.GoalId, out var row);
                GoalProgressItems.Add(new GoalProgressItem(progress, row));
            }

            OnPropertyChanged(nameof(GoalProgressItems));
            OnPropertyChanged(nameof(HasGoalProgressItems));
        }
        catch (Exception)
        {
            // Keep list empty — next reload will retry.
        }
    }

    public async Task SaveGoalAsync(DeviceIdentityService deviceIdentity)
    {
        var userId = await GetUserIdAsync();
        if (userId is null)
        {
            await ShowMessageAsync(UiText.Get("T140"));
            return;
        }

        var title = GoalTitleInput.Trim();
        var target = ParseMinor(GoalTargetInput);
        if (title.Length == 0 || target <= 0)
        {
            await ShowMessageAsync(UiText.Get("T139"));
            return;
        }

        var row = new SavingsGoalRow
        {
            GoalId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            Title = title,
            Category = GoalCategoryInput.Trim(),
            TargetMinor = target,
            SavedMinor = ParseMinor(GoalAlreadySavedInput),
            StartDateTicks = DateTime.UtcNow.Ticks,
            DeadlineTicks = GoalDeadline.Date.Ticks,
            UpdatedAtUtcTicks = DateTime.UtcNow.Ticks,
            Version = 1,
            IsDeleted = false,
            DeviceId = deviceIdentity.GetOrCreate(),
        };

        await _repository.UpsertGoalAsync(userId, row);
        GoalTitleInput = string.Empty;
        GoalCategoryInput = string.Empty;
        GoalTargetInput = string.Empty;
        GoalAlreadySavedInput = string.Empty;
        OnPropertyChanged(nameof(GoalTitleInput));
        OnPropertyChanged(nameof(GoalCategoryInput));
        OnPropertyChanged(nameof(GoalTargetInput));
        OnPropertyChanged(nameof(GoalAlreadySavedInput));
        await ShowMessageAsync(UiText.Get("T614"));
        await ReloadAsync();
    }

    public async Task DeleteGoalAsync(GoalProgressItem item)
    {
        try
        {
            var userId = await GetUserIdAsync();
            if (userId is null)
            {
                return;
            }

            var row = item.Row;
            var updated = new SavingsGoalRow
            {
                GoalId = row.GoalId,
                UserId = row.UserId,
                Title = row.Title,
                Category = row.Category,
                TargetMinor = row.TargetMinor,
                SavedMinor = row.SavedMinor,
                StartDateTicks = row.StartDateTicks,
                DeadlineTicks = row.DeadlineTicks,
                UpdatedAtUtcTicks = DateTime.UtcNow.Ticks,
                Version = row.Version + 1,
                IsDeleted = true,
                DeviceId = row.DeviceId,
            };
            await _repository.UpsertGoalAsync(userId, updated);
            await ShowMessageAsync(UiText.Get("T460"));
            await ReloadAsync();
        }
        catch (Exception)
        {
            // Delete ignored — next reload restores state.
        }
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

    private async Task ShowMessageAsync(string message)
    {
        await _page.DisplayAlertAsync(UiText.Get("T120"), message, UiText.Get("T122"));
    }
}

public sealed class GoalProgressItem
{
    private readonly SavingsGoalProgress _progress;

    public GoalProgressItem(SavingsGoalProgress progress, SavingsGoalRow? row)
    {
        _progress = progress;
        Row = row ?? new SavingsGoalRow();
        var deadline = progress.Goal.Deadline;
        Title = progress.Goal.Title;
        Category = progress.Goal.Category;
        CategoryLabel = string.IsNullOrWhiteSpace(progress.Goal.Category) ? UiText.Get("T615") : progress.Goal.Category;
        SavedText = progress.SavedText;
        RemainingText = progress.RemainingText;
        DeadlineText = deadline.ToString("D");
    }

    public string Title { get; }
    public string Category { get; }
    public string CategoryLabel { get; }
    public string SavedText { get; }
    public string RemainingText { get; }
    public string DeadlineText { get; }
    public SavingsGoalRow Row { get; }
    public double ProgressPercent => _progress.ProgressPercent / 100.0;
    public string ProgressPercentText => $"{Math.Round(_progress.ProgressPercent):F0}%";
    public string ProgressColor => _progress.IsComplete ? "#137A53"
        : _progress.IsOnTrack ? "#2F7DB8" : "#C2413A";
    public string RemainingColor => _progress.RemainingMinor <= 0 ? "#137A53" : "#A16207";
    public string StatusLabel => _progress.IsComplete ? UiText.Get("T616")
        : _progress.IsOnTrack ? UiText.Get("T617") : UiText.Get("T618");
    public string StatusColor => _progress.IsComplete ? "#137A53"
        : _progress.IsOnTrack ? "#2F7DB8" : "#C2413A";
}
