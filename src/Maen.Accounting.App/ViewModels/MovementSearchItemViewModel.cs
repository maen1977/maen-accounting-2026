using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.ViewModels;

/// <summary>
/// Presentation wrapper for a personal ledger movement returned by <see cref="MovementSearchEngine"/>.
/// </summary>
public sealed class MovementSearchItemViewModel
{
    public MovementSearchItemViewModel(ProfitEntry model)
    {
        Model = model;
        DateText = model.EntryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        TitleText = model.Category;
        DetailText = model.Notes;
        AmountText = Money.Format(model.EffectiveAmountMinor);
        AmountColor = model.IsIncome ? "#137A53" : "#C2413A";
        MovementTypeText = DisplayMovementType(model.MovementType);
    }

    public ProfitEntry Model { get; }
    public string DateText { get; }
    public string TitleText { get; }
    public string DetailText { get; }
    public string AmountText { get; }
    public string AmountColor { get; }
    public string MovementTypeText { get; }

    private static string DisplayMovementType(string movementType) =>
        TranslateMovementType(movementType);

    private static string TranslateMovementType(string movementType) => movementType switch
    {
        "" => "",
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
        _ => UiText.Get("T148"),
    };
}
