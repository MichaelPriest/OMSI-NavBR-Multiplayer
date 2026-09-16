using NavBR.Client.Multiplayer;

namespace NavBR.Client.Overlay;

public sealed record HudPresetDefinition(
    string Id,
    string DisplayName,
    double Width,
    double Scale,
    double Opacity,
    bool ShowFuel,
    bool ShowPedals,
    bool ShowStatus,
    bool ShowMinimap,
    bool ShowMultiplayer,
    bool ShowAlerts,
    bool ShowSideIndicators);

public sealed record HudThemeDefinition(string Id, string DisplayName);

public static class HudProfileCatalog
{
    public const string DefaultPreset = "normal";
    public const string DefaultTheme = "navbr-modern";
    public const string DefaultAnchor = "free";

    public static IReadOnlyList<HudPresetDefinition> Presets { get; } =
    [
        new("compact", "Compacto", 330d, 0.76d, 0.84d, false, false, false, false, false, true, false),
        new("normal", "Normal", 470d, 0.82d, 0.82d, true, false, true, true, false, true, false),
        new("full", "Completo", 620d, 0.92d, 0.90d, true, true, true, true, true, true, true),
        new("digital-cluster", "Cluster Digital", 540d, 0.88d, 0.94d, true, false, true, false, false, true, true),
        new("lcd-amber", "LCD / Âmbar", 470d, 0.86d, 0.96d, true, false, true, false, false, true, false),
        new("transparent", "Transparente Integrado", 530d, 0.84d, 0.72d, true, false, true, true, true, true, true)
    ];

    public static IReadOnlyList<HudThemeDefinition> Themes { get; } =
    [
        new("navbr-modern", "NavBR Modern"),
        new("bus-panel", "Painel de ônibus"),
        new("lcd", "LCD"),
        new("amber-classic", "Âmbar clássico"),
        new("light", "Claro")
    ];

    public static IReadOnlyList<string> Anchors { get; } =
    [
        "free",
        "top-left",
        "top-center",
        "top-right",
        "bottom-left",
        "bottom-center",
        "bottom-right"
    ];

    public static HudPresetDefinition ResolvePreset(string? id) =>
        Presets.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? Presets.Single(item => item.Id == DefaultPreset);

    public static HudThemeDefinition ResolveTheme(string? id) =>
        Themes.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? Themes.Single(item => item.Id == DefaultTheme);

    public static string ResolveAnchor(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return Anchors.Contains(normalized ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            ? normalized!
            : DefaultAnchor;
    }

    public static MultiplayerSettings ApplyPreset(MultiplayerSettings settings, string? presetId)
    {
        var preset = ResolvePreset(presetId);
        return settings with
        {
            DashboardSettingsVersion = 3,
            DashboardPreset = preset.Id,
            DashboardWidth = preset.Width,
            DashboardScale = preset.Scale,
            DashboardOpacity = preset.Opacity,
            DashboardShowFuel = preset.ShowFuel,
            DashboardShowPedals = preset.ShowPedals,
            DashboardShowStatus = preset.ShowStatus,
            DashboardShowMinimap = preset.ShowMinimap,
            DashboardShowMultiplayer = preset.ShowMultiplayer,
            DashboardShowAlerts = preset.ShowAlerts,
            DashboardShowSideIndicators = preset.ShowSideIndicators
        };
    }

    public static MultiplayerSettings ApplySizePreset(MultiplayerSettings settings, string size) =>
        (size ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "small" or "pequeno" => settings with { DashboardScale = 0.72d },
            "large" or "grande" => settings with { DashboardScale = 1.20d },
            "xl" or "extra-large" => settings with { DashboardScale = 1.45d },
            _ => settings with { DashboardScale = 1d }
        };

    public static MultiplayerSettings Resize(
        MultiplayerSettings settings,
        double width,
        double height = 0d) =>
        settings with
        {
            DashboardWidth = Math.Clamp(double.IsFinite(width) ? width : 470d, 280d, 960d),
            DashboardHeight = Math.Clamp(double.IsFinite(height) ? height : 0d, 0d, 720d)
        };
}
