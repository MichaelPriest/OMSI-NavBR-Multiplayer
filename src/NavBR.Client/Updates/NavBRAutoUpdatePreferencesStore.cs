using System.Text.Json;

namespace NavBR.Client.Updates;

internal sealed record NavBRAutoUpdatePreferences(
    string Channel = "alpha",
    bool CheckAtStartup = true,
    bool AutoDownload = true);

internal static class NavBRAutoUpdatePreferencesStore
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer");

    private static readonly string SettingsPath = Path.Combine(
        SettingsDirectory,
        "updates.json");

    private static readonly object Sync = new();
    private static NavBRAutoUpdatePreferences? _cached;

    public static NavBRAutoUpdatePreferences Load()
    {
        lock (Sync)
        {
            if (_cached is not null)
            {
                return _cached;
            }

            try
            {
                if (!File.Exists(SettingsPath))
                {
                    _cached = Normalize(new NavBRAutoUpdatePreferences());
                    return _cached;
                }

                var parsed =
                    JsonSerializer.Deserialize<NavBRAutoUpdatePreferences>(
                        File.ReadAllText(SettingsPath));
                _cached = Normalize(
                    parsed ?? new NavBRAutoUpdatePreferences());
            }
            catch
            {
                _cached = Normalize(new NavBRAutoUpdatePreferences());
            }

            return _cached;
        }
    }

    public static NavBRAutoUpdatePreferences Save(
        NavBRAutoUpdatePreferences preferences)
    {
        preferences = Normalize(preferences);
        lock (Sync)
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(
                SettingsPath,
                JsonSerializer.Serialize(
                    preferences,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
            _cached = preferences;
        }

        return preferences;
    }

    private static NavBRAutoUpdatePreferences Normalize(
        NavBRAutoUpdatePreferences preferences) =>
        preferences with
        {
            Channel = string.Equals(
                preferences.Channel?.Trim(),
                "stable",
                StringComparison.OrdinalIgnoreCase)
                ? "stable"
                : "alpha"
        };
}
