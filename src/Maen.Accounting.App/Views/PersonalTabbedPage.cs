using Maen.Accounting.App.Services;
using Maen.Accounting.App.ViewModels;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using AndroidTabbedPage = Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage;

namespace Maen.Accounting.App.Views;

public sealed class PersonalTabbedPage : Microsoft.Maui.Controls.TabbedPage
{
    private readonly PersonalEntryPage _entryPage;

    public PersonalTabbedPage(
        PersonalDashboardPage dashboardPage,
        PersonalEntryPage entryPage,
        PlanningPage planningPage,
        SavingsGoalsPage savingsGoalsPage,
        ReportsPage reportsPage,
        SettingsPage settingsPage,
        MainStateViewModel state)
    {
        _entryPage = entryPage;
        FlowDirection = UiText.Language == AppLanguage.English
            ? FlowDirection.LeftToRight
            : FlowDirection.RightToLeft;
        Title = UiText.Get("T105");
        BarBackgroundColor = Color.FromArgb("#0B172A");
        BarTextColor = Colors.White;
        SelectedTabColor = Color.FromArgb("#48D597");
        UnselectedTabColor = Color.FromArgb("#A8B4C7");
        AndroidTabbedPage.SetToolbarPlacement(this, ToolbarPlacement.Bottom);

        dashboardPage.Title = UiText.Get("T032");
        entryPage.Title = UiText.Get("T110");
        reportsPage.Title = UiText.Get("T022");
        settingsPage.Title = UiText.Get("T018");
        planningPage.Title = UiText.Get("T451");
        savingsGoalsPage.Title = UiText.Get("T600");

        Children.Add(dashboardPage);
        Children.Add(entryPage);
        Children.Add(planningPage);
        Children.Add(savingsGoalsPage);
        Children.Add(reportsPage);
        Children.Add(settingsPage);
        state.EntryEditorRequested += (_, _) => CurrentPage = _entryPage;
    }
}
