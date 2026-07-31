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
            await DisplayAlertAsync("تم الحفظ", _state.StatusMessage, "حسنًا");
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("تعذر الحفظ", exception.Message, "حسنًا");
        }
        finally
        {
            if (button is not null) button.IsEnabled = true;
        }
    }
}
