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
        SubmitButton.Text = _registerMode ? "إنشاء الحساب" : "تسجيل الدخول";
        ModeButton.Text = _registerMode ? "لدي حساب بالفعل" : "إنشاء حساب جديد";
        StatusLabel.Text = string.Empty;
    }

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;
        if (_registerMode && password != (ConfirmEntry.Text ?? string.Empty))
        {
            StatusLabel.Text = "كلمتا المرور غير متطابقتين.";
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

    private async void OnLocalClicked(object? sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            StatusLabel.Text = "أدخل بريدًا صحيحًا لتعريف البيانات المحلية.";
            return;
        }

        await RunBusyAsync(() =>
        {
            Authenticated?.Invoke(this, _authService.CreateLocalSession(email));
            return Task.CompletedTask;
        });
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        try
        {
            SetBusy(true);
            StatusLabel.Text = string.Empty;
            await action();
        }
        catch (Exception exception)
        {
            StatusLabel.Text = exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        BusyIndicator.IsVisible = busy;
        BusyIndicator.IsRunning = busy;
        SubmitButton.IsEnabled = !busy;
        ModeButton.IsEnabled = !busy;
    }
}
