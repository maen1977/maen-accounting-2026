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

    private void OnEditPaymentClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: PaymentItemViewModel item })
        {
            _viewModel.EditPayment(item);
        }
    }

    private async void OnDeletePaymentClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not PaymentItemViewModel item) return;
        var confirmed = await DisplayAlertAsync(UiText.Get("T490"), UiText.Format("T842", item.Number), UiText.Get("T177"), UiText.Get("T178"));
        if (!confirmed) return;
        try
        {
            await _viewModel.DeletePaymentAsync(item);
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T294"), exception.Message, UiText.Get("T122"));
        }
    }
}
