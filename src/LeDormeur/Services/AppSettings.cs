using System.Text.Json;

namespace LeDormeur.Services;

public sealed class AppSettings
{
    public int Hours { get; set; } = 1;
    public int Minutes { get; set; }
    public int BrightnessToRemove { get; set; } = 50;
    /// <summary>
    /// UI language code: fr, en, es, de, it, pt, nl, ru, zh.
    /// Null/empty means not saved yet — the app will detect from Windows UI culture.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>Automatic presence check + sleep at a fixed clock time.</summary>
    public bool AutoModeEnabled { get; set; }

    /// <summary>Hour of day (0–23) when auto mode triggers.</summary>
    public int AutoModeHour { get; set; } = 23;

    /// <summary>Minute of hour (0–59) when auto mode triggers.</summary>
    public int AutoModeMinute { get; set; }

    /// <summary>Minutes to wait for a "still there?" response before sleeping.</summary>
    public int AutoModeTimeoutMinutes { get; set; } = 5;

    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LeDormeur");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public TimeSpan AutoModeTimeOfDay =>
        new(AutoModeHour, AutoModeMinute, 0);

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings is null)
                return new AppSettings();

            settings.Normalize();
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Normalize();
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Do not block the app if writing fails
        }
    }

    public void Normalize()
    {
        Hours = Math.Clamp(Hours, 0, 12);
        Minutes = Math.Clamp(Minutes, 0, 59);
        BrightnessToRemove = Math.Clamp(BrightnessToRemove, 0, 100);

        if (Hours == 0 && Minutes == 0)
            Hours = 1;

        if (string.IsNullOrWhiteSpace(Language))
        {
            Language = null;
        }
        else
        {
            Language = Language.Trim().ToLowerInvariant() switch
            {
                "en" => "en",
                "es" => "es",
                "de" => "de",
                "it" => "it",
                "pt" => "pt",
                "nl" => "nl",
                "ru" => "ru",
                "zh" => "zh",
                "fr" => "fr",
                _ => "fr"
            };
        }

        AutoModeHour = Math.Clamp(AutoModeHour, 0, 23);
        AutoModeMinute = Math.Clamp(AutoModeMinute, 0, 59);
        AutoModeTimeoutMinutes = Math.Clamp(AutoModeTimeoutMinutes, 1, 120);
    }
}
