using System.Globalization;
using System.Text;
using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

public static class ReportCsvExporter
{
    public static string Build(IEnumerable<ProfitEntry> entries, IReadOnlyList<string> headers)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(headers);

        if (headers.Count != 10)
        {
            throw new ArgumentException("A report export requires exactly ten headers.", nameof(headers));
        }

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", headers.Select(Escape)));

        foreach (var entry in entries.Where(static entry => !entry.IsDeleted).OrderBy(static entry => entry.EntryDate))
        {
            var cells = new[]
            {
                entry.EntryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Money.ToDecimal(entry.SalesMinor).ToString("0.000", CultureInfo.InvariantCulture),
                Money.ToDecimal(entry.CostMinor).ToString("0.000", CultureInfo.InvariantCulture),
                Money.ToDecimal(entry.ExpensesMinor).ToString("0.000", CultureInfo.InvariantCulture),
                Money.ToDecimal(entry.NetProfitMinor).ToString("0.000", CultureInfo.InvariantCulture),
                entry.MovementType,
                entry.Wallet,
                entry.Category,
                entry.Counterparty,
                entry.Notes
            };

            builder.AppendLine(string.Join(",", cells.Select(Escape)));
        }

        return builder.ToString();
    }

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"', StringComparison.Ordinal) || text.Contains(',', StringComparison.Ordinal) || text.Contains('\n', StringComparison.Ordinal) || text.Contains('\r', StringComparison.Ordinal))
        {
            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return text;
    }
}
