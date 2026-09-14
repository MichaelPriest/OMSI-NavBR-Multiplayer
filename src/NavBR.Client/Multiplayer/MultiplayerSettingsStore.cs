using System.IO;
using System.Text.Json;
using NavBR.Client.Overlay;

namespace NavBR.Client.Multiplayer;

public static class MultiplayerSettingsStore
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer");

    private static readonly string SettingsPath = Path.Combine(
        SettingsDirectory,
        "multiplayer.json");

    public static event Action<MultiplayerSettings>? SettingsSaved;

    public static MultiplayerSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return MultiplayerSettings.CreateDefault();
            }

            var settings = JsonSerializer.Deserialize<MultiplayerSettings>(
                File.ReadAllText(SettingsPath));

            if (settings is null || string.IsNullOrWhiteSpace(settings.PlayerId))
            {
                return MultiplayerSettings.CreateDefault();
            }

            return Normalize(settings);
        }
        catch
        {
            return MultiplayerSettings.CreateDefault();
        }
    }

    public static void Save(MultiplayerSettings settings)
    {
        settings = Normalize(settings);
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(SettingsPath, json);
        SettingsSaved?.Invoke(settings);
    }

    private static MultiplayerSettings Normalize(MultiplayerSettings settings)
    {
        var chat = NavBRHotkeyCatalog.Resolve(
            settings.ChatHotkey,
            NavBRHotkeyCatalog.DefaultChatHotkey).Name;
        var voice = NavBRHotkeyCatalog.Resolve(
            settings.VoiceHotkey,
            NavBRHotkeyCatalog.DefaultVoiceHotkey).Name;

        return settings with
        {
            ChatHotkey = chat,
            VoiceHotkey = voice,
            HudX = Math.Clamp(double.IsFinite(settings.HudX) ? settings.HudX : 0.02d, 0d, 1d),
            HudY = Math.Clamp(double.IsFinite(settings.HudY) ? settings.HudY : 1d, 0d, 1d),
            HudZoom = Math.Clamp(double.IsFinite(settings.HudZoom) ? settings.HudZoom : 1d, 0.65d, 2.25d),
            HudMapOpacity = Math.Clamp(double.IsFinite(settings.HudMapOpacity) ? settings.HudMapOpacity : 0.58d, 0.30d, 0.90d)
        };
    }
}
