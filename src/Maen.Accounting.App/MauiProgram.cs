using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Maen.Accounting.App.Data;
using Maen.Accounting.App.Services;
using Maen.Accounting.App.ViewModels;
using Maen.Accounting.App.Views;

namespace Maen.Accounting.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var arabicCulture = CultureInfo.GetCultureInfo("ar-JO");
        CultureInfo.DefaultThreadCurrentCulture = arabicCulture;
        CultureInfo.DefaultThreadCurrentUICulture = arabicCulture;

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();


        builder.Services.AddSingleton(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(25)
        });
        builder.Services.AddSingleton<FirebaseOptions>();
        builder.Services.AddSingleton<AuthSessionStore>();
        builder.Services.AddSingleton<AppPreferencesService>();
        builder.Services.AddSingleton<FirebaseAuthService>();
        builder.Services.AddSingleton<AuthTokenProvider>();
        builder.Services.AddSingleton<DeviceIdentityService>();
        builder.Services.AddSingleton<UserDatabaseFactory>();
        builder.Services.AddSingleton<ProfitEntryRepository>();
        builder.Services.AddSingleton<AccountingRepository>();
        builder.Services.AddSingleton<BusinessRepository>();
        builder.Services.AddSingleton<BackupService>();
        builder.Services.AddSingleton<FirestoreSyncService>();
        builder.Services.AddSingleton<MainStateViewModel>();
        builder.Services.AddSingleton<AccountingViewModel>();
        builder.Services.AddSingleton<BusinessViewModel>();
        builder.Services.AddSingleton<SessionCoordinator>();

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<PersonalDashboardPage>();
        builder.Services.AddTransient<AccountingPage>();
        builder.Services.AddTransient<BusinessPage>();
        builder.Services.AddTransient<EntryPage>();
        builder.Services.AddTransient<PersonalEntryPage>();
        builder.Services.AddTransient<ReportsPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<MainTabbedPage>();
        builder.Services.AddTransient<PersonalTabbedPage>();

        return builder.Build();
    }
}
