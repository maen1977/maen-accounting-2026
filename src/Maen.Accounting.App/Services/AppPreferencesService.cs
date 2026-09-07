using System.Globalization;

namespace Maen.Accounting.App.Services;

public enum AppLanguage
{
    Arabic,
    English
}

public enum AccountExperience
{
    Personal,
    Business,
    Wallet
}

public sealed class AppPreferencesService
{
    private const string OnboardingKey = "maen_onboarding_v2";
    private const string LanguageKey = "maen_language_v2";
    private const string ExperienceKey = "maen_experience_v2";

    public AppLanguage Language
    {
        get => Preferences.Default.Get(LanguageKey, nameof(AppLanguage.Arabic)) == nameof(AppLanguage.English)
            ? AppLanguage.English
            : AppLanguage.Arabic;
        set => Preferences.Default.Set(LanguageKey, value == AppLanguage.English ? nameof(AppLanguage.English) : nameof(AppLanguage.Arabic));
    }

    public AccountExperience Experience
    {
        get
        {
            var stored = Preferences.Default.Get(ExperienceKey, nameof(AccountExperience.Business));
            return Enum.TryParse<AccountExperience>(stored, out var experience) ? experience : AccountExperience.Business;
        }
        set => Preferences.Default.Set(ExperienceKey, value.ToString());
    }

    public string StorageScope => Experience switch
    {
        AccountExperience.Personal => "personal",
        AccountExperience.Wallet => "wallet",
        _ => "business"
    };

    public bool IsConfigured => Preferences.Default.Get(OnboardingKey, false);

    public void CompleteOnboarding(AppLanguage language, AccountExperience experience)
    {
        Language = language;
        Experience = experience;
        Preferences.Default.Set(OnboardingKey, true);
        ApplyCulture();
    }

    public void ApplyCulture()
    {
        UiText.Language = Language;
        var culture = Language == AppLanguage.English
            ? CultureInfo.GetCultureInfo("en-US")
            : CultureInfo.GetCultureInfo("ar-JO");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public void ResetOnboarding()
    {
        Preferences.Default.Remove(OnboardingKey);
        Preferences.Default.Remove(LanguageKey);
        Preferences.Default.Remove(ExperienceKey);
    }
}
