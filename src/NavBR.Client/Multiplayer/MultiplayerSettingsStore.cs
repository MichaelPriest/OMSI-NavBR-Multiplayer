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

        // Older builds allowed chat/PTT to end up on the same chord. Keep the
        // user's valid selection whenever possible, but always migrate a
        // duplicate pair to distinct defaults so the HUD never advertises two
        // actions on the same shortcut.
        if (string.Equals(chat, voice, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(chat, NavBRHotkeyCatalog.DefaultVoiceHotkey, StringComparison.OrdinalIgnoreCase))
            {
                voice = NavBRHotkeyCatalog.DefaultVoiceHotkey;
            }
            else
            {
                chat = NavBRHotkeyCatalog.DefaultChatHotkey;
            }
        }

        var legacyDashboard = settings.DashboardSettingsVersion <= 0;
        var compactDashboardMigration = settings.DashboardSettingsVersion < 2;
        var stopIconStyle = settings.StopIconStyle?.Trim().ToLowerInvariant() switch
        {
            "dot" => "dot",
            "custom" => "custom",
            _ => "omsi"
        };
        var customIconPath = string.IsNullOrWhiteSpace(settings.StopCustomIconPath)
            ? null
            : settings.StopCustomIconPath.Trim();
        if (stopIconStyle == "custom" && customIconPath is null)
        {
            stopIconStyle = "omsi";
        }

        return settings with
        {
            ChatHotkey = chat,
            VoiceHotkey = voice,
            HudX = Math.Clamp(double.IsFinite(settings.HudX) ? settings.HudX : 0.02d, 0d, 1d),
            HudY = Math.Clamp(double.IsFinite(settings.HudY) ? settings.HudY : 1d, 0d, 1d),
            HudZoom = Math.Clamp(double.IsFinite(settings.HudZoom) ? settings.HudZoom : 1d, 0.65d, 10d),
            HudMapOpacity = Math.Clamp(double.IsFinite(settings.HudMapOpacity) ? settings.HudMapOpacity : 0.52d, 0.30d, 0.90d),
            DashboardSettingsVersion = 2,
            DashboardEnabled = legacyDashboard || settings.DashboardEnabled,
            DashboardX = Math.Clamp(
                legacyDashboard ? 0.02d : double.IsFinite(settings.DashboardX) ? settings.DashboardX : 0.02d,
                0d,
                1d),
            DashboardY = Math.Clamp(
                legacyDashboard ? 0.58d : double.IsFinite(settings.DashboardY) ? settings.DashboardY : 0.58d,
                0d,
                1d),
            DashboardScale = Math.Clamp(
                compactDashboardMigration
                    ? 0.82d
                    : double.IsFinite(settings.DashboardScale) ? settings.DashboardScale : 0.82d,
                0.70d,
                1.60d),
            DashboardOpacity = Math.Clamp(
                compactDashboardMigration
                    ? 0.78d
                    : double.IsFinite(settings.DashboardOpacity) ? settings.DashboardOpacity : 0.78d,
                0.45d,
                1d),
            DashboardShowFuel = legacyDashboard || settings.DashboardShowFuel,
            DashboardShowPedals = legacyDashboard || settings.DashboardShowPedals,
            DashboardShowStatus = legacyDashboard || settings.DashboardShowStatus,
            StopIconStyle = stopIconStyle,
            StopCustomIconPath = customIconPath
        };
    }
}
