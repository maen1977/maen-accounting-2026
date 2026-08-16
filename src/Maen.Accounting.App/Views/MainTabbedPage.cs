using Maen.Accounting.App.Services;
using Maen.Accounting.App.ViewModels;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using AndroidTabbedPage = Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage;

namespace Maen.Accounting.App.Views;

public sealed class MainTabbedPage : Microsoft.Maui.Controls.TabbedPage
{
    private readonly EntryPage _entryPage;

    public MainTabbedPage(
        MainStateViewModel state,
        DashboardPage dashboardPage,
        AccountingPage accountingPage,
        BusinessPage businessPage,
        EntryPage entryPage,
        ReportsPage reportsPage,
        SettingsPage settingsPage)
    {
        FlowDirection = UiText.Language == AppLanguage.English
            ? FlowDirection.LeftToRight
            : FlowDirection.RightToLeft;
        BarBackgroundColor = Color.FromArgb("#0B1F33");
        BarTextColor = Colors.White;
        SelectedTabColor = Color.FromArgb("#C8A45D");
        UnselectedTabColor = Color.FromArgb("#AFC0D3");
        AndroidTabbedPage.SetToolbarPlacement(this, ToolbarPlacement.Bottom);
        _entryPage = entryPage;

        Children.Add(dashboardPage);
        Children.Add(accountingPage);
        Children.Add(businessPage);
        Children.Add(entryPage);
        Children.Add(reportsPage);
        Children.Add(settingsPage);
        state.EntryEditorRequested += (_, _) => CurrentPage = _entryPage;
    }
}
