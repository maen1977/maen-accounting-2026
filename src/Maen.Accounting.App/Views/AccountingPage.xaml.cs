using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class AccountingPage : ContentPage
{
    private readonly AccountingViewModel _viewModel;

    public AccountingPage(AccountingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.ReloadAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T284"), exception.Message, UiText.Get("T122"));
        }
    }

    private async void OnSaveJournalClicked(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.SaveJournalEntryAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T285"), exception.Message, UiText.Get("T122"));
        }
    }

    private async void OnMigrateClicked(object? sender, EventArgs e)
    {
        if (!await DisplayAlertAsync(
                UiText.Get("T286"),
                UiText.Get("T287"),
                UiText.Get("T288"),
                UiText.Get("T289")))
        {
            return;
        }

        try
        {
            await _viewModel.MigrateLegacyEntriesAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T290"), exception.Message, UiText.Get("T122"));
        }
    }
}
