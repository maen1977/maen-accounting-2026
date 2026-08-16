using System.Globalization;
using System.Text;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Exports business monthly report slices to CSV text.
/// </summary>
public static class BusinessReportCsvExporter
{
    public static string Build(IReadOnlyList<MonthlySlice> slices)
    {
        ArgumentNullException.ThrowIfNull(slices);
        var builder = new StringBuilder();
        var headers = new[]
        {
            Escape(UiTextKeys.Get("T510")),
            Escape(UiTextKeys.Get("T504")),
            Escape(UiTextKeys.Get("T505")),
            Escape(UiTextKeys.Get("T506")),
            Escape(UiTextKeys.Get("T507")),
            Escape(UiTextKeys.Get("T508"))
        };
        builder.AppendLine(string.Join(",", headers));
        long totalSales = 0;
        long totalPurchases = 0;
        long totalReceipts = 0;
        long totalSupplier = 0;
        foreach (var slice in slices)
        {
            totalSales += slice.SalesMinor;
            totalPurchases += slice.PurchasesMinor;
            totalReceipts += slice.ReceiptsMinor;
            totalSupplier += slice.SupplierPaymentsMinor;
            builder.AppendLine(string.Join(",",
                Escape(slice.Month.ToString("yyyy-MM", CultureInfo.InvariantCulture)),
                Money.ToDecimal(slice.SalesMinor).ToString("0.00", CultureInfo.InvariantCulture),
                Money.ToDecimal(slice.PurchasesMinor).ToString("0.00", CultureInfo.InvariantCulture),
                Money.ToDecimal(slice.ReceiptsMinor).ToString("0.00", CultureInfo.InvariantCulture),
                Money.ToDecimal(slice.SupplierPaymentsMinor).ToString("0.00", CultureInfo.InvariantCulture),
                Money.ToDecimal(slice.NetCashMinor).ToString("0.00", CultureInfo.InvariantCulture)));
        }
        builder.AppendLine(string.Join(",",
            Escape(UiTextKeys.Get("T510")),
            Money.ToDecimal(totalSales).ToString("0.00", CultureInfo.InvariantCulture),
            Money.ToDecimal(totalPurchases).ToString("0.00", CultureInfo.InvariantCulture),
            Money.ToDecimal(totalReceipts).ToString("0.00", CultureInfo.InvariantCulture),
            Money.ToDecimal(totalSupplier).ToString("0.00", CultureInfo.InvariantCulture),
            Money.ToDecimal(totalReceipts - totalSupplier).ToString("0.00", CultureInfo.InvariantCulture)));
        return builder.ToString();
    }

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"', StringComparison.Ordinal)
            || text.Contains(',', StringComparison.Ordinal)
            || text.Contains('\n', StringComparison.Ordinal)
            || text.Contains('\r', StringComparison.Ordinal))
        {
            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return text;
    }
}
