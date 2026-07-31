using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

public static class ProfitCalculator
{
    public static FinancialSummary Summarize(IEnumerable<ProfitEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        long sales = 0;
        long cost = 0;
        long expenses = 0;
        var count = 0;

        foreach (var entry in entries.Where(static entry => !entry.IsDeleted))
        {
            sales = checked(sales + entry.SalesMinor);
            cost = checked(cost + entry.CostMinor);
            expenses = checked(expenses + entry.ExpensesMinor);
            count++;
        }

        var gross = checked(sales - cost);
        var net = checked(gross - expenses);
        var average = count == 0 ? 0 : net / count;
        return new FinancialSummary(sales, cost, expenses, gross, net, average, count);
    }
}
