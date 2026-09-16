using System.IO;
using System.Text.Json;

namespace NavBR.Client.Diagnostics;

internal static class DiagnosticsConsentStore
{
    private static readonly object Sync = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer");
    private static readonly string SettingsPath = Path.Combine(DirectoryPath, "diagnostics.json");

    public static bool IsEnabled
    {
        get
        {
            lock (Sync)
            {
                try
                {
                    if (!File.Exists(SettingsPath))
                    {
                        return false;
                    }

                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<DiagnosticsConsentSettings>(json);
                    return settings?.Enabled == true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }

    public static void SetEnabled(bool enabled)
    {
        lock (Sync)
        {
            Directory.CreateDirectory(DirectoryPath);
            var payload = JsonSerializer.Serialize(
                new DiagnosticsConsentSettings(enabled, DateTimeOffset.UtcNow),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, payload);
        }
    }

    private sealed record DiagnosticsConsentSettings(bool Enabled, DateTimeOffset UpdatedUtc);
}
