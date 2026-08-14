using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;
using Maen.Accounting.App;

namespace Maen.Accounting.App.ViewModels;

public sealed class ProfitEntryItemViewModel
{
    public ProfitEntryItemViewModel(ProfitEntry model)
    {
        Model = model;
        DateText = model.EntryDate.ToString("yyyy-MM-dd");
        SalesText = Money.Format(model.SalesMinor);
        CostText = Money.Format(model.CostMinor);
        ExpensesText = Money.Format(model.ExpensesMinor);
        NetText = Money.Format(model.NetProfitMinor);
        NotesText = string.IsNullOrWhiteSpace(model.Notes) ? UiText.Get("T141") : model.Notes;
        SalesSummaryText = UiText.Format("T172", SalesText);
        CostSummaryText = UiText.Format("T173", CostText);
        ExpensesSummaryText = UiText.Format("T174", ExpensesText);
    }

    public ProfitEntry Model { get; }
    public string DateText { get; }
    public string SalesText { get; }
    public string CostText { get; }
    public string ExpensesText { get; }
    public string NetText { get; }
    public string NotesText { get; }
    public string SalesSummaryText { get; }
    public string CostSummaryText { get; }
    public string ExpensesSummaryText { get; }
}
