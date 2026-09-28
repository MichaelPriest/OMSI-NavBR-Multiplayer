using System.IO;
using System.Text.Json;

namespace NavBR.Client.Windows;

internal sealed record Alpha12Preferences(
    bool FirstRunCompleted = false,
    bool AdvancedModeEnabled = false,
    string HudTheme = "classic",
    bool ShowDrivingTips = true)
{
    public static Alpha12Preferences Default { get; } = new();
}

internal static class Alpha12PreferencesStore
{
    private static readonly object Sync = new();
    private static Alpha12Preferences? _cachedPreferences;

    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer");

    private static readonly string FilePath = Path.Combine(DirectoryPath, "alpha12-preferences.json");

    public static event Action<Alpha12Preferences>? PreferencesSaved;

    public static Alpha12Preferences Load()
    {
        lock (Sync)
        {
            if (_cachedPreferences is not null)
            {
                return _cachedPreferences;
            }

            try
            {
                if (!File.Exists(FilePath))
                {
                    _cachedPreferences = Alpha12Preferences.Default;
                    return _cachedPreferences;
                }

                var preferences = JsonSerializer.Deserialize<Alpha12Preferences>(File.ReadAllText(FilePath));
                _cachedPreferences = Normalize(preferences ?? Alpha12Preferences.Default);
                return _cachedPreferences;
            }
            catch
            {
                _cachedPreferences = Alpha12Preferences.Default;
                return _cachedPreferences;
            }
        }
    }

    public static void Save(Alpha12Preferences preferences)
    {
        preferences = Normalize(preferences);
        lock (Sync)
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(
                FilePath,
                JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true }));
            _cachedPreferences = preferences;
        }

        PreferencesSaved?.Invoke(preferences);
    }

    private static Alpha12Preferences Normalize(Alpha12Preferences preferences)
    {
        var hudTheme = preferences.HudTheme?.Trim().ToLowerInvariant() switch
        {
            "amber" => "amber",
            "lcd" => "lcd",
            "minimal" => "minimal",
            _ => "classic"
        };

        return preferences with { HudTheme = hudTheme };
    }
}
