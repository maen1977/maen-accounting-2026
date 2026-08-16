using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

public sealed class PersonalLedgerSummaryTests
{
    [Fact]
    public void Summary_separates_bank_balance_from_personal_cash_flow()
    {
        var entries = new[]
        {
            Entry("salary", new DateOnly(2026, 8, 1), sales: 300_000, movementType: PersonalMovementTypes.Salary),
            Entry("bank-deposit", new DateOnly(2026, 8, 2), amount: 200_000, movementType: PersonalMovementTypes.BankDeposit, wallet: "bank"),
            Entry("bank-withdrawal", new DateOnly(2026, 8, 3), amount: 50_000, movementType: PersonalMovementTypes.BankWithdrawal, wallet: "bank"),
            Entry("purchase", new DateOnly(2026, 8, 4), cost: 25_000, movementType: PersonalMovementTypes.Purchase)
        };

        var summary = PersonalLedgerSummaryCalculator.Summarize(entries, new DateOnly(2026, 8, 16));

        Assert.Equal(150_000, summary.BankBalanceMinor);
        Assert.Equal(200_000, summary.CurrentMonthBankDepositsMinor);
        Assert.Equal(50_000, summary.CurrentMonthBankWithdrawalsMinor);
        Assert.Equal(300_000, summary.CurrentMonthIncomeMinor);
        Assert.Equal(25_000, summary.CurrentMonthPurchasesMinor);
        Assert.Equal(275_000, summary.CurrentMonthAvailableBalanceMinor);
    }

    [Fact]
    public void Summary_groups_current_month_spending_by_category()
    {
        var entries = new[]
        {
            Entry("rent", new DateOnly(2026, 8, 1), cost: 120_000, movementType: PersonalMovementTypes.Purchase, category: "Housing"),
            Entry("food-1", new DateOnly(2026, 8, 2), cost: 60_000, movementType: PersonalMovementTypes.Purchase, category: "Food"),
            Entry("food-2", new DateOnly(2026, 8, 3), cost: 40_000, movementType: PersonalMovementTypes.Purchase, category: "food"),
            Entry("old", new DateOnly(2026, 7, 31), cost: 999_000, movementType: PersonalMovementTypes.Purchase, category: "Old"),
            Entry("deleted", new DateOnly(2026, 8, 4), cost: 999_000, movementType: PersonalMovementTypes.Purchase, category: "Deleted", isDeleted: true)
        };

        var summary = PersonalLedgerSummaryCalculator.Summarize(entries, new DateOnly(2026, 8, 16));

        Assert.Equal(2, summary.CurrentMonthCategorySpending.Count);
        Assert.Equal("Housing", summary.CurrentMonthCategorySpending[0].Category);
        Assert.Equal(120_000, summary.CurrentMonthCategorySpending[0].AmountMinor);
        Assert.Equal("Food", summary.CurrentMonthCategorySpending[1].Category);
        Assert.Equal(100_000, summary.CurrentMonthCategorySpending[1].AmountMinor);
        Assert.Equal(54.5, Math.Round(summary.CurrentMonthCategorySpending[0].SharePercent, 1));
    }

    [Fact]
    public void Summary_ignores_deleted_bank_movements_and_filters_month()
    {
        var entries = new[]
        {
            Entry("current", new DateOnly(2026, 8, 1), amount: 100_000, movementType: PersonalMovementTypes.BankDeposit, wallet: "bank"),
            Entry("previous", new DateOnly(2026, 7, 31), amount: 40_000, movementType: PersonalMovementTypes.BankDeposit, wallet: "bank"),
            Entry("deleted", new DateOnly(2026, 8, 2), amount: 999_000, movementType: PersonalMovementTypes.BankDeposit, wallet: "bank", isDeleted: true)
        };

        var summary = PersonalLedgerSummaryCalculator.Summarize(entries, new DateOnly(2026, 8, 16));

        Assert.Equal(140_000, summary.BankBalanceMinor);
        Assert.Equal(100_000, summary.CurrentMonthBankDepositsMinor);
        Assert.Equal(1, summary.CurrentMonth.EntriesCount);
    }

    private static ProfitEntry Entry(
        string id,
        DateOnly date,
        long sales = 0,
        long cost = 0,
        long expenses = 0,
        long amount = 0,
        string movementType = PersonalMovementTypes.Other,
        string wallet = "main",
        string category = "",
        bool isDeleted = false) =>
        new(
            id,
            "user-1",
            date,
            sales,
            cost,
            expenses,
            id,
            isDeleted,
            new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            1,
            "device-1",
            amount,
            movementType,
            category,
            wallet,
            string.Empty);
}
