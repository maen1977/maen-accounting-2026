using Maen.Accounting.App.Services;
using Maen.Accounting.Core.Models;

namespace Maen.Accounting.App.Views;

public partial class LoginPage : ContentPage
{
    private readonly FirebaseAuthService _authService;
    private bool _registerMode;

    public LoginPage(FirebaseAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    public event EventHandler<AuthSession>? Authenticated;

    private void OnModeClicked(object? sender, EventArgs e)
    {
        _registerMode = !_registerMode;
        ConfirmLabel.IsVisible = _registerMode;
        ConfirmEntry.IsVisible = _registerMode;
        ForgotPasswordButton.IsVisible = !_registerMode;
        SubmitButton.Text = _registerMode ? UiText.Get("T218") : UiText.Get("T056");
        ModeButton.Text = _registerMode ? UiText.Get("T219") : UiText.Get("T011");
        SetStatus(string.Empty);
    }

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;
        if (_registerMode && password != (ConfirmEntry.Text ?? string.Empty))
        {
            SetStatus(UiText.Get("T222"));
            return;
        }

        await RunBusyAsync(async () =>
        {
            var session = _registerMode
                ? await _authService.RegisterAsync(email, password)
                : await _authService.SignInAsync(email, password);
            Authenticated?.Invoke(this, session);
        });
    }

    private async void OnForgotPasswordClicked(object? sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            SetStatus(UiText.Get("T223"));
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _authService.SendPasswordResetEmailAsync(email);
            SetStatus(UiText.Get("T224"), success: true);
        });
    }

    private async void OnLocalClicked(object? sender, EventArgs e)
    {
        await RunBusyAsync(() =>
        {
            Authenticated?.Invoke(this, _authService.CreateLocalDeviceSession());
            return Task.CompletedTask;
        });
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        try
        {
            SetBusy(true);
            SetStatus(string.Empty);
            await action();
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetStatus(string message, bool success = false)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = success
            ? (Color)Application.Current!.Resources["PrimaryDark"]
            : (Color)Application.Current!.Resources["Danger"];
    }

    private void SetBusy(bool busy)
    {
        BusyIndicator.IsVisible = busy;
        BusyIndicator.IsRunning = busy;
        SubmitButton.IsEnabled = !busy;
        ModeButton.IsEnabled = !busy;
        LocalButton.IsEnabled = !busy;
        ForgotPasswordButton.IsEnabled = !busy;
        EmailEntry.IsEnabled = !busy;
        PasswordEntry.IsEnabled = !busy;
        ConfirmEntry.IsEnabled = !busy;
    }
}
