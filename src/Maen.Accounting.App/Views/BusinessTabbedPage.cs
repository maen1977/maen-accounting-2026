using Maen.Accounting.App.Services;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using AndroidTabbedPage = Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage;

namespace Maen.Accounting.App.Views;

public sealed class BusinessTabbedPage : Microsoft.Maui.Controls.TabbedPage
{
    public BusinessTabbedPage(
        BusinessContactsPage contactsPage,
        BusinessInvoicesPage invoicesPage,
        BusinessPaymentsPage paymentsPage)
    {
        FlowDirection = UiText.Language == AppLanguage.English
            ? FlowDirection.LeftToRight
            : FlowDirection.RightToLeft;
        Title = UiText.Get("T040");
        BarBackgroundColor = Color.FromArgb("#0B1F33");
        BarTextColor = Colors.White;
        SelectedTabColor = Color.FromArgb("#C8A45D");
        UnselectedTabColor = Color.FromArgb("#AFC0D3");
        AndroidTabbedPage.SetToolbarPlacement(this, ToolbarPlacement.Bottom);

        contactsPage.Title = UiText.Get("T073");
        invoicesPage.Title = UiText.Get("T074");
        paymentsPage.Title = UiText.Get("T065");

        Children.Add(contactsPage);
        Children.Add(invoicesPage);
        Children.Add(paymentsPage);
    }
}
