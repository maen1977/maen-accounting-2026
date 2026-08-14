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
}
