using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class EntryPage : ContentPage
{
    private readonly MainStateViewModel _state;

    public EntryPage(MainStateViewModel state)
    {
        InitializeComponent();
        BindingContext = _state = state;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var button = sender as Button;
        if (button is not null) button.IsEnabled = false;
        try
        {
            await _state.SaveCurrentAsync();
            await DisplayAlertAsync(UiText.Get("T295"), _state.StatusMessage, UiText.Get("T122"));
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T296"), exception.Message, UiText.Get("T122"));
        }
        finally
        {
            if (button is not null) button.IsEnabled = true;
        }
    }
}
