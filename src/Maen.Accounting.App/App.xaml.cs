using Maen.Accounting.App.Services;
using Maen.Accounting.App.Views;

namespace Maen.Accounting.App;

public partial class App : Application
{
    private readonly SessionCoordinator _coordinator;
    private Window? _window;
    private DateTime _backgroundedAtUtc;

    public App(SessionCoordinator coordinator)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
        _coordinator = coordinator;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _window = new Window(new SplashPage())
        {
            Title = "Maen Accounting"
        };
        _ = StartAsync();
        return _window;
    }

    private async Task StartAsync()
    {
        if (_window is not null)
        {
            await _coordinator.StartAsync(_window);
        }
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        _backgroundedAtUtc = DateTime.UtcNow;
    }

    protected override void OnResume()
    {
        base.OnResume();
        _ = EnforceAppLockAsync();
    }

    /// <summary>Presents the lock screen when the app was backgrounded beyond the idle window.</summary>
    private async Task EnforceAppLockAsync()
    {
        try
        {
            if (!await AppLockService.IsEnabledAsync() || !await AppLockService.HasPinAsync())
            {
                return;
            }

            var idleMinutes = await AppLockService.GetIdleMinutesAsync();
            var elapsed = DateTime.UtcNow - _backgroundedAtUtc;
            if (elapsed < TimeSpan.FromMinutes(idleMinutes))
            {
                return;
            }

            if (_window is null)
            {
                return;
            }

            await _window.Dispatcher.DispatchAsync(() =>
            {
                if (_window is not null)
                {
                    var lockPage = new AppLockPage();
                    lockPage.Unlocked += OnUnlocked;
                    _window.Page = lockPage;
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppLock] Enforcement skipped: {ex.Message}");
        }
    }

    private void OnUnlocked(object? sender, EventArgs args)
    {
        if (sender is AppLockPage lockPage)
        {
            lockPage.Unlocked -= OnUnlocked;
        }

        if (_window is not null)
        {
            _ = _coordinator.ShowCurrentSessionAsync(_window);
        }
    }
}

