using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

public sealed class ReportCsvExporterTests
{
    [Fact]
    public void Export_orders_entries_and_escapes_text()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new[]
        {
            new ProfitEntry("later", "user", new DateOnly(2026, 2, 2), 0, 2_000, 0, "ملاحظة, ثانية", false, now, now, 1, "device", 2_000, "purchase", "غذاء", "bank", "متجر \"الخير\""),
            new ProfitEntry("deleted", "user", new DateOnly(2026, 1, 1), 5_000, 0, 0, "محذوف", true, now, now, 1, "device"),
            new ProfitEntry("first", "user", new DateOnly(2026, 1, 2), 10_000, 0, 1_000, "راتب", false, now, now, 1, "device", 10_000, "salary", "عمل", "main", "جهة")
        };

        var csv = ReportCsvExporter.Build(entries, ["التاريخ", "المبيعات", "التكلفة", "المصروفات", "الصافي", "نوع الحركة", "المحفظة", "الفئة", "الجهة", "الملاحظات"]);
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("التاريخ,المبيعات", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("2026-01-02,100.00,0.00,10.00,90.00", lines[1], StringComparison.Ordinal);
        Assert.Contains("\"ملاحظة, ثانية\"", lines[2], StringComparison.Ordinal);
        Assert.Contains("\"متجر \"\"الخير\"\"\"", lines[2], StringComparison.Ordinal);
        Assert.DoesNotContain("محذوف", csv, StringComparison.Ordinal);
    }
}
