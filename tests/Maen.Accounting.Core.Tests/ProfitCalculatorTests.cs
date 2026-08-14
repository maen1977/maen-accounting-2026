using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

public sealed class ProfitCalculatorTests
{
    [Fact]
    public void Calculates_sales_cost_expenses_and_net()
    {
        var entries = new[]
        {
            Entry("a", 10000, 4000, 1000),
            Entry("b", 5000, 2000, 500)
        };

        var summary = ProfitCalculator.Summarize(entries);

        Assert.Equal(15000, summary.SalesMinor);
        Assert.Equal(6000, summary.CostMinor);
        Assert.Equal(1500, summary.ExpensesMinor);
        Assert.Equal(9000, summary.GrossProfitMinor);
        Assert.Equal(7500, summary.NetProfitMinor);
        Assert.Equal(3750, summary.AverageNetMinor);
        Assert.Equal(2, summary.EntriesCount);
    }

    [Fact]
    public void Ignores_deleted_entries_in_all_summary_metrics()
    {
        var entries = new[]
        {
            Entry("active", 10000, 4000, 1000),
            Entry("deleted", 9000, 1000, 500) with { IsDeleted = true }
        };

        var summary = ProfitCalculator.Summarize(entries);

        Assert.Equal(10000, summary.SalesMinor);
        Assert.Equal(4000, summary.CostMinor);
        Assert.Equal(1000, summary.ExpensesMinor);
        Assert.Equal(6000, summary.GrossProfitMinor);
        Assert.Equal(5000, summary.NetProfitMinor);
        Assert.Equal(5000, summary.AverageNetMinor);
        Assert.Equal(1, summary.EntriesCount);
    }

    private static ProfitEntry Entry(string id, long sales, long cost, long expenses) => new(
        id, "user", new DateOnly(2026, 7, 1), sales, cost, expenses, string.Empty, false,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "device");
}
