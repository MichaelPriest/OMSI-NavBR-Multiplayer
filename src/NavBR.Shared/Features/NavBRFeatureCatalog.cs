namespace NavBR.Shared.Features;

public enum NavBRFeatureState
{
    Ready,
    Experimental,
    InDevelopment,
    Planned
}

public sealed record NavBRFeatureDescriptor(
    string Id,
    string Name,
    NavBRFeatureState State,
    bool IncludedInAlpha12,
    bool RequiresExplicitOptIn = false,
    string? Notes = null);

public static class NavBRFeatureCatalog
{
    public const string TargetVersion = "0.3.0-alpha.12";

    public static IReadOnlyList<NavBRFeatureDescriptor> All { get; } =
    [
        new("core-ui", "Core UI and localization", NavBRFeatureState.InDevelopment, true),
        new("telemetry", "OMSI telemetry and compatibility", NavBRFeatureState.Experimental, true),
        new("navigation", "GPS, HUD, TTData and navigation", NavBRFeatureState.InDevelopment, true),
        new("peer-host", "Peer-host multiplayer", NavBRFeatureState.Experimental, true),
        new("online-directory", "Public/private room directory", NavBRFeatureState.Planned, true),
        new("voice", "Advanced voice and communication", NavBRFeatureState.InDevelopment, true),
        new("physical-vehicles", "Physical remote vehicles in OMSI", NavBRFeatureState.Experimental, true, true),
        new("shared-traffic", "Shared AI traffic", NavBRFeatureState.Experimental, true, true),
        new("hardware-cockpit", "Arduino/ESP32 Hardware Cockpit", NavBRFeatureState.InDevelopment, true),
        new("driver-profile", "Driver profile and statistics", NavBRFeatureState.InDevelopment, true),
        new("virtual-companies", "Virtual companies", NavBRFeatureState.Planned, true),
        new("dispatcher", "CCO / Dispatcher", NavBRFeatureState.Planned, true),
        new("session-sync", "Session time/date/weather sync", NavBRFeatureState.Planned, true),
        new("fleet-identity", "Fleet, garage and vehicle identity", NavBRFeatureState.Planned, true),
        new("compatibility-manifest", "Map/mod/dependency compatibility manifest", NavBRFeatureState.InDevelopment, true),
        new("replay-ghost", "Replay and Ghost Bus", NavBRFeatureState.Experimental, true),
        new("live-web-map", "Optional live web map", NavBRFeatureState.Planned, true, true),
        new("community-events", "Events and special operations", NavBRFeatureState.Planned, true),
        new("permissions", "Roles, permissions and moderation", NavBRFeatureState.Planned, true),
        new("session-health", "Session health diagnostics", NavBRFeatureState.InDevelopment, true),
        new("sdk", "NavBR SDK / local API", NavBRFeatureState.Planned, true, true),
        new("workshop", "Community profiles and workshop", NavBRFeatureState.Planned, true),
        new("portal", "Alpha.12 web portal", NavBRFeatureState.InDevelopment, true),
        new("companion", "Mobile/companion API foundation", NavBRFeatureState.Planned, true)
    ];

    public static NavBRFeatureDescriptor? Find(string id) =>
        All.FirstOrDefault(feature => string.Equals(feature.Id, id, StringComparison.OrdinalIgnoreCase));
}
