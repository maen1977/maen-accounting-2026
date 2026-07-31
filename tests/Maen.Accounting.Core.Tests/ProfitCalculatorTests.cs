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
        Assert.Equal(7500, summary.NetProfitMinor);
        Assert.Equal(3750, summary.AverageNetMinor);
    }

    private static ProfitEntry Entry(string id, long sales, long cost, long expenses) => new(
        id, "user", new DateOnly(2026, 7, 1), sales, cost, expenses, string.Empty, false,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "device");
}
