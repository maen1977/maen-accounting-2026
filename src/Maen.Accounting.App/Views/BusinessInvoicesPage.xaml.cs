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

    private void OnEditInvoiceClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not InvoiceItemViewModel item) return;
        _viewModel.EditInvoice(item);
    }

    private async void OnVoidInvoiceClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not InvoiceItemViewModel item) return;
        var confirmed = await DisplayAlertAsync(UiText.Get("T491"), UiText.Get("T491"), UiText.Get("T494"), UiText.Get("T496"));
        if (!confirmed) return;
        try
        {
            await _viewModel.VoidInvoiceAsync(item);
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T293"), exception.Message, UiText.Get("T122"));
        }
    }

    private async void OnDeleteInvoiceClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not InvoiceItemViewModel item) return;
        var confirmed = await DisplayAlertAsync(UiText.Get("T492"), UiText.Get("T492"), UiText.Get("T495"), UiText.Get("T496"));
        if (!confirmed) return;
        try
        {
            await _viewModel.DeleteInvoiceAsync(item);
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T293"), exception.Message, UiText.Get("T122"));
        }
    }
}
