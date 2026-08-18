using System.Text.Json;
using System.Text.Json.Serialization;

namespace AmphetamineNet.Services;

/// <summary>
/// Persisted user preferences for AmphetamineNet
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Keeps the Mac awake with the lid closed
    /// </summary>
    /// <value>True when closed-lid keep-awake is enabled</value>
    public bool AllowClosedLid { get; set; } = true;

    /// <summary>
    /// Prevents the display from sleeping
    /// </summary>
    /// <value>True when display sleep is blocked</value>
    public bool PreventDisplaySleep { get; set; } = false;

    /// <summary>
    /// Starts a keep-awake session on launch
    /// </summary>
    /// <value>True when a session should start automatically</value>
    public bool StartWithSession { get; set; } = false;

    /// <summary>
    /// Last selected session duration in minutes
    /// </summary>
    /// <value>Duration in minutes, or zero for indefinite</value>
    public int DurationMinutes { get; set; } = 0; // 0 = indefinitely

    /// <summary>
    /// Remembered custom timer duration
    /// </summary>
    /// <value>Custom duration in minutes, if any</value>
    public int? CustomDurationMinutes { get; set; }

    /// <summary>
    /// Selected UI language code
    /// </summary>
    /// <value>BCP-47 language code</value>
    public string Language { get; set; } = Localization.DefaultLanguage;

    /// <summary>
    /// Launches without showing a main window
    /// </summary>
    /// <value>True when the app should start minimized</value>
    public bool LaunchMinimized { get; set; } = false;

    /// <summary>
    /// Path to the settings JSON file
    /// </summary>
    /// <value>Path to the settings JSON file</value>
    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AmphetamineNet",
            "settings.json");

    /// <summary>
    /// JSON serializer options for settings
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Loads settings from disk or returns defaults
    /// </summary>
    /// <returns>Loaded settings instance</returns>
    public static AppSettings Load()
    {
        try
        {
            var path = SettingsPath;
            if (!File.Exists(path))
                return new AppSettings();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Writes the current settings to disk
    /// </summary>
    public void Save()
    {
        try
        {
            var path = SettingsPath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            AppLog.Write($"settings save error: {ex.Message}");
        }
    }
}
