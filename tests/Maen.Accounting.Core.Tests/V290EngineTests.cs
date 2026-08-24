using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

public sealed class V290SavingsGoalTests
{
    private static readonly DateOnly Today = new(2026, 8, 16);

    private static SavingsGoal Goal(string id, string category, long target, long alreadySaved, DateOnly start, DateOnly deadline) =>
        new(id, "user", $"Goal {id}", category, target, alreadySaved, start, deadline);

    private static ProfitEntry Entry(DateOnly date, string category, long sales = 0, long cost = 0, long expenses = 0, bool deleted = false) =>
        new(Guid.NewGuid().ToString("N"), "user", date, sales, cost, expenses, "", deleted, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d", Category: category);

    [Fact]
    public void Track_MatchesCategoryDepositsToGoal()
    {
        var start = new DateOnly(2026, 7, 1);
        var goal = Goal("g1", "سفر", 100_000, 20_000, start, new DateOnly(2026, 12, 31));
        var entries = new[]
        {
            Entry(new DateOnly(2026, 8, 1), "سفر", sales: 30_000),
            Entry(new DateOnly(2026, 8, 5), "طعام", sales: 10_000),
        };

        var result = SavingsGoalCalculator.Track(new[] { goal }, entries, Today).Single();

        Assert.Equal(50_000, result.SavedMinor);
    }

    [Fact]
    public void Track_SubtractsCategorySpendingFromSavedAmount()
    {
        var start = new DateOnly(2026, 7, 1);
        var goal = Goal("g1", "سفر", 100_000, 40_000, start, new DateOnly(2026, 12, 31));
        var entries = new[]
        {
            Entry(new DateOnly(2026, 8, 1), "سفر", sales: 20_000),
            Entry(new DateOnly(2026, 8, 10), "سفر", cost: 0, expenses: 30_000),
        };

        var result = SavingsGoalCalculator.Track(new[] { goal }, entries, Today).Single();

        Assert.Equal(30_000, result.SavedMinor);
    }

    [Fact]
    public void Track_SavedCannotGoNegative()
    {
        var start = new DateOnly(2026, 7, 1);
        var goal = Goal("g1", "سفر", 100_000, 0, start, new DateOnly(2026, 12, 31));
        var entries = new[]
        {
            Entry(new DateOnly(2026, 8, 1), "سفر", expenses: 50_000),
        };

        var result = SavingsGoalCalculator.Track(new[] { goal }, entries, Today).Single();

        Assert.Equal(0, result.SavedMinor);
    }

    [Fact]
    public void Track_FullTargetMarksComplete()
    {
        var start = new DateOnly(2026, 7, 1);
        var goal = Goal("g1", "سفر", 50_000, 55_000, start, new DateOnly(2026, 12, 31));
        var result = SavingsGoalCalculator.Track(new[] { goal }, Array.Empty<ProfitEntry>(), Today).Single();

        Assert.True(result.IsComplete);
        Assert.Equal(100, result.ProgressPercent);
        Assert.Equal(0, result.RemainingMinor);
    }

    [Fact]
    public void Track_OverTargetCapsProgressAtHundred()
    {
        var start = new DateOnly(2026, 7, 1);
        var goal = Goal("g1", "سفر", 40_000, 60_000, start, new DateOnly(2026, 12, 31));
        var result = SavingsGoalCalculator.Track(new[] { goal }, Array.Empty<ProfitEntry>(), Today).Single();

        Assert.Equal(100, Math.Round(result.ProgressPercent));
        Assert.True(result.IsComplete);
    }

    [Fact]
    public void Track_IgnoresClosedGoals()
    {
        var goal = new SavingsGoal("g1", "user", "Closed", "سفر", 100_000, 90_000, new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31), IsClosed: true);
        var active = Goal("g2", "طعام", 10_000, 0, new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 31));

        var result = SavingsGoalCalculator.Track(new[] { goal, active }, Array.Empty<ProfitEntry>(), Today);

        Assert.Single(result);
        Assert.Equal("g2", result[0].Goal.GoalId);
    }

    [Fact]
    public void Track_IgnoresDeletedEntries()
    {
        var start = new DateOnly(2026, 7, 1);
        var goal = Goal("g1", "سفر", 100_000, 0, start, new DateOnly(2026, 12, 31));
        var entries = new[]
        {
            Entry(new DateOnly(2026, 8, 1), "سفر", sales: 77_000, deleted: true),
            Entry(new DateOnly(2026, 8, 2), "سفر", sales: 5_000),
        };

        var result = SavingsGoalCalculator.Track(new[] { goal }, entries, Today).Single();

        Assert.Equal(5_000, result.SavedMinor);
    }

    [Fact]
    public void Track_SortsByDaysRemainingAscending()
    {
        var far = Goal("g1", "سفر", 10_000, 0, new DateOnly(2026, 1, 1), new DateOnly(2027, 6, 1));
        var near = Goal("g2", "طعام", 10_000, 0, new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 1));

        var result = SavingsGoalCalculator.Track(new[] { far, near }, Array.Empty<ProfitEntry>(), Today);

        Assert.Equal("g2", result[0].Goal.GoalId);
        Assert.Equal("g1", result[1].Goal.GoalId);
    }

    [Fact]
    public void Track_CategoryMatchingIsCaseInsensitive()
    {
        var start = new DateOnly(2026, 7, 1);
        var goal = Goal("g1", "SAFAR", 100_000, 0, start, new DateOnly(2026, 12, 31));
        var entries = new[] { Entry(new DateOnly(2026, 8, 1), "safar", sales: 12_000) };

        var result = SavingsGoalCalculator.Track(new[] { goal }, entries, Today).Single();

        Assert.Equal(12_000, result.SavedMinor);
    }

    [Fact]
    public void Track_EmptyCategoryEntriesAreIgnored()
    {
        var start = new DateOnly(2026, 7, 1);
        var goal = Goal("g1", "سفر", 100_000, 0, start, new DateOnly(2026, 12, 31));
        var entries = new[] { new ProfitEntry("e1", "user", Today, 40_000, 0, 0, "", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d") };

        var result = SavingsGoalCalculator.Track(new[] { goal }, entries, Today).Single();

        Assert.Equal(0, result.SavedMinor);
    }
}

public sealed class V290FinancialHealthTests
{
    private static readonly DateOnly Today = new(2026, 8, 16);

    private static ProfitEntry Entry(DateOnly date, long sales = 0, long cost = 0, long expenses = 0, string category = "") =>
        new(Guid.NewGuid().ToString("N"), "user", date, sales, cost, expenses, "", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d", Category: category);

    [Fact]
    public void Assess_EmptyEntries_ReturnsNeutralScore()
    {
        var result = FinancialHealthCalculator.Assess(Array.Empty<ProfitEntry>(), Array.Empty<FinancialPlan>());

        Assert.Equal(50, result.Score);
        Assert.Equal(0, result.SavingsRatePercent);
        Assert.Equal(0, result.RunwayMonths);
        Assert.Empty(result.Flags);
    }

    [Fact]
    public void Assess_GoodSavingsRate_IncreasesScore()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 7, 5), sales: 100_000),
            Entry(new DateOnly(2026, 7, 20), expenses: 60_000),
            Entry(new DateOnly(2026, 8, 5), sales: 100_000),
            Entry(new DateOnly(2026, 8, 10), expenses: 60_000),
        };

        var result = FinancialHealthCalculator.Assess(entries, Array.Empty<FinancialPlan>());

        Assert.True(result.Score > 50);
        Assert.True(result.SavingsRatePercent >= 35 && result.SavingsRatePercent <= 45);
        Assert.DoesNotContain(result.Flags, flag => flag.Code == "LowSavingsRate");
    }

    [Fact]
    public void Assess_LowSavingsRate_RaisesLowSavingsRateFlag()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 6, 5), sales: 100_000),
            Entry(new DateOnly(2026, 6, 20), expenses: 95_000),
            Entry(new DateOnly(2026, 7, 5), sales: 100_000),
            Entry(new DateOnly(2026, 7, 25), expenses: 95_000),
            Entry(new DateOnly(2026, 8, 1), sales: 100_000),
            Entry(new DateOnly(2026, 8, 12), expenses: 95_000),
        };

        var result = FinancialHealthCalculator.Assess(entries, Array.Empty<FinancialPlan>());

        Assert.True(result.SavingsRatePercent < 10);
        Assert.Contains(result.Flags, flag => flag.Code == "LowSavingsRate");
    }

    [Fact]
    public void Assess_SpendingWithoutIncome_RaisesSpendingWithoutIncomeFlag()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 8, 5), expenses: 30_000),
            Entry(new DateOnly(2026, 8, 10), cost: 20_000),
        };

        var result = FinancialHealthCalculator.Assess(entries, Array.Empty<FinancialPlan>());

        Assert.Contains(result.Flags, flag => flag.Code == "SpendingWithoutIncome");
    }

    [Fact]
    public void Assess_HighFixedCostRatio_RaisesHighFixedCostsFlag()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 8, 1), sales: 100_000, category: "إيجار"),
            Entry(new DateOnly(2026, 8, 12), expenses: 70_000, category: "إيجار"),
        };
        var plan = new FinancialPlan(100_000, 50_000, 10_000, new[] { new PlanCategoryLimit("إيجار", 30_000) });

        var result = FinancialHealthCalculator.Assess(entries, new[] { plan });

        Assert.Contains(result.Flags, flag => flag.Code == "HighFixedCosts");
    }

    [Fact]
    public void Assess_FixedCostsOnlyCountLatestMonth()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 6, 5), sales: 100_000, expenses: 5_000, category: "إيجار"),
            Entry(new DateOnly(2026, 7, 5), sales: 100_000, expenses: 5_000, category: "إيجار"),
            Entry(new DateOnly(2026, 8, 1), sales: 100_000),
            Entry(new DateOnly(2026, 8, 10), expenses: 70_000, category: "إيجار"),
        };
        var plan = new FinancialPlan(100_000, 50_000, 10_000, new[] { new PlanCategoryLimit("إيجار", 30_000) });

        var result = FinancialHealthCalculator.Assess(entries, new[] { plan });

        // Fixed-cost tracking only counts the latest month's fixed-category spending (70,000),
        // not the small fixed spending from June and July (which would total 80,000 otherwise).
        Assert.Equal(Math.Round(70_000.0 / 300_000 * 100, 1), result.FixedCostRatioPercent);
    }

    [Fact]
    public void Assess_RunwayMonthsReflectsIncomeAgainstSpending()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 8, 1), sales: 100_000),
            Entry(new DateOnly(2026, 8, 12), expenses: 200_000),
        };

        var result = FinancialHealthCalculator.Assess(entries, Array.Empty<FinancialPlan>());

        Assert.True(result.RunwayMonths < 1);
        Assert.Contains(result.Flags, flag => flag.Code == "ShortRunway");
    }

    [Fact]
    public void Assess_ScoreLevelMapsCorrectly()
    {
        var summary = new FinancialHealthSummary(82, 20, 10, 5, Array.Empty<FinancialHealthFlag>());

        Assert.Equal("Excellent", summary.ScoreLevel);
        Assert.Equal("Critical", new FinancialHealthSummary(10, 20, 10, 5, Array.Empty<FinancialHealthFlag>()).ScoreLevel);
        Assert.Equal("Fair", new FinancialHealthSummary(45, 20, 10, 5, Array.Empty<FinancialHealthFlag>()).ScoreLevel);
    }

    [Fact]
    public void Assess_DeletedEntriesAreIgnored()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 8, 1), sales: 100_000),
            Entry(new DateOnly(2026, 8, 12), expenses: 95_000),
        };

        var result = FinancialHealthCalculator.Assess(entries, Array.Empty<FinancialPlan>());

        Assert.Contains(result.Flags, flag => flag.Code == "LowSavingsRate");
    }

    [Fact]
    public void Assess_NoIncomeWithZeroSpending_IsHealthy()
    {
        var entries = new[] { Entry(new DateOnly(2026, 8, 1), sales: 0) };

        var result = FinancialHealthCalculator.Assess(entries, Array.Empty<FinancialPlan>());

        Assert.DoesNotContain(result.Flags, flag => flag.Code == "SpendingWithoutIncome");
    }
}

public sealed class V290CashForecastTests
{
    private static readonly DateOnly Today = new(2026, 8, 16);

    private static ProfitEntry Entry(DateOnly date, long sales = 0, long cost = 0, long expenses = 0) =>
        new(Guid.NewGuid().ToString("N"), "user", date, sales, cost, expenses, "", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d");

    private static Obligation Obligation(long amount, string cycle, params DateOnly[] paid) =>
        new(Guid.NewGuid().ToString("N"), "user", "Obligation", "rent", amount, new DateOnly(2026, 7, 1), cycle, true, paid);

    [Fact]
    public void Forecast_CoversNinetyDaysAhead()
    {
        var entries = new[] { Entry(new DateOnly(2026, 7, 1), sales: 60_000) };

        var result = CashForecastCalculator.Forecast(entries, Array.Empty<Obligation>(), Today);

        Assert.True(result.Slices.Count >= 3);
        Assert.Equal(new DateOnly(2026, 8, 1), new DateOnly(result.Slices[0].Year, result.Slices[0].Month, 1));
        var last = result.Slices[^1];
        Assert.True(new DateOnly(last.Year, last.Month, 1) <= new DateOnly(2026, 11, 1));
    }

    [Fact]
    public void Forecast_UsesRecentNinetyDaysAverageIncome()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 6, 1), sales: 30_000),
            Entry(new DateOnly(2026, 7, 1), sales: 30_000),
        };

        var result = CashForecastCalculator.Forecast(entries, Array.Empty<Obligation>(), Today);

        var firstSlice = result.Slices[0];
        Assert.True(firstSlice.ProjectedIncomeMinor > 0);
        Assert.Equal(0, firstSlice.ProjectedSpendingMinor);
    }

    [Fact]
    public void Forecast_OldEntriesBeyondNinetyDaysAreIgnored()
    {
        var entries = new[] { Entry(new DateOnly(2025, 1, 1), sales: 10_000) };

        var result = CashForecastCalculator.Forecast(entries, Array.Empty<Obligation>(), Today);

        Assert.Equal(0, result.Slices.Sum(slice => slice.ProjectedIncomeMinor));
    }

    [Fact]
    public void Forecast_ObligationsReduceNetCash()
    {
        var entries = new[] { Entry(new DateOnly(2026, 8, 1), sales: 100_000) };
        var obligations = new[] { Obligation(25_000, "monthly") };

        var result = CashForecastCalculator.Forecast(entries, obligations, Today);

        Assert.Contains(result.Slices, slice => slice.ObligationsMinor == 25_000);
    }

    [Fact]
    public void Forecast_PaidOccurrenceIsExcludedWhileFutureStay()
    {
        var entries = new[] { Entry(new DateOnly(2026, 8, 1), sales: 100_000) };
        var obligations = new[] { Obligation(25_000, "monthly", new DateOnly(2026, 8, 1)) };

        var result = CashForecastCalculator.Forecast(entries, obligations, Today);

        // The paid August occurrence is excluded; only unpaid future occurrences count.
        var augustSlice = result.Slices.Single(static slice => slice.Month == 8);
        Assert.NotEqual(25_000, augustSlice.ObligationsMinor);
        Assert.Equal(0, augustSlice.ObligationsMinor);
    }

    [Fact]
    public void Forecast_IdentifiesWorstMonth()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 8, 1), sales: 30_000),
            Entry(new DateOnly(2026, 8, 10), expenses: 60_000),
        };
        var obligations = new[] { Obligation(40_000, "monthly") };

        var result = CashForecastCalculator.Forecast(entries, obligations, Today);

        Assert.NotNull(result.WorstSlice);
        Assert.Equal(result.Slices.Min(static slice => slice.NetMinor), result.WorstSlice.NetMinor);
        Assert.True(result.WorstSlice.NetMinor < 0);
    }

    [Fact]
    public void Forecast_NoEntries_ReturnsNeutralSlices()
    {
        var result = CashForecastCalculator.Forecast(Array.Empty<ProfitEntry>(), Array.Empty<Obligation>(), Today);

        Assert.True(result.Slices.Count >= 3);
        Assert.Equal(0, result.Slices[0].ProjectedIncomeMinor);
    }

    [Fact]
    public void Forecast_SliceNetEqualsIncomeMinusSpendingMinusObligations()
    {
        var entries = new[] { Entry(new DateOnly(2026, 8, 5), sales: 90_000) };
        var obligations = new[] { Obligation(10_000, "monthly") };

        var result = CashForecastCalculator.Forecast(entries, obligations, Today);

        foreach (var slice in result.Slices)
        {
            Assert.Equal(
                slice.ProjectedIncomeMinor - slice.ProjectedSpendingMinor - slice.ObligationsMinor,
                slice.NetMinor);
        }
    }

    [Fact]
    public void Forecast_DeletedEntriesAreIgnored()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 8, 1), sales: 50_000),
            new ProfitEntry("e1", "user", new DateOnly(2026, 8, 2), 50_000, 0, 0, "", true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d"),
        };

        var withDeleted = CashForecastCalculator.Forecast(entries, Array.Empty<Obligation>(), Today);
        var withoutDeleted = CashForecastCalculator.Forecast(entries.Where(static entry => !entry.IsDeleted).ToArray(), Array.Empty<Obligation>(), Today);

        foreach (var (with, without) in withDeleted.Slices.Zip(withoutDeleted.Slices))
        {
            Assert.Equal(without.ProjectedIncomeMinor, with.ProjectedIncomeMinor);
        }
    }

    [Fact]
    public void Forecast_ForecastDaysIsNinety()
    {
        Assert.Equal(90, CashForecastCalculator.ForecastDays);
    }
}

public sealed class V290BusinessComparisonTests
{
    private static MonthlySlice Slice(int year, int month, long sales = 0, long receipts = 0, long supplierPayments = 0) =>
        new(new DateOnly(year, month, 1), sales, ReceiptsMinor: receipts, SupplierPaymentsMinor: supplierPayments);

    [Fact]
    public void Compare_FirstMonthHasNoMoM()
    {
        var months = new[] { Slice(2026, 7, sales: 100_000) };

        var result = BusinessComparisonCalculator.Compare(months);

        Assert.Null(result[0].SalesDeltaMinor);
        Assert.Null(result[0].SalesYoYDeltaMinor);
    }

    [Fact]
    public void Compare_ComputesMoMSalesDelta()
    {
        var months = new[] { Slice(2026, 7, sales: 100_000), Slice(2026, 8, sales: 120_000) };

        var result = BusinessComparisonCalculator.Compare(months);

        Assert.Equal(20_000, result[1].SalesDeltaMinor);
        Assert.Equal(20, Math.Round(result[1].SalesDeltaPercent));
        Assert.True(result[1].SalesIsUp);
    }

    [Fact]
    public void Compare_ComputesYoYSalesDelta()
    {
        var months = new[] { Slice(2025, 8, sales: 80_000), Slice(2026, 7, sales: 100_000), Slice(2026, 8, sales: 120_000) };

        var result = BusinessComparisonCalculator.Compare(months);

        Assert.Equal(40_000, result[2].SalesYoYDeltaMinor);
        Assert.True(result[2].SalesYoYDeltaPercent >= 49 && result[2].SalesYoYDeltaPercent <= 51);
        Assert.Null(result[1].NetCashYoYDeltaMinor);
        Assert.Contains("YoY", result[2].SalesYoYText);
    }

    [Fact]
    public void Compare_NetCashUsesReceiptsMinusPayments()
    {
        var months = new[]
        {
            Slice(2026, 7, receipts: 50_000, supplierPayments: 30_000),
            Slice(2026, 8, receipts: 90_000, supplierPayments: 40_000),
        };

        var result = BusinessComparisonCalculator.Compare(months);

        Assert.Equal(20_000, months[0].NetCashMinor);
        Assert.Equal(50_000, months[1].NetCashMinor);
        Assert.Equal(30_000, result[1].NetCashDeltaMinor);
    }

    [Fact]
    public void Compare_ZeroPreviousAvoidsDivisionByZero()
    {
        var months = new[] { Slice(2026, 7, sales: 0), Slice(2026, 8, sales: 100_000) };

        var result = BusinessComparisonCalculator.Compare(months);

        Assert.Equal(0, result[1].SalesDeltaPercent);
        Assert.True(result[1].SalesIsUp);
    }

    [Fact]
    public void Compare_FlatMonthIsNotUp()
    {
        var months = new[] { Slice(2026, 7, sales: 100_000), Slice(2026, 8, sales: 100_000) };

        var result = BusinessComparisonCalculator.Compare(months);

        Assert.True(result[1].SalesIsFlat);
        Assert.False(result[1].SalesIsUp);
    }

    [Fact]
    public void Compare_OnlyMatchesSameMonthLastYear()
    {
        var months = new[] { Slice(2025, 7, sales: 77_000), Slice(2026, 8, sales: 120_000) };

        var result = BusinessComparisonCalculator.Compare(months);

        Assert.Null(result[1].SalesYoYDeltaMinor);
    }

    [Fact]
    public void Compare_DecreasingSalesReportsNegativeDelta()
    {
        var months = new[] { Slice(2026, 7, sales: 100_000), Slice(2026, 8, sales: 80_000) };

        var result = BusinessComparisonCalculator.Compare(months);

        Assert.Equal(-20_000, result[1].SalesDeltaMinor);
        Assert.Equal(-20, Math.Round(result[1].SalesDeltaPercent));
        Assert.Contains("MoM", result[1].SalesDeltaText);
    }

    [Fact]
    public void Compare_EmptyListReturnsEmpty()
    {
        var result = BusinessComparisonCalculator.Compare(Array.Empty<MonthlySlice>());

        Assert.Empty(result);
    }

    [Fact]
    public void Compare_MultipleMonthsEachGetComparison()
    {
        var months = Enumerable.Range(1, 12).Select(month => Slice(2026, month, sales: month * 10_000)).ToArray();

        var result = BusinessComparisonCalculator.Compare(months);

        Assert.Equal(12, result.Count);
        Assert.NotNull(result[0].SalesYoYText);
    }
}
