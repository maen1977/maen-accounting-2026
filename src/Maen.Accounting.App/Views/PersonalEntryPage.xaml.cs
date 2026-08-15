using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class PersonalEntryPage : ContentPage
{
    private readonly MainStateViewModel _state;

    public PersonalEntryPage(MainStateViewModel state)
    {
        InitializeComponent();
        BindingContext = _state = state;
    }

    private void OnSalaryQuickClicked(object? sender, EventArgs e)
    {
        _state.SelectedMovementType = UiText.Get("T332");
        _state.SelectedDirection = UiText.Get("T151");
    }

    private void OnDailyIncomeQuickClicked(object? sender, EventArgs e)
    {
        _state.SelectedMovementType = UiText.Get("T334");
        _state.SelectedDirection = UiText.Get("T151");
    }

    private void OnPurchaseQuickClicked(object? sender, EventArgs e)
    {
        _state.SelectedMovementType = UiText.Get("T145");
        _state.SelectedDirection = UiText.Get("T150");
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var button = sender as Button;
        if (button is not null) button.IsEnabled = false;

        try
        {
            await _state.SavePersonalCurrentAsync();
            await DisplayAlertAsync(UiText.Get("T120"), _state.StatusMessage, UiText.Get("T122"));
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T121"), exception.Message, UiText.Get("T122"));
        }
        finally
        {
            if (button is not null) button.IsEnabled = true;
        }
    }
}
