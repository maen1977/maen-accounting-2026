using Maen.Accounting.App.Views;

namespace Maen.Accounting.App;

public partial class App : Application
{
    private readonly SessionCoordinator _coordinator;
    private Window? _window;

    public App(SessionCoordinator coordinator)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
        _coordinator = coordinator;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _window = new Window(new SplashPage());
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
}

