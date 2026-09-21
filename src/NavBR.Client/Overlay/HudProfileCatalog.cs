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
            "Barra superior, minimapa, multiplayer, voz e status usando apenas estado real do NavBR.",
            820d, 0.88d, 0.86d,
            true, false, true, true, true, true, true),
        new(
            "transit-control",
            "Transit Control",
            "transit-control",
            "Operação profissional",
            "Painel operacional amplo com rota, próxima parada, atraso, mapa, multiplayer e estados do veículo.",
            860d, 0.90d, 0.92d,
            true, false, true, true, true, true, true),
        new(
            "cockpit-digital",
            "Cockpit Digital",
            "cockpit-digital",
            "Painel digital moderno",
            "Cluster central moderno com velocidade e serviço em destaque, minimapa compacto e baixa obstrução da cabine.",
            780d, 0.90d, 0.94d,
            true, false, true, true, false, true, true),
        new(
            "navigation-pro",
            "Navigation Pro",
            "navigation-pro",
            "GPS / route advisor",
            "Prioriza navegação, rota, próxima parada e um minimapa maior para linhas e mapas menos conhecidos.",
            800d, 0.90d, 0.92d,
            true, false, true, true, false, true, true),
        new(
            "multiplayer-focus",
            "Multiplayer Focus",
            "multiplayer-focus",
            "Comboio / RP online",
            "Destaca jogadores próximos, distância, ping, chat e voz/PTT sem perder mapa, rota e telemetria local.",
            820d, 0.90d, 0.90d,
            true, false, true, true, true, true, true),
        new(
            "classic-omsi-plus",
            "Classic OMSI+",
            "classic-omsi-plus",
            "OMSI clássico modernizado",
            "Visual âmbar e compacto inspirado nos displays tradicionais do OMSI, com dados atuais do NavBR.",
            720d, 0.88d, 0.94d,
            true, false, true, false, false, true, true),
        new(
            "minimal-driver",
            "Minimal Driver",
            "minimal-driver",
            "Direção limpa",
            "Mostra somente serviço, velocidade, próxima parada e alertas essenciais; o minimapa aparece apenas quando necessário para retorno à rota.",
            620d, 0.82d, 0.78d,
            false, false, true, true, false, true, true),
        new(
            "streamer-broadcast",
            "Streamer / Broadcast",
            "streamer-broadcast",
            "Live / gravação",
            "Mantém o centro da tela livre e distribui rota, mapa e multiplayer nas bordas para transmissões e vídeos.",
            820d, 0.86d, 0.80d,
            true, false, true, true, true, true, true),
        new(
            "glass-night",
            "Glass / Night HUD",
            "glass-night",
            "Condução noturna",
            "Painéis escuros translúcidos e discretos para dirigir à noite sem esconder a cabine.",
            780d, 0.84d, 0.64d,
            true, false, true, true, true, true, true),
        new(
            "city-operations",
            "City Operations",
            "city-operations",
            "Gestão urbana",
            "Composição modular de operação com mapa maior, serviço, estados do veículo e multiplayer simultâneos.",
            900d, 0.92d, 0.90d,
            true, true, true, true, true, true, true),
        new(
            "driver-assistance",
            "Driver Assistance",
            "driver-assistance",
            "Assistência ao motorista",
            "Prioriza próxima parada, rota e alertas reais de portas, freio, ré, setas, luzes e saída operacional.",
            760d, 0.90d, 0.92d,
            true, false, true, true, false, true, true),
        new(
            "rp-urban",
            "RP Urbano",
            "urban-glass",
            "Open-world RP",
            "Cards compactos, vidro escuro e status com alto contraste para sessões multiplayer.",
            520d, 0.86d, 0.86d,
            true, false, true, true, true, true, true),
        new(
            "racing-minimal",
            "Corrida Minimal",
            "racing-clean",
            "Racing telemetry",
            "Velocidade grande, baixa obstrução e indicadores essenciais próximos ao campo de visão.",
            390d, 0.86d, 0.80d,
            false, true, true, false, false, true, true),
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
        new("transit-control", "Transit Control"),
        new("cockpit-digital", "Cockpit Digital"),
        new("navigation-pro", "Navigation Pro"),
        new("multiplayer-focus", "Multiplayer Focus"),
        new("classic-omsi-plus", "Classic OMSI+"),
        new("minimal-driver", "Minimal Driver"),
        new("streamer-broadcast", "Streamer / Broadcast"),
        new("glass-night", "Glass / Night HUD"),
        new("city-operations", "City Operations"),
        new("driver-assistance", "Driver Assistance"),
        new("urban-glass", "Urban Glass"),
        new("route-night", "Route Night"),
        new("racing-clean", "Racing Clean"),
        new("navbr-modern", "NavBR Modern"),
        new("bus-panel", "Painel de ônibus"),
        new("lcd", "LCD"),
        new("amber-classic", "Âmbar clássico"),
        new("light", "Claro")
    ];

    public static IReadOnlyList<string> Anchors { get; } =
    [
        "free",
        "custom",
        "top-left",
        "top-center",
        "top-right",
        "bottom-left",
        "bottom-center",
        "bottom-right"
    ];

    public static string NormalizePresetId(string? id) =>
        (id ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "transit-pro" => "transit-control",
            "route-advisor" => "navigation-pro",
            "digital-cluster" => "cockpit-digital",
            _ => (id ?? string.Empty).Trim().ToLowerInvariant()
        };

    public static bool IsComposedPreset(string? id) =>
        NormalizePresetId(id) is
            "immersive-operation" or
            "transit-control" or
            "cockpit-digital" or
            "navigation-pro" or
            "multiplayer-focus" or
            "classic-omsi-plus" or
            "minimal-driver" or
            "streamer-broadcast" or
            "glass-night" or
            "city-operations" or
            "driver-assistance";

    public static HudPresetDefinition ResolvePreset(string? id)
    {
        var normalized = NormalizePresetId(id);
        return Presets.FirstOrDefault(item => string.Equals(item.Id, normalized, StringComparison.OrdinalIgnoreCase))
            ?? Presets.Single(item => item.Id == DefaultPreset);
    }

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
