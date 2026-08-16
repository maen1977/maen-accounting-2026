using Maen.Accounting.App.ViewModels;
using Maen.Accounting.Core.Models;

namespace Maen.Accounting.App.Views;

public partial class BusinessReportsPage : ContentPage
{
    private readonly BusinessReportsViewModel _viewModel;

    public BusinessReportsPage(BusinessReportsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.LoadAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T293"), exception.Message, UiText.Get("T122"));
        }
    }

    private async void OnExportCsvClicked(object? sender, EventArgs e)
    {
        try
        {
            var csv = _viewModel.ExportCsv();
            if (string.IsNullOrEmpty(csv))
            {
                await DisplayAlertAsync(UiText.Get("T503"), UiText.Get("T080"), UiText.Get("T122"));
                return;
            }
            await Clipboard.SetTextAsync(csv);
            await DisplayAlertAsync(UiText.Get("T503"), UiText.Get("T538"), UiText.Get("T122"));
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T293"), exception.Message, UiText.Get("T122"));
        }
    }
}
