using System.Globalization;

namespace LeDormeur.Localization;

public enum AppLanguage
{
    French,
    English,
    Spanish,
    German,
    Italian,
    Portuguese,
    Dutch,
    Russian,
    Chinese
}

public static class AppLanguageExtensions
{
    /// <summary>Display order in the language combo.</summary>
    public static readonly AppLanguage[] All =
    [
        AppLanguage.French,
        AppLanguage.English,
        AppLanguage.Spanish,
        AppLanguage.German,
        AppLanguage.Italian,
        AppLanguage.Portuguese,
        AppLanguage.Dutch,
        AppLanguage.Russian,
        AppLanguage.Chinese
    ];

    public static string ToCode(this AppLanguage language) => language switch
    {
        AppLanguage.English => "en",
        AppLanguage.Spanish => "es",
        AppLanguage.German => "de",
        AppLanguage.Italian => "it",
        AppLanguage.Portuguese => "pt",
        AppLanguage.Dutch => "nl",
        AppLanguage.Russian => "ru",
        AppLanguage.Chinese => "zh",
        _ => "fr"
    };

    /// <summary>Maps a saved code to a language. Unknown or empty → null.</summary>
    public static AppLanguage? TryFromCode(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "fr" => AppLanguage.French,
        "en" => AppLanguage.English,
        "es" => AppLanguage.Spanish,
        "de" => AppLanguage.German,
        "it" => AppLanguage.Italian,
        "pt" => AppLanguage.Portuguese,
        "nl" => AppLanguage.Dutch,
        "ru" => AppLanguage.Russian,
        "zh" => AppLanguage.Chinese,
        _ => null
    };

    /// <summary>Maps a saved code; unknown values fall back to French.</summary>
    public static AppLanguage FromCode(string? code) =>
        TryFromCode(code) ?? AppLanguage.French;

    public static string DisplayName(this AppLanguage language) => language switch
    {
        AppLanguage.English => "English",
        AppLanguage.Spanish => "Español",
        AppLanguage.German => "Deutsch",
        AppLanguage.Italian => "Italiano",
        AppLanguage.Portuguese => "Português",
        AppLanguage.Dutch => "Nederlands",
        AppLanguage.Russian => "Русский",
        AppLanguage.Chinese => "中文",
        _ => "Français"
    };

    /// <summary>
    /// Picks the best supported UI language from the Windows UI culture.
    /// Falls back to English when the system language is not supported.
    /// </summary>
    public static AppLanguage DetectFromSystem()
    {
        for (var culture = CultureInfo.CurrentUICulture;
             !string.IsNullOrEmpty(culture.Name);
             culture = culture.Parent)
        {
            var match = TryFromCode(culture.TwoLetterISOLanguageName);
            if (match.HasValue)
                return match.Value;
        }

        return AppLanguage.English;
    }
}
