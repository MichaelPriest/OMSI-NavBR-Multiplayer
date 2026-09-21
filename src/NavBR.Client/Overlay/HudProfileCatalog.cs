using NavBR.Client.Multiplayer;

namespace NavBR.Client.Overlay;

public sealed record HudPresetDefinition(
    string Id,
    string DisplayName,
    string ThemeId,
    string Inspiration,
    string Description,
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
        new(
            "immersive-operation",
            "Imersivo / Operação",
            "immersive-operation",
            "HUD de operação em tela",
            "Modo opcional inspirado em interfaces de condução modernas: rota, próxima parada, minimapa, multiplayer, voz e status em um painel amplo sem substituir o HUD atual.",
            820d, 0.88d, 0.86d,
            true, false, true, true, true, true, true),
        new(
            "rp-urban",
            "RP Urbano",
            "urban-glass",
            "Open-world RP",
            "Cards compactos, vidro escuro e status com alto contraste para sessões multiplayer.",
            520d, 0.86d, 0.86d,
            true, false, true, true, true, true, true),
        new(
            "route-advisor",
            "Route Advisor",
            "route-night",
            "Truck / bus simulator",
            "Prioriza linha, destino, próxima parada e orientação de rota com leitura rápida.",
            560d, 0.88d, 0.91d,
            true, false, true, true, false, true, true),
        new(
            "racing-minimal",
            "Corrida Minimal",
            "racing-clean",
            "Racing telemetry",
            "Velocidade grande, baixa obstrução e indicadores essenciais próximos ao campo de visão.",
            390d, 0.86d, 0.80d,
            false, true, true, false, false, true, true),
        new(
            "transit-pro",
            "Transit Pro",
            "transit-control",
            "Transit operations",
            "Painel operacional para ônibus com atraso, parada solicitada, combustível e estados do veículo.",
            500d, 0.88d, 0.94d,
            true, false, true, true, true, true, true),
        new(
            "compact",
            "Compacto",
            "current",
            "NavBR",
            "Versão pequena para manter apenas alertas essenciais.",
            330d, 0.76d, 0.84d,
            false, false, false, false, false, true, false),
        new(
            "normal",
            "Normal (HUD atual)",
            "current",
            "NavBR",
            "Equilíbrio entre direção, navegação e informações do veículo.",
            470d, 0.82d, 0.82d,
            true, false, true, true, false, true, false),
        new(
            "full",
            "Completo",
            "current",
            "NavBR",
            "Ativa o conjunto completo de módulos disponíveis.",
            620d, 0.92d, 0.90d,
            true, true, true, true, true, true, true),
        new(
            "digital-cluster",
            "Cluster Digital",
            "current",
            "Digital cockpit",
            "Cluster escuro de alta legibilidade focado em velocidade e indicadores.",
            540d, 0.88d, 0.94d,
            true, false, true, false, false, true, true),
        new(
            "lcd-amber",
            "LCD / Âmbar",
            "current",
            "Classic bus display",
            "Visual âmbar inspirado em painéis eletrônicos de ônibus.",
            470d, 0.86d, 0.96d,
            true, false, true, false, false, true, false),
        new(
            "transparent",
            "Transparente Integrado",
            "current",
            "Glass HUD",
            "Camadas translúcidas para integrar o HUD ao cenário sem esconder a cabine.",
            530d, 0.84d, 0.72d,
            true, false, true, true, true, true, true)
    ];

    public static IReadOnlyList<HudThemeDefinition> Themes { get; } =
    [
        new("current", "Atual / NavBR Clássico"),
        new("immersive-operation", "Imersivo / Operação"),
        new("urban-glass", "Urban Glass"),
        new("route-night", "Route Night"),
        new("racing-clean", "Racing Clean"),
        new("transit-control", "Transit Control"),
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
            DashboardTheme = preset.ThemeId,
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
