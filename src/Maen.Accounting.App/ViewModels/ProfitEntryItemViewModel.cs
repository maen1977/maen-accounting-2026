using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;
using Maen.Accounting.App;

namespace Maen.Accounting.App.ViewModels;

public sealed class ProfitEntryItemViewModel
{
    public ProfitEntryItemViewModel(ProfitEntry model)
    {
        Model = model;
        DateText = model.EntryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        SalesText = Money.Format(model.SalesMinor);
        CostText = Money.Format(model.CostMinor);
        ExpensesText = Money.Format(model.ExpensesMinor);
        NetText = Money.Format(model.NetProfitMinor);
        AmountText = Money.Format(model.EffectiveAmountMinor);
        MovementTypeText = DisplayMovementType(model.MovementType);
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
    public string AmountText { get; }
    public string MovementTypeText { get; }
    public string NotesText { get; }
    public string SalesSummaryText { get; }
    public string CostSummaryText { get; }
    public string ExpensesSummaryText { get; }

    private static string DisplayMovementType(string movementType) => movementType switch
    {
        PersonalMovementTypes.Salary => UiText.Get("T332"),
        PersonalMovementTypes.Freelance => UiText.Get("T333"),
        PersonalMovementTypes.OtherIncome => UiText.Get("T334"),
        PersonalMovementTypes.Sale => UiText.Get("T144"),
        PersonalMovementTypes.Purchase => UiText.Get("T145"),
        PersonalMovementTypes.Expense => UiText.Get("T145"),
        PersonalMovementTypes.Withdrawal => UiText.Get("T335"),
        PersonalMovementTypes.Transfer => UiText.Get("T147"),
        PersonalMovementTypes.DebtPayment => UiText.Get("T336"),
        PersonalMovementTypes.BankDeposit => UiText.Get("T364"),
        PersonalMovementTypes.BankWithdrawal => UiText.Get("T365"),
        _ => UiText.Get("T148")
    };
}
