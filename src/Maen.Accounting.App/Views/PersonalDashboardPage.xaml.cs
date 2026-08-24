using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class PersonalDashboardPage : ContentPage
{
    private readonly MainStateViewModel _state;

    public PersonalDashboardPage(MainStateViewModel state)
    {
        InitializeComponent();
        BindingContext = _state = state;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrWhiteSpace(_state.UserEmail))
        {
            await _state.ReloadAsync();
        }
    }

    private async void OnSaveBudgetClicked(object? sender, EventArgs e)
    {
        try
        {
            _state.SaveMonthlyBudget();
            await DisplayAlertAsync(UiText.Get("T181"), UiText.Get("T188"), UiText.Get("T180"));
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T181"), exception.Message, UiText.Get("T180"));
        }
    }

    private void OnAddClicked(object? sender, EventArgs e) => _state.BeginNewEntry();

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
        try
        {
            await _state.DeleteAsync(item);
            await DisplayAlertAsync(UiText.Get("T833"), UiText.Get("T834"), UiText.Get("T180"));
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T179"), exception.Message, UiText.Get("T180"));
        }
    }

    private void OnClearMovementFilterClicked(object? sender, EventArgs e) => _state.ClearMovementFilter();

    private void OnMovementSearchTextChanged(object? sender, TextChangedEventArgs e) =>
        _state.MovementSearchText = e.NewTextValue ?? string.Empty;
}
