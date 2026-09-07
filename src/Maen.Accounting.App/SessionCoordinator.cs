using Microsoft.Extensions.DependencyInjection;
using Maen.Accounting.App.Data;
using Maen.Accounting.App.Services;
using Maen.Accounting.App.ViewModels;
using Maen.Accounting.App.Views;
using Maen.Accounting.Core.Models;

namespace Maen.Accounting.App;

public sealed class SessionCoordinator
{
    private readonly IServiceProvider _services;
    private readonly AuthSessionStore _sessionStore;
    private readonly FirebaseAuthService _authService;
    private readonly UserDatabaseFactory _databaseFactory;
    private readonly AppPreferencesService _preferences;
    private static readonly TimeSpan StartupInitializationTimeout = TimeSpan.FromSeconds(30);
    private Window? _window;

    public SessionCoordinator(
        IServiceProvider services,
        AuthSessionStore sessionStore,
        FirebaseAuthService authService,
        UserDatabaseFactory databaseFactory,
        AppPreferencesService preferences)
    {
        _services = services;
        _sessionStore = sessionStore;
        _authService = authService;
        _databaseFactory = databaseFactory;
        _preferences = preferences;
    }

    public async Task StartAsync(Window window)
    {
        _window = window;
        _preferences.ApplyCulture();
        var session = await _sessionStore.LoadAsync();
        if (session is { IsLocal: false } && session.NeedsRefresh(DateTimeOffset.UtcNow))
        {
            try
            {
                session = await _authService.RefreshAsync(session);
                await _sessionStore.SaveAsync(session);
            }
            catch
            {
                session = null;
                await _sessionStore.ClearAsync();
            }
        }

        if (session is null)
        {
            ShowLogin();
        }
        else if (!_preferences.IsConfigured)
        {
            ShowOnboarding();
        }
        else
        {
            await ShowMainAsync(session);
        }
    }

    public async Task CompleteLoginAsync(AuthSession session)
    {
        await _sessionStore.SaveAsync(session);
        if (!_preferences.IsConfigured)
        {
            ShowOnboarding();
            return;
        }

        await ShowMainAsync(session);
    }

    public async Task ShowCurrentSessionAsync(Window window)
    {
        _window = window;
        var session = await _sessionStore.LoadAsync();
        if (session is null)
        {
            ShowLogin();
            return;
        }

        if (!_preferences.IsConfigured)
        {
            ShowOnboarding();
            return;
        }

        await ShowMainAsync(session);
    }

    public void ShowOnboarding()
    {
        var onboarding = _services.GetRequiredService<OnboardingPage>();
        onboarding.Completed += OnOnboardingCompleted;
        SetPage(onboarding);
    }

    public void ShowLogin()
    {
        _databaseFactory.ClearActiveConnection();
        var login = _services.GetRequiredService<LoginPage>();
        login.Authenticated += OnAuthenticated;
        SetPage(new NavigationPage(login));
    }

    private async Task ShowMainAsync(AuthSession session)
    {
        var state = _services.GetRequiredService<MainStateViewModel>();
        var accounting = _services.GetRequiredService<AccountingViewModel>();
        var business = _services.GetRequiredService<BusinessViewModel>();
        var reports = _services.GetRequiredService<BusinessReportsViewModel>();
        state.SignedOut -= OnSignedOut;
        state.SignedOut += OnSignedOut;

        // تبديل الشاشة قبل تحميل البيانات يمنع بقاء شاشة البداية في حالة دوران صامتة.
        // لكل تجربة مساحة SQLite ومسار Firestore مستقلان: personal أو business أو wallet.
        var experience = _preferences.Experience;
        var isLightweightExperience = experience is AccountExperience.Personal or AccountExperience.Wallet;

        try
        {
            SetPage(experience switch
            {
                AccountExperience.Personal => _services.GetRequiredService<PersonalTabbedPage>(),
                AccountExperience.Wallet => _services.GetRequiredService<WalletTabbedPage>(),
                _ => _services.GetRequiredService<MainTabbedPage>()
            });

            if (isLightweightExperience)
            {
                await state.InitializeAsync(session).WaitAsync(StartupInitializationTimeout);
            }
            else
            {
                // SQLite-net يستخدم اتصال قاعدة المستخدم نفسه؛ التهيئة المتوازية كانت تسمح
                // لتبويبات الشركات والتقارير بالتنافس على القراءة الأولى وإظهار دوران مستمر.
                await state.InitializeAsync(session).WaitAsync(StartupInitializationTimeout);
                await accounting.InitializeAsync(session).WaitAsync(StartupInitializationTimeout);
                await business.InitializeAsync(session).WaitAsync(StartupInitializationTimeout);
                await reports.InitializeAsync(session).WaitAsync(StartupInitializationTimeout);
            }

            if (!string.IsNullOrWhiteSpace(state.StartupCloudDataMessage))
            {
                await ShowStartupAlertAsync(state.StartupCloudDataMessage);
            }
        }
        catch (TimeoutException)
        {
            await ShowStartupAlertAsync(UiText.Get("T226"));
        }
        catch (Exception exception)
        {
            await ShowStartupAlertAsync(UiText.Format("T227", CloudSyncExceptionFormatter.GetDetail(exception)));
        }
    }

    private async Task ShowStartupAlertAsync(string message)
    {
        if (_window?.Page is Page page)
        {
            await page.DisplayAlertAsync(UiText.Get("T225"), message, UiText.Get("T122"));
        }
    }

    private async void OnOnboardingCompleted(object? sender, EventArgs args)
    {
        if (sender is OnboardingPage onboarding)
        {
            onboarding.Completed -= OnOnboardingCompleted;
        }

        if (_window is not null)
        {
            await StartAsync(_window);
        }
    }

    private async void OnAuthenticated(object? sender, AuthSession session)
    {
        try
        {
            await CompleteLoginAsync(session);
        }
        catch (Exception exception)
        {
            if (_window?.Page is Page page)
            {
                await page.DisplayAlertAsync(UiText.Get("T228"), CloudSyncExceptionFormatter.GetDetail(exception), UiText.Get("T122"));
            }
        }
    }

    private void OnSignedOut(object? sender, EventArgs args) => ShowLogin();

    private void SetPage(Page page)
    {
        if (_window is null)
        {
            throw new InvalidOperationException(UiText.Get("T241"));
        }
        _window.Page = page;
    }
}
