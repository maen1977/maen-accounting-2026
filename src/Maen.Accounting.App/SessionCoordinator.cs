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
    private static readonly TimeSpan StartupInitializationTimeout = TimeSpan.FromSeconds(30);
    private Window? _window;

    public SessionCoordinator(
        IServiceProvider services,
        AuthSessionStore sessionStore,
        FirebaseAuthService authService,
        UserDatabaseFactory databaseFactory)
    {
        _services = services;
        _sessionStore = sessionStore;
        _authService = authService;
        _databaseFactory = databaseFactory;
    }

    public async Task StartAsync(Window window)
    {
        _window = window;
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
        else
        {
            await ShowMainAsync(session);
        }
    }

    public async Task CompleteLoginAsync(AuthSession session)
    {
        await _sessionStore.SaveAsync(session);
        await ShowMainAsync(session);
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
        state.SignedOut -= OnSignedOut;
        state.SignedOut += OnSignedOut;

        // تبديل الشاشة قبل تحميل البيانات يمنع بقاء شاشة البداية في حالة دوران صامتة.
        SetPage(_services.GetRequiredService<MainTabbedPage>());

        try
        {
            await Task.WhenAll(
                state.InitializeAsync(session),
                accounting.InitializeAsync(session),
                business.InitializeAsync(session))
                .WaitAsync(StartupInitializationTimeout);
        }
        catch (TimeoutException)
        {
            await ShowStartupAlertAsync(
                "تم فتح البرنامج، لكن تحميل بعض البيانات استغرق وقتًا أطول من المتوقع. يمكنك متابعة العمل وتحديث الصفحات لاحقًا.");
        }
        catch (Exception exception)
        {
            await ShowStartupAlertAsync(
                $"تم فتح البرنامج، لكن تعذر تحميل بعض البيانات. يمكنك متابعة العمل ثم إعادة المحاولة من داخل الصفحات.\n\nالتفاصيل: {exception.Message}");
        }
    }

    private async Task ShowStartupAlertAsync(string message)
    {
        if (_window?.Page is Page page)
        {
            await page.DisplayAlertAsync("تنبيه تحميل البيانات", message, "حسنًا");
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
                await page.DisplayAlertAsync("تعذر فتح البرنامج", exception.Message, "حسنًا");
            }
        }
    }

    private void OnSignedOut(object? sender, EventArgs args) => ShowLogin();

    private void SetPage(Page page)
    {
        if (_window is null)
        {
            throw new InvalidOperationException("Application window is not ready.");
        }
        _window.Page = page;
    }
}
