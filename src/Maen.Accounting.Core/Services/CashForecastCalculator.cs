using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Projects cash flow for the next 90 days by combining actual recent
/// averages (daily income and spending) with scheduled recurring
/// obligations. Returns per-month projection slices plus a worst-case
/// month to guide planning. Amounts are in minor currency units.
/// </summary>
public static class CashForecastCalculator
{
    public const int ForecastDays = 90;

    public static CashForecast Forecast(
        IEnumerable<ProfitEntry> entries,
        IEnumerable<Obligation> obligations,
        DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(obligations);

        var entriesList = entries
            .Where(entry => !entry.IsDeleted)
            .Where(entry => entry.EntryDate >= asOf.AddDays(-ForecastDays) && entry.EntryDate < asOf)
            .ToArray();

        var minDate = entriesList.Length == 0 ? asOf : entriesList.Min(static entry => entry.EntryDate);
        var dayCount = Math.Max(asOf.DayNumber - minDate.DayNumber, 1);
        var dailyIncome = (double)entriesList.Sum(static entry => entry.SalesMinor) / dayCount;
        var dailySpending = (double)entriesList.Sum(static entry => checked(entry.CostMinor + entry.ExpensesMinor)) / dayCount;

        var upcoming = ObligationScheduleCalculator.UpcomingEvents(
            obligations, asOf, ForecastDays);

        var slices = new List<CashForecastSlice>();
        var endDate = asOf.AddDays(ForecastDays);
        for (var cursor = new DateOnly(asOf.Year, asOf.Month, 1);
             cursor < endDate;
             cursor = cursor.AddMonths(1))
        {
            var monthStart = cursor > asOf ? cursor : asOf;
            var monthEnd = cursor.AddMonths(1);
            if (monthEnd > endDate)
            {
                monthEnd = endDate;
            }

            if (monthStart >= monthEnd)
            {
                break;
            }

            var monthDays = monthEnd.DayNumber - monthStart.DayNumber;
            var projectedIncome = (long)(dailyIncome * monthDays);
            var projectedSpending = (long)(dailySpending * monthDays);
            var obligationsTotal = upcoming
                .Where(@event => @event.DueDate >= monthStart && @event.DueDate < monthEnd && !@event.IsPaid)
                .Sum(static @event => @event.AmountMinor);

            var net = checked(projectedIncome - projectedSpending - obligationsTotal);
            slices.Add(new CashForecastSlice(
                cursor.Year, cursor.Month,
                projectedIncome,
                projectedSpending,
                obligationsTotal,
                net));
        }

        var worst = slices.Count == 0
            ? null
            : slices.MinBy(static slice => slice.NetMinor);

        return new CashForecast(slices.ToArray(), worst);
    }
}

public sealed record CashForecastSlice(
    int Year,
    int Month,
    long ProjectedIncomeMinor,
    long ProjectedSpendingMinor,
    long ObligationsMinor,
    long NetMinor)
{
    public string MonthLabel => new DateTime(Year, Month, 1).ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
    public string ProjectedIncomeText => Money.Format(ProjectedIncomeMinor);
    public string ProjectedSpendingText => Money.Format(ProjectedSpendingMinor);
    public string ObligationsText => Money.Format(ObligationsMinor);
    public string NetText => Money.Format(NetMinor);
    public string NetColor => NetMinor >= 0 ? "#1B7A43" : "#B23B3B";
}

public sealed record CashForecast(IReadOnlyList<CashForecastSlice> Slices, CashForecastSlice? WorstSlice)
{
    public bool HasForecast => Slices.Count > 0;
}
