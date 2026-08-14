using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class BusinessPage : ContentPage
{
    private readonly BusinessViewModel _viewModel;

    public BusinessPage(BusinessViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.ReloadAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("تعذر التحديث", exception.Message, "حسنًا");
        }
    }

    private async void OnSaveContactClicked(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.SaveContactAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("تعذر حفظ جهة الاتصال", exception.Message, "حسنًا");
        }
    }

    private async void OnSaveInvoiceClicked(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.SaveInvoiceAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("تعذر حفظ الفاتورة", exception.Message, "حسنًا");
        }
    }

    private async void OnSavePaymentClicked(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.SavePaymentAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("تعذر حفظ العملية", exception.Message, "حسنًا");
        }
    }
}
