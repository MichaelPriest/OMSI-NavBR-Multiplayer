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
        new("core-ui", "Core UI and localization", NavBRFeatureState.InDevelopment, true, false, "Alpha.12 shell, settings, first-run and five-language base are included; remaining hardcoded legacy text is being migrated."),
        new("telemetry", "OMSI telemetry and compatibility", NavBRFeatureState.Experimental, true),
        new("navigation", "GPS, HUD, TTData and navigation", NavBRFeatureState.InDevelopment, true),
        new("peer-host", "Peer-host multiplayer", NavBRFeatureState.Experimental, true, false, "TCP 27730 peer-host, diagnostics, network quality metrics, optional UPnP and opt-in application relay fallback are included."),
        new("online-directory", "Public/private room directory", NavBRFeatureState.InDevelopment, true, false, "Private rooms and the configured-server public browser are included; global presence/discovery remains in development."),
        new("voice", "Advanced voice and communication", NavBRFeatureState.InDevelopment, true, false, "General, company/team, dispatcher and proximity channels are included with mute, deafen, per-player gain and audio device selection."),
        new("physical-vehicles", "Physical remote vehicles in OMSI", NavBRFeatureState.Experimental, true, true),
        new("shared-traffic", "Shared AI traffic", NavBRFeatureState.Experimental, true, true),
        new("hardware-cockpit", "Arduino/ESP32 Hardware Cockpit", NavBRFeatureState.InDevelopment, true),
        new("driver-profile", "Driver profile and statistics", NavBRFeatureState.InDevelopment, true, false, "Local driver identity and core statistics are included; export/import and optional online sync remain in development."),
        new("virtual-companies", "Virtual companies", NavBRFeatureState.InDevelopment, true, false, "Local company profile and fleet management are included; roles, schedules and online integration remain in development."),
        new("dispatcher", "CCO / Dispatcher", NavBRFeatureState.InDevelopment, true, false, "Local and multiplayer monitoring, operational states, driver support/incident reports and authority acknowledgement/resolution are included; assignments and richer dispatch commands remain in development."),
        new("session-sync", "Session operational sync", NavBRFeatureState.InDevelopment, true, false, "Read-only authority-driven map, line, route, destination and next-stop synchronization is included as a preview. OMSI time/date/weather writeback and follow-host policies remain pending validated integration."),
        new("fleet-identity", "Fleet, garage and vehicle identity", NavBRFeatureState.InDevelopment, true, false, "Local fleet registration can identify the active OMSI bus; garage and richer shared identity remain in development."),
        new("compatibility-manifest", "Map/mod/dependency compatibility manifest", NavBRFeatureState.InDevelopment, true),
        new("replay-ghost", "Replay and Ghost Bus", NavBRFeatureState.Experimental, true, false, "Recorder/replay foundations exist; final user workflow and Ghost Bus presentation remain in development."),
        new("live-web-map", "Optional live web map", NavBRFeatureState.InDevelopment, true, true, "Included in Alpha.12 as opt-in and in development to preserve privacy and reduce network surface."),
        new("community-events", "Events and special operations", NavBRFeatureState.InDevelopment, true, false, "Included in the Alpha.12 interface/catalog as an in-development community/RP module."),
        new("permissions", "Roles, permissions and moderation", NavBRFeatureState.InDevelopment, true, false, "Admin, moderator, dispatcher, driver, visitor and spectator roles are part of Alpha.12 and remain in development."),
        new("session-health", "Session health diagnostics", NavBRFeatureState.InDevelopment, true, false, "Ping, jitter, estimated loss, telemetry frequency and bridge/plugin health foundations are included."),
        new("sdk", "NavBR SDK / local API", NavBRFeatureState.InDevelopment, true, true, "Included as an opt-in development module; public API surface and compatibility guarantees are not final."),
        new("workshop", "Community profiles and workshop", NavBRFeatureState.InDevelopment, true, false, "Included as an in-development module for bus, street, hardware, HUD and translation profiles."),
        new("portal", "Alpha.12 web portal", NavBRFeatureState.InDevelopment, true, false, "Alpha.12 Test 3 portal/release flow is published; feature/status matrix continues evolving."),
        new("companion", "Mobile/companion API foundation", NavBRFeatureState.InDevelopment, true, false, "Included as the Alpha.12 companion API foundation; mobile clients remain future work.")
    ];

    public static NavBRFeatureDescriptor? Find(string id) =>
        All.FirstOrDefault(feature => string.Equals(feature.Id, id, StringComparison.OrdinalIgnoreCase));
}
