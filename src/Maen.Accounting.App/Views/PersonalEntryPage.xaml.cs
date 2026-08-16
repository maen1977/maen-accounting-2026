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

    private void OnSalaryQuickClicked(object? sender, EventArgs e)
    {
        _state.SelectedMovementType = UiText.Get("T332");
        _state.SelectedDirection = UiText.Get("T151");
    }

    private void OnDailyIncomeQuickClicked(object? sender, EventArgs e)
    {
        _state.SelectedMovementType = UiText.Get("T334");
        _state.SelectedDirection = UiText.Get("T151");
    }

    private void OnPurchaseQuickClicked(object? sender, EventArgs e)
    {
        _state.SelectedMovementType = UiText.Get("T145");
        _state.SelectedDirection = UiText.Get("T150");
    }

    private void OnBankDepositQuickClicked(object? sender, EventArgs e)
    {
        _state.SelectedMovementType = UiText.Get("T364");
        _state.SelectedDirection = UiText.Get("T151");
        _state.WalletInput = "bank";
    }

    private void OnBankWithdrawalQuickClicked(object? sender, EventArgs e)
    {
        _state.SelectedMovementType = UiText.Get("T365");
        _state.SelectedDirection = UiText.Get("T150");
        _state.WalletInput = "bank";
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var button = sender as Button;
        if (button is not null) button.IsEnabled = false;

        try
        {
            await _state.SavePersonalCurrentAsync();
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

    private async void OnAttachPhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var options = new PickOptions { PickerTitle = UiText.Get("T722") };
            var result = await FilePicker.Default.PickAsync(options);
            if (result is null)
            {
                return;
            }

            using var stream = await result.OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            if (memory.Length > 4 * 1024 * 1024)
            {
                await DisplayAlertAsync(UiText.Get("T121"), UiText.Get("T736"), UiText.Get("T122"));
                return;
            }

            _state.AttachmentBase64 = Convert.ToBase64String(memory.ToArray());
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T121"), exception.Message, UiText.Get("T122"));
        }
    }

    private void OnRemoveAttachmentClicked(object? sender, EventArgs e)
    {
        _state.AttachmentBase64 = string.Empty;
    }
}
