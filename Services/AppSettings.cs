using System.Text.Json;
using System.Text.Json.Serialization;

namespace AmphetamineNet.Services;

public sealed class AppSettings
{
    public bool AllowClosedLid { get; set; } = true;
    public bool PreventDisplaySleep { get; set; } = false;
    public bool StartWithSession { get; set; } = false;
    public int DurationMinutes { get; set; } = 0; // 0 = indefinitely
    public bool LaunchMinimized { get; set; } = false;

    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AmphetamineNet",
            "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

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

    public void Save()
    {
        var path = SettingsPath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}
