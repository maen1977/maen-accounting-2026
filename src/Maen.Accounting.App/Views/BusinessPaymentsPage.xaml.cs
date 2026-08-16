using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class BusinessPaymentsPage : ContentPage
{
    private readonly BusinessViewModel _viewModel;

    public BusinessPaymentsPage(BusinessViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private async void OnSavePaymentClicked(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.SavePaymentAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T294"), exception.Message, UiText.Get("T122"));
        }
    }
}
