using Maen.Accounting.App;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.ViewModels;

public sealed class PersonalCategoryItemViewModel
{
    public PersonalCategoryItemViewModel(PersonalCategorySummary summary, int rank)
    {
        Category = string.IsNullOrWhiteSpace(summary.Category) ? UiText.Get("T390") : summary.Category;
        AmountText = Money.Format(summary.AmountMinor);
        ShareText = $"{Math.Round(summary.SharePercent):0}%";
        Progress = Math.Clamp(summary.SharePercent / 100d, 0, 1);
        Rank = rank;
        AccentColor = rank switch
        {
            1 => "#C59A3D",
            2 => "#2E6F95",
            3 => "#137A53",
            _ => "#64748B"
        };
    }

    public string Category { get; }
    public string AmountText { get; }
    public string ShareText { get; }
    public double Progress { get; }
    public int Rank { get; }
    public string AccentColor { get; }
}
