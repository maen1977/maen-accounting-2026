namespace Maen.Accounting.Core.Models;

public sealed record FinancialSummary(
    long SalesMinor,
    long CostMinor,
    long ExpensesMinor,
    long GrossProfitMinor,
    long NetProfitMinor,
    long AverageNetMinor,
    int EntriesCount);
