using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Enriches business monthly slices with month-over-month (MoM) and
/// year-over-year (YoY) deltas for sales and net cash, so the reports UI
/// can show trend arrows and percentages. Pure calculation.
/// </summary>
public static class BusinessComparisonCalculator
{
    public static IReadOnlyList<MonthlyComparison> Compare(
        IReadOnlyList<MonthlySlice> months)
    {
        ArgumentNullException.ThrowIfNull(months);
        var results = new List<MonthlyComparison>(months.Count);
        for (var index = 0; index < months.Count; index++)
        {
            var current = months[index];
            MonthlySlice? previous = index > 0 ? months[index - 1] : null;
            MonthlySlice? sameMonthLastYear = months.LastOrDefault(
                candidate => candidate.Month.Year == current.Month.Year - 1
                    && candidate.Month.Month == current.Month.Month);

            results.Add(new MonthlyComparison(
                current,
                previous?.SalesMinor ?? null,
                previous?.NetCashMinor ?? null,
                sameMonthLastYear?.SalesMinor ?? null,
                sameMonthLastYear?.NetCashMinor ?? null));
        }

        return results;
    }
}

public sealed record MonthlyComparison(
    MonthlySlice Slice,
    long? PreviousSalesMinor,
    long? PreviousNetCashMinor,
    long? YearAgoSalesMinor,
    long? YearAgoNetCashMinor)
{
    public long? SalesDeltaMinor => PreviousSalesMinor is null ? null : checked(Slice.SalesMinor - PreviousSalesMinor.Value);
    public double SalesDeltaPercent => PreviousSalesMinor is null || PreviousSalesMinor == 0 ? 0
        : checked((double)(Slice.SalesMinor - PreviousSalesMinor.Value) / Math.Abs(PreviousSalesMinor.Value)) * 100;
    public long? NetCashDeltaMinor => PreviousNetCashMinor is null ? null : checked(Slice.NetCashMinor - PreviousNetCashMinor.Value);
    public double NetCashDeltaPercent => PreviousNetCashMinor is null || PreviousNetCashMinor == 0 ? 0
        : checked((double)(Slice.NetCashMinor - PreviousNetCashMinor.Value) / Math.Abs(PreviousNetCashMinor.Value)) * 100;

    public long? SalesYoYDeltaMinor => YearAgoSalesMinor is null ? null : checked(Slice.SalesMinor - YearAgoSalesMinor.Value);
    public double SalesYoYDeltaPercent => YearAgoSalesMinor is null || YearAgoSalesMinor == 0 ? 0
        : checked((double)(Slice.SalesMinor - YearAgoSalesMinor.Value) / Math.Abs(YearAgoSalesMinor.Value)) * 100;

    public long? NetCashYoYDeltaMinor => YearAgoNetCashMinor is null ? null : checked(Slice.NetCashMinor - YearAgoNetCashMinor.Value);
    public double NetCashYoYDeltaPercent => YearAgoNetCashMinor is null || YearAgoNetCashMinor == 0 ? 0
        : checked((double)(Slice.NetCashMinor - YearAgoNetCashMinor.Value) / Math.Abs(YearAgoNetCashMinor.Value)) * 100;

    public string SalesDeltaText => FormatDelta(SalesDeltaMinor, SalesDeltaPercent, "MoM");
    public string NetCashDeltaText => FormatDelta(NetCashDeltaMinor, NetCashDeltaPercent, "MoM");
    public string SalesYoYText => FormatDelta(SalesYoYDeltaMinor, SalesYoYDeltaPercent, "YoY");
    public string NetCashYoYText => FormatDelta(NetCashYoYDeltaMinor, NetCashYoYDeltaPercent, "YoY");

    public bool SalesIsUp => SalesDeltaMinor > 0;
    public bool SalesIsFlat => SalesDeltaMinor == 0;

    private static string FormatDelta(long? delta, double percent, string kind)
    {
        if (delta is null)
        {
            return kind;
        }

        var sign = delta > 0 ? "+" : string.Empty;
        return $"{sign}{delta.Value:n0} ({sign}{percent:n0}%) {kind}";
    }
}
