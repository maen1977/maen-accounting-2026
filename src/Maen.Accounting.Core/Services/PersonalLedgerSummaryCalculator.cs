using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Calculates personal-ledger summaries without any UI, persistence, or localization dependency.
/// </summary>
public static class PersonalLedgerSummaryCalculator
{
    public static PersonalLedgerSummary Summarize(IEnumerable<ProfitEntry> entries, DateOnly asOfDate)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var activeEntries = entries
            .Where(static entry => !entry.IsDeleted)
            .ToArray();
        var monthEntries = activeEntries
            .Where(entry => entry.EntryDate.Year == asOfDate.Year && entry.EntryDate.Month == asOfDate.Month)
            .ToArray();
        var yearEntries = activeEntries
            .Where(entry => entry.EntryDate.Year == asOfDate.Year)
            .ToArray();

        return new PersonalLedgerSummary(
            Overall: ProfitCalculator.Summarize(activeEntries),
            CurrentMonth: ProfitCalculator.Summarize(monthEntries),
            CurrentYear: ProfitCalculator.Summarize(yearEntries),
            BankBalanceMinor: activeEntries.Sum(GetBankImpactMinor),
            CurrentMonthBankDepositsMinor: monthEntries.Select(GetBankImpactMinor).Where(static amount => amount > 0).Sum(),
            CurrentMonthBankWithdrawalsMinor: monthEntries.Select(GetBankImpactMinor).Where(static amount => amount < 0).Sum(amount => checked(-amount)),
            CurrentMonthIncomeMinor: monthEntries.Sum(static entry => entry.SalesMinor),
            CurrentMonthSalaryMinor: SumByMovement(monthEntries, PersonalMovementTypes.Salary),
            CurrentMonthFreelanceMinor: SumByMovement(monthEntries, PersonalMovementTypes.Freelance),
            CurrentMonthPurchasesMinor: SumByMovement(monthEntries, PersonalMovementTypes.Purchase, PersonalMovementTypes.Expense),
            CurrentMonthWithdrawalsMinor: SumByMovement(monthEntries, PersonalMovementTypes.Withdrawal),
            CurrentMonthDebtPaymentsMinor: SumByMovement(monthEntries, PersonalMovementTypes.DebtPayment),
            CurrentMonthAvailableBalanceMinor: monthEntries.Where(IsCashWallet).Sum(static entry => checked(entry.SalesMinor - entry.CostMinor - entry.ExpensesMinor)),
            CurrentMonthCategorySpending: SummarizeCategories(monthEntries));
    }

    private static long SumByMovement(IReadOnlyCollection<ProfitEntry> entries, params string[] movementTypes) =>
        entries
            .Where(entry => movementTypes.Contains(entry.MovementType, StringComparer.Ordinal))
            .Sum(static entry => entry.EffectiveAmountMinor);

    private static IReadOnlyList<PersonalCategorySummary> SummarizeCategories(IEnumerable<ProfitEntry> entries)
    {
        var grouped = entries
            .Where(IsCategorySpending)
            .GroupBy(static entry => entry.Category.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new PersonalCategorySummary(
                Category: group.Key,
                AmountMinor: group.Sum(GetSpendingAmountMinor),
                EntriesCount: group.Count()))
            .Where(static item => item.AmountMinor > 0)
            .OrderByDescending(static item => item.AmountMinor)
            .ThenBy(static item => item.Category, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();

        var totalMinor = grouped.Sum(static item => item.AmountMinor);
        return grouped
            .Select(item => item with
            {
                SharePercent = totalMinor <= 0 ? 0 : (double)item.AmountMinor / totalMinor * 100
            })
            .ToArray();
    }

    private static bool IsCategorySpending(ProfitEntry entry) =>
        entry.MovementType is PersonalMovementTypes.Purchase
            or PersonalMovementTypes.Expense
            or PersonalMovementTypes.DebtPayment
            or PersonalMovementTypes.Withdrawal
        && checked(entry.CostMinor + entry.ExpensesMinor) > 0;

    private static long GetSpendingAmountMinor(ProfitEntry entry) =>
        checked(entry.CostMinor + entry.ExpensesMinor) > 0
            ? checked(entry.CostMinor + entry.ExpensesMinor)
            : entry.EffectiveAmountMinor;

    private static long GetBankImpactMinor(ProfitEntry entry)
    {
        if (!IsBankWallet(entry)) return 0;
        if (entry.MovementType == PersonalMovementTypes.BankDeposit || entry.IsIncome)
        {
            return entry.EffectiveAmountMinor;
        }

        if (entry.MovementType == PersonalMovementTypes.BankWithdrawal || entry.IsOutflow)
        {
            return checked(-entry.EffectiveAmountMinor);
        }

        return 0;
    }

    private static bool IsBankWallet(ProfitEntry entry) =>
        string.Equals(entry.Wallet, "bank", StringComparison.OrdinalIgnoreCase);

    private static bool IsCashWallet(ProfitEntry entry) => !IsBankWallet(entry);
}

public sealed record PersonalLedgerSummary(
    FinancialSummary Overall,
    FinancialSummary CurrentMonth,
    FinancialSummary CurrentYear,
    long BankBalanceMinor,
    long CurrentMonthBankDepositsMinor,
    long CurrentMonthBankWithdrawalsMinor,
    long CurrentMonthIncomeMinor,
    long CurrentMonthSalaryMinor,
    long CurrentMonthFreelanceMinor,
    long CurrentMonthPurchasesMinor,
    long CurrentMonthWithdrawalsMinor,
    long CurrentMonthDebtPaymentsMinor,
    long CurrentMonthAvailableBalanceMinor,
    IReadOnlyList<PersonalCategorySummary> CurrentMonthCategorySpending);

public sealed record PersonalCategorySummary(
    string Category,
    long AmountMinor,
    int EntriesCount,
    double SharePercent = 0);
