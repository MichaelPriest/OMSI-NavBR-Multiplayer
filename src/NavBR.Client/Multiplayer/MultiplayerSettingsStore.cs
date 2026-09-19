using System.IO;
using System.Text.Json;
using NavBR.Client.Driver;
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
        SynchronizeDriverProfileName(settings.DisplayName);
    }

    private static void SynchronizeDriverProfileName(string displayName)
    {
        var profile = DriverProfileStore.Load();
        if (string.Equals(profile.DisplayName, displayName, StringComparison.Ordinal))
        {
            return;
        }

        DriverProfileStore.Update(current => current with { DisplayName = displayName });
    }

    private static MultiplayerSettings Normalize(MultiplayerSettings settings)
    {
        var chat = NavBRHotkeyCatalog.Resolve(
            settings.ChatHotkey,
            NavBRHotkeyCatalog.DefaultChatHotkey).Name;
        var voice = NavBRHotkeyCatalog.Resolve(
            settings.VoiceHotkey,
            NavBRHotkeyCatalog.DefaultVoiceHotkey).Name;
        var displayName = string.IsNullOrWhiteSpace(settings.DisplayName)
            ? "Driver"
            : settings.DisplayName.Trim();
        if (displayName.Length > 80)
        {
            displayName = displayName[..80];
        }

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
        var modularDashboardMigration = settings.DashboardSettingsVersion < 3;
        var preset = HudProfileCatalog.ResolvePreset(settings.DashboardPreset);
        var theme = HudProfileCatalog.ResolveTheme(settings.DashboardTheme);
        var anchor = HudProfileCatalog.ResolveAnchor(settings.DashboardAnchor);
        var stopIconStyle = settings.StopIconStyle?.Trim().ToLowerInvariant() switch
        {
            "dot" => "dot",
            "custom" => "custom",
            _ => "omsi"
        };
        var customIconPath = string.IsNullOrWhiteSpace(settings.StopCustomIconPath)
            ? null
            : settings.StopCustomIconPath.Trim();
        var relayServerUrl = string.IsNullOrWhiteSpace(settings.RelayServerUrl)
            ? MultiplayerSettings.DefaultOnlineServerUrl
            : settings.RelayServerUrl.Trim();

        // Network settings version 2 makes the shared Render service the
        // out-of-box multiplayer transport. Only migrate the historical
        // loopback default; preserve any custom server the player selected.
        var legacyLoopbackDefault =
            settings.NetworkSettingsVersion < 2 &&
            string.Equals(
                settings.ServerUrl?.Trim(),
                "http://127.0.0.1:27730",
                StringComparison.OrdinalIgnoreCase);
        var serverUrl = string.IsNullOrWhiteSpace(settings.ServerUrl)
            ? MultiplayerSettings.DefaultOnlineServerUrl
            : legacyLoopbackDefault
                ? MultiplayerSettings.DefaultOnlineServerUrl
                : settings.ServerUrl.Trim();
        var enableApplicationRelay = legacyLoopbackDefault ||
                                     settings.EnableApplicationRelay;

        // Alpha.15 promotes physical remote buses from a hidden opt-in test to
        // the default multiplayer experience. Apply this once to pre-v3
        // settings; after any subsequent save, an explicit user opt-out is
        // preserved because NetworkSettingsVersion is written as 3.
        var enablePhysicalVehiclesByDefault =
            settings.NetworkSettingsVersion < 3;
        if (stopIconStyle == "custom" && customIconPath is null)
        {
            stopIconStyle = "omsi";
        }

        var dashboardWidth = modularDashboardMigration
            ? preset.Width
            : double.IsFinite(settings.DashboardWidth) ? settings.DashboardWidth : preset.Width;
        var dashboardHeight = modularDashboardMigration
            ? 0d
            : double.IsFinite(settings.DashboardHeight) ? settings.DashboardHeight : 0d;

        return settings with
        {
            DisplayName = displayName,
            ChatHotkey = chat,
            VoiceHotkey = voice,
            HudX = Math.Clamp(double.IsFinite(settings.HudX) ? settings.HudX : 0.02d, 0d, 1d),
            HudY = Math.Clamp(double.IsFinite(settings.HudY) ? settings.HudY : 1d, 0d, 1d),
            HudZoom = Math.Clamp(double.IsFinite(settings.HudZoom) ? settings.HudZoom : 1d, 0.65d, 10d),
            HudMapOpacity = Math.Clamp(double.IsFinite(settings.HudMapOpacity) ? settings.HudMapOpacity : 0.52d, 0.30d, 0.90d),
            DashboardSettingsVersion = 3,
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
                    : double.IsFinite(settings.DashboardScale) ? settings.DashboardScale : preset.Scale,
                0.60d,
                1.80d),
            DashboardOpacity = Math.Clamp(
                compactDashboardMigration
                    ? 0.78d
                    : double.IsFinite(settings.DashboardOpacity) ? settings.DashboardOpacity : preset.Opacity,
                0.35d,
                1d),
            DashboardShowFuel = legacyDashboard || settings.DashboardShowFuel,
            DashboardShowPedals = legacyDashboard || settings.DashboardShowPedals,
            DashboardShowStatus = legacyDashboard || settings.DashboardShowStatus,
            DashboardPreset = preset.Id,
            DashboardTheme = theme.Id,
            DashboardAnchor = anchor,
            DashboardWidth = Math.Clamp(dashboardWidth, 280d, 960d),
            DashboardHeight = Math.Clamp(dashboardHeight, 0d, 720d),
            DashboardMinimapScale = Math.Clamp(
                double.IsFinite(settings.DashboardMinimapScale) ? settings.DashboardMinimapScale : 1d,
                0.55d,
                2d),
            DashboardMultiplayerScale = Math.Clamp(
                double.IsFinite(settings.DashboardMultiplayerScale) ? settings.DashboardMultiplayerScale : 1d,
                0.55d,
                2d),
            DashboardAlertsScale = Math.Clamp(
                double.IsFinite(settings.DashboardAlertsScale) ? settings.DashboardAlertsScale : 1d,
                0.55d,
                2d),
            DashboardSideIndicatorsScale = Math.Clamp(
                double.IsFinite(settings.DashboardSideIndicatorsScale) ? settings.DashboardSideIndicatorsScale : 1d,
                0.55d,
                2d),
            StopIconStyle = stopIconStyle,
            StopCustomIconPath = customIconPath,
            ExperimentalPhysicalVehiclesEnabled =
                enablePhysicalVehiclesByDefault || settings.ExperimentalPhysicalVehiclesEnabled,
            NetworkSettingsVersion = 3,
            ServerUrl = serverUrl,
            EnableApplicationRelay = enableApplicationRelay,
            RelayServerUrl = relayServerUrl
        };
    }
}
