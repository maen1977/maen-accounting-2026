using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class BusinessContactsPage : ContentPage
{
    private readonly BusinessViewModel _viewModel;

    public BusinessContactsPage(BusinessViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private async void OnSaveContactClicked(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.SaveContactAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T292"), exception.Message, UiText.Get("T122"));
        }
    }

    private async void OnToggleContactClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not ContactItemViewModel item) return;
        try
        {
            await _viewModel.ToggleContactAsync(item);
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T292"), exception.Message, UiText.Get("T122"));
        }
    }
}
