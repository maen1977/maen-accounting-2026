using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class BusinessInvoicesPage : ContentPage
{
    private readonly BusinessViewModel _viewModel;

    public BusinessInvoicesPage(BusinessViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private async void OnSaveInvoiceClicked(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.SaveInvoiceAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T293"), exception.Message, UiText.Get("T122"));
        }
    }
}
