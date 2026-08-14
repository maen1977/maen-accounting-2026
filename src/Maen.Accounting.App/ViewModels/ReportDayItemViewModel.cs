using System.Globalization;
using Maen.Accounting.Core.Services;
using Maen.Accounting.App.Services;

namespace Maen.Accounting.App.ViewModels;

public sealed class ReportDayItemViewModel
{
    public ReportDayItemViewModel(DateOnly date, long salesMinor, long expensesMinor, long netMinor, double netProgress)
    {
        Date = date;
        SalesMinor = salesMinor;
        ExpensesMinor = expensesMinor;
        NetMinor = netMinor;
        NetProgress = netProgress;
    }

    public DateOnly Date { get; }
    public long SalesMinor { get; }
    public long ExpensesMinor { get; }
    public long NetMinor { get; }
    public double NetProgress { get; }
    public string DateText => Date.ToDateTime(TimeOnly.MinValue).ToString(
        "dd MMM",
        UiText.Language == AppLanguage.English ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo("ar-SA"));
    public string NetText => Money.Format(NetMinor);
    public string SalesText => Money.Format(SalesMinor);
    public string ExpensesText => Money.Format(ExpensesMinor);
    public string SalesSummaryText => UiText.Format("T172", SalesText);
    public string ExpensesSummaryText => UiText.Format("T174", ExpensesText);
    public Color NetColor => NetMinor >= 0
        ? Color.FromArgb("#168A64")
        : Color.FromArgb("#D45B5B");
}
