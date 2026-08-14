using Maen.Accounting.App.Services;

namespace Maen.Accounting.App.Views;

public partial class OnboardingPage : ContentPage
{
    private readonly AppPreferencesService _preferences;
    private AppLanguage _language = AppLanguage.Arabic;
    private AccountExperience _experience = AccountExperience.Business;
    private bool _languageStepComplete;

    public event EventHandler? Completed;

    public OnboardingPage(AppPreferencesService preferences)
    {
        InitializeComponent();
        _preferences = preferences;
        ApplyLanguage();
        UpdateSelectionStyles();
    }

    private void OnArabicClicked(object? sender, EventArgs e)
    {
        _language = AppLanguage.Arabic;
        ApplyLanguage();
        UpdateSelectionStyles();
    }

    private void OnEnglishClicked(object? sender, EventArgs e)
    {
        _language = AppLanguage.English;
        ApplyLanguage();
        UpdateSelectionStyles();
    }

    private void OnLanguageContinueClicked(object? sender, EventArgs e)
    {
        _languageStepComplete = true;
        LanguageSection.IsVisible = false;
        AccountSection.IsVisible = true;
        StepNumberLabel.Text = "2";
        StepLabel.Text = UiText.Get("T015");
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _languageStepComplete = false;
        AccountSection.IsVisible = false;
        LanguageSection.IsVisible = true;
        StepNumberLabel.Text = "1";
        StepLabel.Text = UiText.Get("T013");
    }

    private void OnPersonalClicked(object? sender, EventArgs e)
    {
        _experience = AccountExperience.Personal;
        UpdateSelectionStyles();
    }

    private void OnBusinessClicked(object? sender, EventArgs e)
    {
        _experience = AccountExperience.Business;
        UpdateSelectionStyles();
    }

    private void OnContinueClicked(object? sender, EventArgs e)
    {
        if (!_languageStepComplete)
        {
            OnLanguageContinueClicked(sender, e);
            return;
        }

        _preferences.CompleteOnboarding(_language, _experience);
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyLanguage()
    {
        var english = _language == AppLanguage.English;
        UiText.Language = _language;
        FlowDirection = english ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;

        LogoLabel.Text = UiText.Get("T085");
        BrandLabel.Text = UiText.Get("T092");
        BrandSubtitleLabel.Text = UiText.Get("T098");
        WelcomeLabel.Text = UiText.Get("T089");
        IntroLabel.Text = UiText.Get("T014");
        LanguageTitleLabel.Text = UiText.Get("T013");
        LanguageHintLabel.Text = english ? "Your language controls the entire app interface." : "اختيارك سيطبّق على جميع شاشات التطبيق.";
        ExperienceTitleLabel.Text = UiText.Get("T015");
        ExperienceHintLabel.Text = UiText.Get("T103");
        PersonalButton.Text = UiText.Get("T027");
        BusinessButton.Text = UiText.Get("T036");
        ArabicButton.Text = UiText.Get("T037");
        EnglishButton.Text = UiText.Get("T001");
        LanguageContinueButton.Text = UiText.Get("T087");
        ContinueButton.Text = UiText.Get("T087");
        BackButton.Text = english ? "Back" : "رجوع";
        StepLabel.Text = UiText.Get(_languageStepComplete ? "T015" : "T013");
    }

    private void UpdateSelectionStyles()
    {
        var primary = Color.FromArgb("#0E9F6E");
        var secondary = Color.FromArgb("#EAF8F2");
        var primaryText = Colors.White;
        var secondaryText = Color.FromArgb("#087A55");

        ArabicButton.BackgroundColor = _language == AppLanguage.Arabic ? primary : secondary;
        ArabicButton.TextColor = _language == AppLanguage.Arabic ? primaryText : secondaryText;
        EnglishButton.BackgroundColor = _language == AppLanguage.English ? primary : secondary;
        EnglishButton.TextColor = _language == AppLanguage.English ? primaryText : secondaryText;
        PersonalButton.BackgroundColor = _experience == AccountExperience.Personal ? primary : secondary;
        PersonalButton.TextColor = _experience == AccountExperience.Personal ? primaryText : secondaryText;
        BusinessButton.BackgroundColor = _experience == AccountExperience.Business ? primary : secondary;
        BusinessButton.TextColor = _experience == AccountExperience.Business ? primaryText : secondaryText;
    }
}
