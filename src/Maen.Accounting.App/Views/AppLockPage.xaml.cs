using Maen.Accounting.App.Services;

namespace Maen.Accounting.App.Views;

/// <summary>Lock screen shown when the app was backgrounded beyond the configured idle window.</summary>
public partial class AppLockPage : ContentPage
{
    public event EventHandler? Unlocked;

    private int _failedAttempts;

    public AppLockPage()
    {
        InitializeComponent();
    }

    private async void OnUnlockClicked(object? sender, EventArgs e)
    {
        var pin = PinEntry.Text ?? string.Empty;
        if (!AppLockService.IsValidPin(pin))
        {
            ShowError(UiText.Get("T805"));
            return;
        }

        if (await AppLockService.VerifyPinAsync(pin))
        {
            Unlocked?.Invoke(this, EventArgs.Empty);
            return;
        }

        _failedAttempts++;
        if (_failedAttempts >= 10)
        {
            ShowError(UiText.Get("T806"));
            return;
        }

        ShowError(UiText.Get("T807"));
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        PinEntry.Text = string.Empty;
        PinEntry.Focus();
    }
}
