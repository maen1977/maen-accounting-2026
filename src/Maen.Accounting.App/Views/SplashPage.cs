using Maen.Accounting.App.Services;

namespace Maen.Accounting.App.Views;

public sealed class SplashPage : ContentPage
{
    public SplashPage()
    {
        FlowDirection = UiText.Language == AppLanguage.English
            ? FlowDirection.LeftToRight
            : FlowDirection.RightToLeft;
        Content = new VerticalStackLayout
        {
            Spacing = 20,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Image { Source = "maen_logo.png", WidthRequest = 120, HeightRequest = 120 },
                new Label { Text = UiText.Get("T092"), FontSize = 28, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center },
                new ActivityIndicator { IsRunning = true, Color = Color.FromArgb("#0E9F6E") }
            }
        };
    }
}
