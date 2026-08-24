using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

public sealed class V300RecurringMovementTests
{
    private static readonly DateOnly Today = new(2026, 8, 16);

    private static RecurringMovement Movement(
        DateOnly next,
        string cycle = "monthly",
        string kind = "expense",
        long amount = 50_000,
        bool isActive = true) =>
        new($"m{next:yyyyMMdd}", "user", "Movement", "إيجار", amount, kind,
            new DateOnly(2026, 1, 1), cycle, next, isActive);

    [Fact]
    public void DueMovements_ReturnsDueInactiveSkipped()
    {
        var movements = new[]
        {
            Movement(new DateOnly(2026, 8, 16)),
            Movement(new DateOnly(2026, 8, 10)),
            Movement(new DateOnly(2026, 8, 1)),
            Movement(new DateOnly(2026, 8, 1), isActive: false),
            Movement(new DateOnly(2026, 8, 20)),
        };

        var due = RecurringMovementCalculator.DueMovements(movements, Today);

        Assert.Equal(2, due.Count);
        Assert.All(due, item => Assert.True(item.NextOccurrence <= Today));
    }

    [Fact]
    public void DueMovements_SkipsStaleBeyondMaxMissedDays()
    {
        var movements = new[]
        {
            Movement(new DateOnly(2026, 8, 1)),
            Movement(new DateOnly(2026, 8, 9)),
            Movement(new DateOnly(2026, 8, 16)),
        };

        var due = RecurringMovementCalculator.DueMovements(movements, Today);

        // 2026-08-01 is 15 days late (> MaxMissedDays = 7) and must be skipped;
        // 2026-08-09 is 7 days late and stays due (only strictly older than 7 days is stale).
        Assert.Equal(2, due.Count);
        Assert.All(due, item => Assert.NotEqual(new DateOnly(2026, 8, 1), item.NextOccurrence));
        Assert.Contains(due, item => item.NextOccurrence == new DateOnly(2026, 8, 9));
    }

    [Fact]
    public void AdvanceOccurrence_MonthlyWrapsYearAndClampsEndOfMonth()
    {
        Assert.Equal(new DateOnly(2026, 7, 1), RecurringMovementCalculator.AdvanceOccurrence(new DateOnly(2026, 6, 1), "monthly"));
        Assert.Equal(new DateOnly(2027, 1, 1), RecurringMovementCalculator.AdvanceOccurrence(new DateOnly(2026, 12, 1), "monthly"));
        Assert.Equal(new DateOnly(2026, 4, 30), RecurringMovementCalculator.AdvanceOccurrence(new DateOnly(2026, 3, 31), "monthly"));
        Assert.Equal(new DateOnly(2026, 8, 23), RecurringMovementCalculator.AdvanceOccurrence(new DateOnly(2026, 8, 16), "weekly"));
        Assert.Equal(new DateOnly(2026, 8, 17), RecurringMovementCalculator.AdvanceOccurrence(new DateOnly(2026, 8, 16), "daily"));
        Assert.Equal(new DateOnly(2027, 8, 16), RecurringMovementCalculator.AdvanceOccurrence(new DateOnly(2026, 8, 16), "yearly"));
    }

    [Fact]
    public void BuildAmounts_IncomeCarriesSalesExpenseCarriesExpenses()
    {
        var income = Movement(new DateOnly(2026, 8, 1), kind: "income", amount: 300_000);
        var expense = Movement(new DateOnly(2026, 8, 1), kind: "expense", amount: 40_000);

        var (incomeSales, incomeCost, incomeExpenses) = RecurringMovementCalculator.BuildAmounts(income);
        var (expenseSales, expenseCost, expenseExpenses) = RecurringMovementCalculator.BuildAmounts(expense);

        Assert.Equal((300_000L, 0L, 0L), (incomeSales, incomeCost, incomeExpenses));
        Assert.Equal((0L, 0L, 40_000L), (expenseSales, expenseCost, expenseExpenses));
    }

    [Fact]
    public void BuildAmounts_NullKindDefaultsToExpense()
    {
        var movement = Movement(new DateOnly(2026, 8, 1), kind: null!);

        var amounts = RecurringMovementCalculator.BuildAmounts(movement);

        Assert.Equal((0L, 0L, 50_000L), amounts);
    }

    [Fact]
    public void DaysUntil_NegativeWhenDue()
    {
        var movement = Movement(new DateOnly(2026, 8, 16));
        Assert.Equal(0, RecurringMovementCalculator.DaysUntil(movement, Today));
        Assert.Equal(-7, RecurringMovementCalculator.DaysUntil(Movement(new DateOnly(2026, 8, 9)), Today));
        Assert.Equal(10, RecurringMovementCalculator.DaysUntil(Movement(new DateOnly(2026, 8, 26)), Today));
    }
}

public sealed class V300CategoryAnalyticsTests
{
    private static readonly DateOnly Month = new(2026, 8, 1);

    private static ProfitEntry Entry(DateOnly date, string category, long cost = 0, long expenses = 0, long sales = 0) =>
        new(Guid.NewGuid().ToString("N"), "user", date, sales, cost, expenses, "", false,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d",
            Category: category);

    [Fact]
    public void Analyze_TopCategoryIsHighestSpendingCategory()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 8, 1), "إيجار", expenses: 70_000),
            Entry(new DateOnly(2026, 8, 5), "طعام", cost: 0, expenses: 20_000),
            Entry(new DateOnly(2026, 8, 12), "إيجار", expenses: 5_000),
            Entry(new DateOnly(2026, 8, 1), "غير محدد", sales: 300_000),
        };

        var result = TopCategoryAnalyticsEngine.Analyze(entries, Month);

        // The "غير محدد" entry is income-only and must be excluded; only the two
        // spending categories remain (إيجار 75,000 + طعام 20,000 = 95,000 total).
        Assert.Equal(2, result.Count);
        Assert.Equal("إيجار", result[0].Category);
        Assert.Equal(75_000, result[0].AmountMinor);
        Assert.Equal(78.9, Math.Round(result[0].SharePercent, 1));
        Assert.Equal("طعام", result[1].Category);
        Assert.Equal(20_000, result[1].AmountMinor);
        Assert.Equal(21.1, Math.Round(result[1].SharePercent, 1));
    }

    [Fact]
    public void Analyze_IgnoresIncomeAndDeletedEntries()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 8, 1), "طعام", cost: 30_000),
            Entry(new DateOnly(2026, 8, 1), "مبيعات", sales: 500_000),
            new ProfitEntry(Guid.NewGuid().ToString("N"), "user", new DateOnly(2026, 8, 1), 0, 60_000, 0, "", true,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d"),
        };

        var result = TopCategoryAnalyticsEngine.Analyze(entries, Month);

        Assert.Single(result);
        Assert.Equal(30_000, result[0].AmountMinor);
    }

    [Fact]
    public void Analyze_SetsPeakAndLowMonthsPerCategory()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 6, 5), "طعام", expenses: 10_000),
            Entry(new DateOnly(2026, 7, 5), "طعام", expenses: 40_000),
            Entry(new DateOnly(2026, 8, 5), "طعام", expenses: 15_000),
        };

        var result = TopCategoryAnalyticsEngine.Analyze(entries, Month);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 7, 1), result[0].PeakMonth);
        Assert.Equal(new DateOnly(2026, 6, 1), result[0].LowMonth);
    }

    [Fact]
    public void Analyze_EmptyReturnsEmpty()
    {
        Assert.Empty(TopCategoryAnalyticsEngine.Analyze(Array.Empty<ProfitEntry>(), Month));
    }

    [Fact]
    public void MonthlyTotals_ReturnsLastNMonthsDescending()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 5, 2), category: "مبيعات", sales: 100_000),
            Entry(new DateOnly(2026, 6, 2), category: "طعام", expenses: 30_000),
            Entry(new DateOnly(2026, 7, 2), category: "مبيعات", sales: 200_000),
            Entry(new DateOnly(2026, 7, 8), category: "طعام", expenses: 50_000),
        };

        var result = TopCategoryAnalyticsEngine.MonthlyTotals(entries, 3);

        Assert.Equal(3, result.Count);
        Assert.Equal(0L, result[0].SpendingMinor);
        Assert.Equal(100_000L, result[0].IncomeMinor);
        Assert.Equal(50_000L, result[2].SpendingMinor);
        Assert.Equal(150_000L, result[2].NetMinor);
    }
}

public sealed class V300AuditTrailTests
{
    [Fact]
    public void Summarize_HealthyWhenHashesVerifiedAndCovered()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new[]
        {
            new ProfitEntry("e1", "user", new DateOnly(2026, 8, 1), 100_000, 0, 0, "", false,
                now.AddMinutes(-5), now, 1, "d", IntegrityHash: "hash"),
            new ProfitEntry("e2", "user", new DateOnly(2026, 8, 2), 0, 30_000, 0, "", false,
                now, now, 2, "d", IntegrityHash: "hash"),
        };

        var summary = AuditTrailCalculator.Summarize(entries, 2, 2, now);

        Assert.True(summary.IsHealthy);
        Assert.Equal("healthy", summary.Verdict);
        Assert.Equal(2, summary.ActiveRecordCount);
        Assert.Equal(100.0, summary.VerifiedPercent);
        Assert.Equal(100.0, summary.HashCoveragePercent);
    }

    [Fact]
    public void Summarize_PartialWhenCoverageBetween50And90()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new[]
        {
            new ProfitEntry("e1", "user", new DateOnly(2026, 8, 1), 100_000, 0, 0, "", false,
                now, now, 1, "d", IntegrityHash: "hash"),
            new ProfitEntry("e2", "user", new DateOnly(2026, 8, 2), 0, 30_000, 0, "", false,
                now, now, 2, "d", IntegrityHash: string.Empty),
        };

        var summary = AuditTrailCalculator.Summarize(entries, 1, 2, now);

        Assert.Equal("partial", summary.Verdict);
        Assert.False(summary.IsHealthy);
        Assert.Equal(50.0, summary.HashCoveragePercent);
    }

    [Fact]
    public void Summarize_PendingWhenCoverageBelow50()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new[]
        {
            new ProfitEntry("e1", "user", new DateOnly(2026, 8, 1), 100_000, 0, 0, "", false,
                now, now, 1, "d", IntegrityHash: string.Empty),
            new ProfitEntry("e2", "user", new DateOnly(2026, 8, 2), 0, 30_000, 0, "", false,
                now, now, 2, "d", IntegrityHash: string.Empty),
            new ProfitEntry("e3", "user", new DateOnly(2026, 8, 3), 0, 10_000, 0, "", false,
                now, now, 3, "d", IntegrityHash: string.Empty),
        };

        var summary = AuditTrailCalculator.Summarize(entries, 1, 3, now);

        Assert.Equal("pending", summary.Verdict);
    }

    [Fact]
    public void Summarize_TracksDeletedRecordsAndTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        var editTime = now.AddHours(-2);
        var deleteTime = now.AddHours(-1);
        var entries = new[]
        {
            new ProfitEntry("e1", "user", new DateOnly(2026, 8, 1), 100_000, 0, 0, "", false,
                editTime, editTime, 1, "d", IntegrityHash: "hash"),
            new ProfitEntry("e2", "user", new DateOnly(2026, 8, 2), 0, 30_000, 0, "", true,
                deleteTime, deleteTime, 1, "d", IntegrityHash: "hash"),
        };

        var summary = AuditTrailCalculator.Summarize(entries, 2, 2, now);

        Assert.Equal(1, summary.ActiveRecordCount);
        Assert.Equal(1, summary.DeletedRecordCount);
        Assert.Equal(deleteTime, summary.LatestDeleteUtc);
    }
}

public sealed class V300AttachmentTests
{
    [Fact]
    public void ProfitEntry_AttachmentBase64_DefaultsEmpty()
    {
        var entry = new ProfitEntry("e1", "user", new DateOnly(2026, 8, 1), 100_000, 0, 0, "", false,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d");

        Assert.False(entry.HasAttachment);
        Assert.Equal(string.Empty, entry.AttachmentBase64);
    }

    [Fact]
    public void ProfitEntry_WithAttachment_IncrementsVersion()
    {
        var entry = new ProfitEntry("e1", "user", new DateOnly(2026, 8, 1), 100_000, 0, 0, "", false,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d");

        var updated = entry.WithAttachment(Convert.ToBase64String(new byte[] { 1, 2, 3 }));

        Assert.True(updated.HasAttachment);
        Assert.Equal(2, updated.Version);
        Assert.Equal(entry.EntryId, updated.EntryId);
    }
}
