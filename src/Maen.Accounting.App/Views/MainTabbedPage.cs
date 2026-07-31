using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public sealed class MainTabbedPage : TabbedPage
{
    private readonly EntryPage _entryPage;

    public MainTabbedPage(
        MainStateViewModel state,
        DashboardPage dashboardPage,
        EntryPage entryPage,
        ReportsPage reportsPage,
        SettingsPage settingsPage)
    {
        FlowDirection = FlowDirection.RightToLeft;
        BarBackgroundColor = Colors.White;
        BarTextColor = Color.FromArgb("#667085");
        SelectedTabColor = Color.FromArgb("#0E9F6E");
        UnselectedTabColor = Color.FromArgb("#667085");
        _entryPage = entryPage;

        Children.Add(dashboardPage);
        Children.Add(entryPage);
        Children.Add(reportsPage);
        Children.Add(settingsPage);
        state.EntryEditorRequested += (_, _) => CurrentPage = _entryPage;
    }
}
