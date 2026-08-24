using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.ViewModels;

internal static class PaletteColor
{
    private static readonly string[] Colors =
    [
        "#C59A3D", "#2E6F95", "#137A53", "#A0528F",
        "#7B6EB8", "#D08E2F", "#3E8F8F", "#C2413A"
    ];

    public static string ForRank(int rank) => Colors[(rank - 1) % Colors.Length];
}

public sealed class CategoryBreakdownItem
{
    public CategoryBreakdownItem(CategoryBreakdown breakdown, CategoryReport report, string colorHex)
    {
        CategoryText = string.IsNullOrWhiteSpace(breakdown.Category) ? UiText.Get("T390") : breakdown.Category;
        SpentText = Money.Format(breakdown.SpentMinor);
        SummaryText = UiText.Format("T473", breakdown.EntriesCount.ToString(System.Globalization.CultureInfo.CurrentCulture));
        ShareText = $"{Math.Round(report.SharePercentFor(breakdown)):0}%";
        ShareProgress = Math.Clamp(report.SharePercentFor(breakdown) / 100d, 0, 1);
        CategoryColor = Microsoft.Maui.Graphics.Color.FromArgb(colorHex);
    }

    public string CategoryText { get; }
    public string SpentText { get; }
    public string SummaryText { get; }
    public string ShareText { get; }
    public double ShareProgress { get; }
    public Microsoft.Maui.Graphics.Color CategoryColor { get; }
}

public sealed class SavingsTrendPointItem
{
    public SavingsTrendPointItem(SavingsTrendPoint point, long maxAbsSaved)
    {
        MonthText = new DateTime(point.Year, point.Month, 1).ToString("MMM");
        SavedText = FormatSigned(point.SavedMinor);
        StatusText = point.IsOnTarget ? UiText.Get("T475") : UiText.Get("T476");
        var progress = maxAbsSaved > 0 ? Math.Clamp(Math.Abs(point.SavedMinor) / (double)maxAbsSaved, 0, 1) : 0.0;
        SavedProgress = Math.Max(progress, 0.06);
        TrendColor = point.IsOnTarget ? Microsoft.Maui.Graphics.Color.FromArgb("#137A53") : Microsoft.Maui.Graphics.Color.FromArgb("#C8A45D");
    }

    private static string FormatSigned(long minor)
    {
        var text = Money.Format(minor);
        return minor < 0 ? text : text;
    }

    public string MonthText { get; }
    public string SavedText { get; }
    public string StatusText { get; }
    public double SavedProgress { get; }
    public Microsoft.Maui.Graphics.Color TrendColor { get; }
}
