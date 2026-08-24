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

    private async void OnSaveAccountClicked(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.SaveAccountAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T284"), exception.Message, UiText.Get("T122"));
        }
    }

    private void OnEditAccountClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: AccountItemViewModel item })
        {
            _viewModel.BeginEditAccount(item);
        }
    }

    private async void OnDeleteAccountClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: AccountItemViewModel item }) return;
        if (!await DisplayAlertAsync(UiText.Get("T490"), UiText.Format("T846", item.Name), UiText.Get("T177"), UiText.Get("T178"))) return;
        try
        {
            await _viewModel.DeleteAccountAsync(item);
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

    private void OnEditJournalEntryClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: JournalEntryItemViewModel item })
        {
            _viewModel.BeginEditJournalEntry(item);
        }
    }

    private async void OnDeleteJournalEntryClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: JournalEntryItemViewModel item }) return;
        if (!await DisplayAlertAsync(UiText.Get("T490"), UiText.Format("T847", item.EntryNumber), UiText.Get("T177"), UiText.Get("T178"))) return;
        try
        {
            await _viewModel.DeleteJournalEntryAsync(item);
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
