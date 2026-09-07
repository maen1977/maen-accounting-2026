using Maen.Accounting.App.Services;
using Maen.Accounting.App.ViewModels;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using AndroidTabbedPage = Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage;

namespace Maen.Accounting.App.Views;

/// <summary>Personal wallet workspace kept separate from individual and business accounting scopes.</summary>
public sealed class WalletTabbedPage : Microsoft.Maui.Controls.TabbedPage
{
    private readonly PersonalEntryPage _entryPage;

    public WalletTabbedPage(
        PersonalDashboardPage dashboardPage,
        PersonalEntryPage entryPage,
        PlanningPage planningPage,
        SavingsGoalsPage savingsGoalsPage,
        ReportsPage reportsPage,
        SettingsPage settingsPage,
        MainStateViewModel state)
    {
        _entryPage = entryPage;
        FlowDirection = UiText.Language == AppLanguage.English ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        Title = UiText.Language == AppLanguage.English ? "My Wallet" : "محفظتي";
        BarBackgroundColor = Color.FromArgb("#17365D");
        BarTextColor = Colors.White;
        SelectedTabColor = Color.FromArgb("#0F9B8E");
        UnselectedTabColor = Color.FromArgb("#B6C6D8");
        AndroidTabbedPage.SetToolbarPlacement(this, ToolbarPlacement.Bottom);

        dashboardPage.Title = UiText.Language == AppLanguage.English ? "Summary" : "الملخص";
        entryPage.Title = UiText.Language == AppLanguage.English ? "Ledger" : "الدفتر";
        planningPage.Title = UiText.Get("T451");
        savingsGoalsPage.Title = UiText.Get("T600");
        reportsPage.Title = UiText.Get("T022");
        settingsPage.Title = UiText.Get("T018");

        Children.Add(dashboardPage);
        Children.Add(entryPage);
        Children.Add(planningPage);
        Children.Add(savingsGoalsPage);
        Children.Add(reportsPage);
        Children.Add(settingsPage);
        state.EntryEditorRequested += (_, _) => CurrentPage = _entryPage;
    }
}
