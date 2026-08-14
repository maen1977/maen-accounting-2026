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

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var button = sender as Button;
        if (button is not null) button.IsEnabled = false;

        try
        {
            // في المسار الشخصي نستخدم حقلي الدخل والمصروف فقط؛ تكلفة البضاعة تخص حسابات الشركات.
            _state.CostInput = "0";
            await _state.SaveCurrentAsync();
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
