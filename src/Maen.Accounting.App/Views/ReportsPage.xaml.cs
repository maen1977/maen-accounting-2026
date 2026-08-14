using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class ReportsPage : ContentPage
{
    private readonly MainStateViewModel _state;

    public ReportsPage(MainStateViewModel state)
    {
        InitializeComponent();
        BindingContext = _state = state;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshBusinessSummaryIfNeededAsync();
    }

    private async void OnCurrentMonthClicked(object? sender, EventArgs e)
    {
        var today = DateTime.Today;
        _state.ReportMonth = new DateTime(today.Year, today.Month, 1);
        await RefreshBusinessSummaryIfNeededAsync();
    }

    private async void OnPreviousMonthClicked(object? sender, EventArgs e)
    {
        _state.ReportMonth = _state.ReportMonth.AddMonths(-1);
        await RefreshBusinessSummaryIfNeededAsync();
    }

    private async void OnCurrentQuarterClicked(object? sender, EventArgs e)
    {
        _state.SelectCurrentQuarter();
        await RefreshBusinessSummaryIfNeededAsync();
    }

    private async void OnCurrentYearClicked(object? sender, EventArgs e)
    {
        _state.SelectCurrentYear();
        await RefreshBusinessSummaryIfNeededAsync();
    }

    private async void OnReportDateSelected(object? sender, DateChangedEventArgs e)
    {
        var selectedDate = e.NewDate ?? DateTime.Today;
        _state.ReportMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1);
        await RefreshBusinessSummaryIfNeededAsync();
    }

    private async Task RefreshBusinessSummaryIfNeededAsync()
    {
        if (_state.IsBusinessExperience && !string.IsNullOrWhiteSpace(_state.UserEmail))
        {
            await _state.RefreshReportBusinessSummaryAsync();
        }
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = UiText.Get("T201"),
            Text = _state.BuildReportShareText()
        });
    }

    private void OnEditClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: ProfitEntryItemViewModel item })
        {
            _state.BeginEdit(item);
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ProfitEntryItemViewModel item }) return;
        if (!await DisplayAlertAsync(UiText.Get("T175"), UiText.Format("T176", item.DateText), UiText.Get("T177"), UiText.Get("T178"))) return;
        try { await _state.DeleteAsync(item); }
        catch (Exception exception) { await DisplayAlertAsync(UiText.Get("T179"), exception.Message, UiText.Get("T180")); }
    }
}
