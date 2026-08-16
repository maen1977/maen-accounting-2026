using Microsoft.Maui.Controls;

namespace Maen.Accounting.App.Views;

public partial class BrandHeaderView : ContentView
{
    public static readonly BindableProperty EyebrowProperty = BindableProperty.Create(nameof(Eyebrow), typeof(string), typeof(BrandHeaderView), string.Empty);
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(BrandHeaderView), string.Empty);
    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(BrandHeaderView), string.Empty);
    public static readonly BindableProperty BadgeProperty = BindableProperty.Create(nameof(Badge), typeof(string), typeof(BrandHeaderView), string.Empty);
    public static readonly BindableProperty HasBadgeProperty = BindableProperty.Create(nameof(HasBadge), typeof(bool), typeof(BrandHeaderView), false);
    public static readonly BindableProperty StatusProperty = BindableProperty.Create(nameof(Status), typeof(string), typeof(BrandHeaderView), string.Empty);
    public static readonly BindableProperty HasStatusProperty = BindableProperty.Create(nameof(HasStatus), typeof(bool), typeof(BrandHeaderView), false);

    public BrandHeaderView() => InitializeComponent();

    public string Eyebrow { get => (string)GetValue(EyebrowProperty); set => SetValue(EyebrowProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string Badge { get => (string)GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public bool HasBadge { get => (bool)GetValue(HasBadgeProperty); set => SetValue(HasBadgeProperty, value); }
    public string Status { get => (string)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public bool HasStatus { get => (bool)GetValue(HasStatusProperty); set => SetValue(HasStatusProperty, value); }
}
