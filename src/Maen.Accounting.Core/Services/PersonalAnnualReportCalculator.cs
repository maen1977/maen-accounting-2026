using Maen.Accounting.Core.Models;
namespace Maen.Accounting.Core.Services;
/// <summary>
/// Builds a year-at-a-glance report for personal finances: one row per month with
/// income, spending, and net savings, plus year totals and a previous-year comparison.
/// Pure calculation — no UI, persistence, or localization dependency.
/// </summary>
public static class PersonalAnnualReportCalculator
{
    public static PersonalAnnualReport Build(
        IEnumerable<ProfitEntry> entries,
        int year)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var activeEntries = entries
            .Where(static entry => !entry.IsDeleted)
            .ToArray();
        var months = new PersonalMonthlySnapshot[12];
        for (var month = 1; month <= 12; month++)
        {
            var monthEntries = activeEntries
                .Where(entry => entry.EntryDate.Year == year && entry.EntryDate.Month == month)
                .ToArray();
            var income = monthEntries.Sum(static entry => entry.SalesMinor);
            var spending = monthEntries
                .Where(entry => entry.MovementType is PersonalMovementTypes.Purchase
                    or PersonalMovementTypes.Expense
                    or PersonalMovementTypes.Withdrawal)
                .Sum(static entry => checked(entry.CostMinor + entry.ExpensesMinor) > 0
                    ? checked(entry.CostMinor + entry.ExpensesMinor)
                    : entry.EffectiveAmountMinor);
            var net = checked(income - spending);
            months[month - 1] = new PersonalMonthlySnapshot(
                Year: year,
                Month: month,
                IncomeMinor: income,
                SpendingMinor: spending,
                NetMinor: net);
        }
        var previousMonths = months
            .Select(month => month with { Year = year - 1 })
            .ToArray();
        return new PersonalAnnualReport(
            Year: year,
            Months: months,
            TotalIncomeMinor: months.Sum(static month => month.IncomeMinor),
            TotalSpendingMinor: months.Sum(static month => month.SpendingMinor),
            TotalNetMinor: months.Sum(static month => month.NetMinor),
            BestMonth: months.OrderByDescending(static month => month.NetMinor).First(),
            WorstMonth: months.OrderBy(static month => month.NetMinor).First());
    }
    public static double YearOverYearIncomeChangePercent(
        IReadOnlyList<PersonalMonthlySnapshot> current,
        IReadOnlyList<PersonalMonthlySnapshot> previous)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(previous);
        var currentIncome = current.Sum(static month => month.IncomeMinor);
        var previousIncome = previous.Sum(static month => month.IncomeMinor);
        if (previousIncome <= 0)
        {
            return currentIncome > 0 ? 100 : 0;
        }
        return checked((double)(currentIncome - previousIncome) / previousIncome) * 100;
    }
}
public sealed record PersonalAnnualReport(
    int Year,
    IReadOnlyList<PersonalMonthlySnapshot> Months,
    long TotalIncomeMinor,
    long TotalSpendingMinor,
    long TotalNetMinor,
    PersonalMonthlySnapshot BestMonth,
    PersonalMonthlySnapshot WorstMonth);
public sealed record PersonalMonthlySnapshot(
    int Year,
    int Month,
    long IncomeMinor,
    long SpendingMinor,
    long NetMinor);
