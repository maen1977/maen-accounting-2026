using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;
using Xunit;

namespace Maen.Accounting.Core.Tests;

public class PersonalAnnualReportCalculatorTests
{
    private static ProfitEntry Entry(
        DateOnly date, long sales = 0, long cost = 0, long expenses = 0,
        bool deleted = false, string movementType = "other", string category = "")
    {
        var now = DateTimeOffset.UtcNow;
        return new ProfitEntry(Guid.NewGuid().ToString("N"), "user", date, sales, cost, expenses, "", deleted, now, now, 1, "d", 0, movementType, category);
    }

    private static ProfitEntry Purchase(DateOnly date, long cost, string category) =>
        Entry(date, 0, cost, 0, false, "purchase", category);

    private static ProfitEntry Expense(DateOnly date, long expenses, string category) =>
        Entry(date, 0, 0, expenses, false, "expense", category);

    private static ProfitEntry Salary(DateOnly date, long sales) =>
        Entry(date, sales, 0, 0, false, "salary", "");

    [Fact]
    public void Build_EmptyEntries_ReturnsAllZeroMonths()
    {
        var report = PersonalAnnualReportCalculator.Build(Array.Empty<ProfitEntry>(), 2026);

        Assert.Equal(2026, report.Year);
        Assert.Equal(12, report.Months.Count);
        Assert.All(report.Months, month => Assert.Equal(0, month.IncomeMinor));
        Assert.Equal(0, report.TotalIncomeMinor);
        Assert.Equal(0, report.TotalSpendingMinor);
        Assert.Equal(0, report.TotalNetMinor);
    }

    [Fact]
    public void Build_AggregatesIncomeAndSpendingPerMonth()
    {
        var entries = new[]
        {
            Salary(new DateOnly(2026, 3, 5), 100_000),
            Salary(new DateOnly(2026, 3, 20), 50_000),
            Purchase(new DateOnly(2026, 3, 10), 30_000, "food"),
            Expense(new DateOnly(2026, 3, 12), 5_000, "rent"),
            Salary(new DateOnly(2026, 5, 1), 100_000),
        };

        var report = PersonalAnnualReportCalculator.Build(entries, 2026);

        Assert.Equal(250_000, report.TotalIncomeMinor);
        Assert.Equal(35_000, report.TotalSpendingMinor);
        Assert.Equal(215_000, report.TotalNetMinor);
        Assert.Equal(150_000, report.Months[2].IncomeMinor); // March
        Assert.Equal(35_000, report.Months[2].SpendingMinor);
        Assert.Equal(100_000, report.Months[4].IncomeMinor); // May
        Assert.Equal(0, report.Months[4].SpendingMinor);
    }

    [Fact]
    public void Build_WithdrawalsCountAsSpending()
    {
        var entries = new[] { Entry(new DateOnly(2026, 1, 2), 0, 0, 0, false, "withdrawal") };
        // withdrawal with AmountMinor==0 contributes nothing
        var report = PersonalAnnualReportCalculator.Build(entries, 2026);
        Assert.Equal(0, report.Months[0].SpendingMinor);

        var entryWithAmount = Entry(new DateOnly(2026, 1, 2), 0, 0, 0, false, "withdrawal");
        entryWithAmount = entryWithAmount with { AmountMinor = 20_000 };
        var report2 = PersonalAnnualReportCalculator.Build(new[] { entryWithAmount }, 2026);
        Assert.Equal(20_000, report2.Months[0].SpendingMinor);
    }

    [Fact]
    public void Build_DeletedEntriesAreExcluded()
    {
        var entries = new[]
        {
            Salary(new DateOnly(2026, 2, 1), 40_000),
            Entry(new DateOnly(2026, 2, 5), 60_000, 0, 0, deleted: true),
        };

        var report = PersonalAnnualReportCalculator.Build(entries, 2026);

        Assert.Equal(40_000, report.Months[1].IncomeMinor);
    }

    [Fact]
    public void Build_BestAndWorstMonthByNet()
    {
        var entries = new[]
        {
            Salary(new DateOnly(2026, 1, 1), 100_000),
            Purchase(new DateOnly(2026, 1, 2), 80_000, "x"),
            Salary(new DateOnly(2026, 6, 1), 100_000),
            Purchase(new DateOnly(2026, 6, 2), 10_000, "x"),
            Salary(new DateOnly(2026, 9, 1), 100_000),
            Purchase(new DateOnly(2026, 9, 2), 120_000, "x"),
        };

        var report = PersonalAnnualReportCalculator.Build(entries, 2026);

        Assert.Equal(6, report.BestMonth.Month);
        Assert.Equal(90_000, report.BestMonth.NetMinor);
        Assert.Equal(9, report.WorstMonth.Month);
        Assert.Equal(-20_000, report.WorstMonth.NetMinor);
    }

    [Fact]
    public void Build_EntriesFromOtherYearsIgnored()
    {
        var entries = new[] { Salary(new DateOnly(2025, 12, 31), 500_000) };
        var report = PersonalAnnualReportCalculator.Build(entries, 2026);
        Assert.Equal(0, report.TotalIncomeMinor);
    }

    [Fact]
    public void YearOverYearIncomeChangePercent_PreviousZeroPositiveCurrent_Returns100()
    {
        var current = Enumerable.Range(1, 12).Select(m => new PersonalMonthlySnapshot(2026, m, 10_000, 0, 10_000)).ToArray();
        var previous = Enumerable.Range(1, 12).Select(m => new PersonalMonthlySnapshot(2025, m, 0, 0, 0)).ToArray();

        Assert.Equal(100, PersonalAnnualReportCalculator.YearOverYearIncomeChangePercent(current, previous));
    }

    [Fact]
    public void YearOverYearIncomeChangePercent_BothZero_ReturnsZero()
    {
        var current = Enumerable.Range(1, 12).Select(m => new PersonalMonthlySnapshot(2026, m, 0, 0, 0)).ToArray();
        var previous = Enumerable.Range(1, 12).Select(m => new PersonalMonthlySnapshot(2025, m, 0, 0, 0)).ToArray();

        Assert.Equal(0, PersonalAnnualReportCalculator.YearOverYearIncomeChangePercent(current, previous));
    }

    [Fact]
    public void YearOverYearIncomeChangePercent_GrowthAndDecline_CorrectPercent()
    {
        var current = Enumerable.Range(1, 12).Select(m => new PersonalMonthlySnapshot(2026, m, 10_000, 0, 10_000)).ToArray();
        var previous = Enumerable.Range(1, 12).Select(m => new PersonalMonthlySnapshot(2025, m, 8_000, 0, 8_000)).ToArray();

        Assert.Equal(25, PersonalAnnualReportCalculator.YearOverYearIncomeChangePercent(current, previous), 9);

        var decline = Enumerable.Range(1, 12).Select(m => new PersonalMonthlySnapshot(2026, m, 6_000, 0, 6_000)).ToArray();
        Assert.Equal(-25, PersonalAnnualReportCalculator.YearOverYearIncomeChangePercent(decline, previous), 9);
    }

    [Fact]
    public void Build_MultipleMovementTypes_SpendingFormulaConsistent()
    {
        var now = DateTimeOffset.UtcNow;
        var withCost = new ProfitEntry("x", "u", new DateOnly(2026, 4, 3), 0, 40_000, 10_000, "", false, now, now, 1, "d", 0, "purchase", "grocery");
        // cost+expenses > 0 -> uses cost+expenses (50_000)
        var report = PersonalAnnualReportCalculator.Build(new[] { withCost }, 2026);
        Assert.Equal(50_000, report.Months[3].SpendingMinor);

        var noCost = new ProfitEntry("y", "u", new DateOnly(2026, 4, 4), 0, 0, 0, "", false, now, now, 1, "d", 25_000, "purchase", "grocery");
        // cost+expenses == 0 -> uses EffectiveAmountMinor (25_000)
        var report2 = PersonalAnnualReportCalculator.Build(new[] { noCost }, 2026);
        Assert.Equal(25_000, report2.Months[3].SpendingMinor);
    }
}

public class PersonalCategoryReportCalculatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static ProfitEntry Purchase(DateOnly date, long cost, string category, long amountMinor = 0) =>
        new(Guid.NewGuid().ToString("N"), "u", date, 0, cost, 0, "", false, Now, Now, 1, "d", amountMinor, "purchase", category);

    private static ProfitEntry Expense(DateOnly date, long expenses, string category) =>
        new(Guid.NewGuid().ToString("N"), "u", date, 0, 0, expenses, "", false, Now, Now, 1, "d", 0, "expense", category);

    [Fact]
    public void Build_GroupsByCategoryDescending()
    {
        var month = new DateOnly(2026, 7, 1);
        var entries = new[]
        {
            Purchase(month, 10_000, "food"),
            Purchase(month.AddDays(1), 10_000, "food"),
            Purchase(month.AddDays(2), 30_000, "rent"),
            Expense(month.AddDays(3), 5_000, "transport"),
            Purchase(month.AddDays(4), 20_000, "food"),
        };

        var report = PersonalCategoryReportCalculator.Build(entries, month);

        Assert.Equal(75_000, report.TotalSpendingMinor);
        Assert.Equal(3, report.Breakdowns.Count);
        Assert.Equal("food", report.Breakdowns[0].Category);
        Assert.Equal(40_000, report.Breakdowns[0].SpentMinor);
        Assert.Equal(1, report.Breakdowns[0].Rank);
        Assert.Equal("rent", report.Breakdowns[1].Category);
        Assert.Equal(2, report.Breakdowns[1].Rank);
        Assert.Equal("transport", report.Breakdowns[2].Category);
        Assert.Equal(3, report.Breakdowns[2].Rank);
    }

    [Fact]
    public void Build_IgnoresOtherMonthsAndNonSpendingTypes()
    {
        var month = new DateOnly(2026, 4, 1);
        var entries = new[]
        {
            new ProfitEntry("a", "u", month, 50_000, 0, 0, "", false, Now, Now, 1, "d", 0, "salary", "income"), // sale
            Purchase(month.AddDays(1), 8_000, "food"),
            Purchase(new DateOnly(2026, 5, 1), 100_000, "food"),
            new ProfitEntry("b", "u", month, 0, 0, 0, "", false, Now, Now, 1, "d", 5_000, "transfer", ""), // transfer
        };

        var report = PersonalCategoryReportCalculator.Build(entries, month);

        Assert.Equal(8_000, report.TotalSpendingMinor);
        Assert.Single(report.Breakdowns);
    }

    [Fact]
    public void Build_SharePercentBasedOnReportTotal()
    {
        var month = new DateOnly(2026, 2, 1);
        var entries = new[]
        {
            Purchase(month, 75_000, "rent"),
            Purchase(month.AddDays(1), 25_000, "food"),
        };

        var report = PersonalCategoryReportCalculator.Build(entries, month);

        Assert.Equal(75, report.SharePercentFor(report.Breakdowns[0]), 9);
        Assert.Equal(25, report.SharePercentFor(report.Breakdowns[1]), 9);
    }

    [Fact]
    public void Build_ZeroSpending_NoBreakdowns()
    {
        var report = PersonalCategoryReportCalculator.Build(Array.Empty<ProfitEntry>(), new DateOnly(2026, 1, 1));
        Assert.Empty(report.Breakdowns);
        Assert.Equal(0, report.TotalSpendingMinor);
    }

    [Fact]
    public void Build_EmptyCategoryGroupedTogether()
    {
        var month = new DateOnly(2026, 3, 1);
        var entries = new[]
        {
            Purchase(month, 5_000, "food"),
            new ProfitEntry("c", "u", month, 0, 3_000, 0, "", false, Now, Now, 1, "d", 0, "purchase", "  "),
        };

        var report = PersonalCategoryReportCalculator.Build(entries, month);

        Assert.Equal(2, report.Breakdowns.Count);
        Assert.Contains(report.Breakdowns, b => b.Category == string.Empty && b.SpentMinor == 3_000);
    }

    [Fact]
    public void Build_CaseInsensitiveCategoryMerge()
    {
        var month = new DateOnly(2026, 6, 1);
        var entries = new[]
        {
            Purchase(month, 10_000, "Food"),
            Purchase(month.AddDays(1), 15_000, "food"),
            Purchase(month.AddDays(2), 5_000, "FOOD"),
        };

        var report = PersonalCategoryReportCalculator.Build(entries, month);

        Assert.Single(report.Breakdowns);
        Assert.Equal(30_000, report.Breakdowns[0].SpentMinor);
        Assert.Equal(3, report.Breakdowns[0].EntriesCount);
    }
}

public class SavingsTrendCalculatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static ProfitEntry Salary(DateOnly date, long sales) =>
        new(Guid.NewGuid().ToString("N"), "u", date, sales, 0, 0, "", false, Now, Now, 1, "d", 0, "salary", "");

    private static ProfitEntry Purchase(DateOnly date, long cost, string category) =>
        new(Guid.NewGuid().ToString("N"), "u", date, 0, cost, 0, "", false, Now, Now, 1, "d", 0, "purchase", category);

    private static ProfitEntry Expense(DateOnly date, long expenses) =>
        new(Guid.NewGuid().ToString("N"), "u", date, 0, 0, expenses, "", false, Now, Now, 1, "d", 0, "expense", "");

    private static FinancialPlan Plan(long savingsTarget) => new(0, 0, savingsTarget, Array.Empty<PlanCategoryLimit>());

    [Fact]
    public void Build_ReturnsTrailingMonthsEndingAtAsOfDate()
    {
        var trend = SavingsTrendCalculator.Build(Array.Empty<ProfitEntry>(), new DateOnly(2026, 8, 1), 3);

        Assert.Equal(3, trend.Points.Count);
        var trendMonths = trend.Points.Select(point => new DateOnly(point.Year, point.Month, 1)).ToList();
        Assert.Contains(new DateOnly(2026, 6, 1), trendMonths);
        Assert.Contains(new DateOnly(2026, 8, 1), trendMonths);
        Assert.Equal(3, trendMonths.Distinct().Count());
        // oldest point first (index 0), newest point last
        Assert.Equal(new DateOnly(2026, 6, 1), new DateOnly(trend.Points[0].Year, trend.Points[0].Month, 1));
        Assert.Equal(new DateOnly(2026, 8, 1), new DateOnly(trend.Points[^1].Year, trend.Points[^1].Month, 1));
    }

    [Fact]
    public void Build_ZeroTrailingMonthsDefaultsToSix()
    {
        var trend = SavingsTrendCalculator.Build(Array.Empty<ProfitEntry>(), new DateOnly(2026, 3, 1), 0);
        Assert.Equal(6, trend.Points.Count);
    }

    [Fact]
    public void Build_ComputesSavedAsIncomeMinusSpending()
    {
        var asOf = new DateOnly(2026, 4, 1);
        var entries = new[]
        {
            Salary(new DateOnly(2026, 4, 2), 100_000),
            Purchase(new DateOnly(2026, 4, 5), 30_000, "food"),
            Expense(new DateOnly(2026, 4, 6), 10_000),
        };

        var trend = SavingsTrendCalculator.Build(entries, asOf, 1);

        Assert.Equal(60_000, trend.Points[0].SavedMinor);
        Assert.Equal(100_000, trend.Points[0].IncomeMinor);
        Assert.Equal(40_000, trend.Points[0].SpendingMinor);
        Assert.Equal(60_000, trend.TotalSavedMinor);
    }

    [Fact]
    public void Build_NoPlan_TargetZero_NotOnTarget()
    {
        var asOf = new DateOnly(2026, 4, 1);
        var entries = new[] { Salary(new DateOnly(2026, 4, 1), 100_000) };

        var trend = SavingsTrendCalculator.Build(entries, asOf, 1);

        Assert.Equal(0, trend.Points[0].TargetMinor);
        Assert.False(trend.Points[0].IsOnTarget);
    }

    [Fact]
    public void Build_SavedAtOrAboveTarget_IsOnTarget()
    {
        var asOf = new DateOnly(2026, 4, 1);
        var plan = Plan(50_000);
        var entries = new[] { Salary(new DateOnly(2026, 4, 1), 100_000), Purchase(new DateOnly(2026, 4, 2), 50_000, "x") };

        var trend = SavingsTrendCalculator.Build(entries, asOf, 1, plan);

        Assert.True(trend.Points[0].IsOnTarget);

        var overBudget = new[] { Salary(new DateOnly(2026, 4, 1), 100_000), Purchase(new DateOnly(2026, 4, 2), 60_000, "x") };
        var trend2 = SavingsTrendCalculator.Build(overBudget, asOf, 1, plan);
        Assert.False(trend2.Points[0].IsOnTarget);
    }

    [Fact]
    public void ConsecutiveStreak_CountsFromLatestMonthBackwards()
    {
        var asOf = new DateOnly(2026, 4, 1);
        var plan = Plan(20_000);
        var entries = new[]
        {
            Salary(new DateOnly(2026, 2, 1), 10_000),               // saved 10_000 < target
            Salary(new DateOnly(2026, 3, 1), 100_000),              // saved 100_000 >= target
            Salary(new DateOnly(2026, 4, 1), 100_000),              // saved 100_000 >= target
        };

        var trend = SavingsTrendCalculator.Build(entries, asOf, 3, plan);

        Assert.Equal(2, trend.ConsecutiveOnTargetStreak);
        Assert.Equal(2, trend.MonthsOnTarget);
        Assert.Equal(3, trend.MonthsOnTargetEligible);
    }

    [Fact]
    public void ConsecutiveStreak_LatestMonthMisses_ReturnsZero()
    {
        var asOf = new DateOnly(2026, 5, 1);
        var plan = Plan(20_000);
        var entries = new[]
        {
            Salary(new DateOnly(2026, 3, 1), 100_000),
            Salary(new DateOnly(2026, 4, 1), 100_000),
            Salary(new DateOnly(2026, 5, 1), 5_000),
        };

        var trend = SavingsTrendCalculator.Build(entries, asOf, 3, plan);

        Assert.Equal(0, trend.ConsecutiveOnTargetStreak);
    }

    [Fact]
    public void Build_DeletedEntriesExcluded()
    {
        var asOf = new DateOnly(2026, 1, 1);
        var deleted = Salary(new DateOnly(2026, 1, 1), 90_000) with { IsDeleted = true };
        var trend = SavingsTrendCalculator.Build(new[] { deleted }, asOf, 1);
        Assert.Equal(0, trend.Points[0].IncomeMinor);
    }

    [Fact]
    public void Build_CrossesYearBoundary()
    {
        var asOf = new DateOnly(2027, 1, 1);
        var trend = SavingsTrendCalculator.Build(Array.Empty<ProfitEntry>(), asOf, 3);

        Assert.Equal(2026, trend.Points[0].Year);
        Assert.Equal(11, trend.Points[0].Month); // oldest point: Nov 2026; newest: Jan 2027
        Assert.Equal(2027, trend.Points[2].Year);
        Assert.Equal(1, trend.Points[2].Month);
    }

    [Fact]
    public void Build_WithdrawalsReduceSavings()
    {
        var asOf = new DateOnly(2026, 2, 1);
        var now = DateTimeOffset.UtcNow;
        var withdrawal = new ProfitEntry("w", "u", new DateOnly(2026, 2, 2), 0, 0, 0, "", false, now, now, 1, "d", 15_000, "withdrawal", "");
        var entries = new[] { Salary(new DateOnly(2026, 2, 1), 40_000), withdrawal };

        var trend = SavingsTrendCalculator.Build(entries, asOf, 1);

        Assert.Equal(25_000, trend.Points[0].SavedMinor);
    }
}
