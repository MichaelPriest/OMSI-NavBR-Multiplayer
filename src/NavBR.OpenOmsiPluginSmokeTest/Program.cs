using NavBR.OpenOmsiPlugin;
using NavBR.Shared.PluginBridge;

Require(OpenOmsiNavigationRuntime.SuggestedRadius(0, onFoot: false) == 450d, "Stopped bus zoom.");
Require(OpenOmsiNavigationRuntime.SuggestedRadius(38, onFoot: false) == 900d, "Urban zoom.");
Require(OpenOmsiNavigationRuntime.SuggestedRadius(72, onFoot: false) == 1750d, "High-speed zoom.");
Require(OpenOmsiNavigationRuntime.SuggestedRadius(72, onFoot: true) == 250d, "On-foot zoom.");

var now = DateTimeOffset.UtcNow;
var snapshot = new OpenOmsiLuaSnapshot(
    Sequence: 1,
    CapturedAtUtc: now,
    HasPosition: true,
    X: 100d,
    Y: 100d,
    Z: 0d,
    HeadingDegrees: 90d,
    MapName: "Grundorf",
    Line: "76",
    Tour: "1",
    TripIndex: 1,
    Terminus: "Bahnhof",
    NextStop: "Rathaus",
    View: "driver",
    OnFoot: false,
    Paused: false,
    DelaySeconds: 30,
    VehicleName: "Test Bus",
    ClockSeconds: 36000d,
    Day: 6,
    Year: 2026,
    Multiplayer: true,
    TrafficCount: 20,
    ReportedSpeedKph: 32d,
    NextStopArrival: 36120d,
    NextStopDeparture: 36140d,
    NearbyVehicles:
    [
        new OpenOmsiNearbyVehicleState("ai-1", "ai", "AI 1", 110, 100, 0, 90, 0),
        new OpenOmsiNearbyVehicleState("ai-2", "ai", "AI 2", 120, 100, 0, 90, 5),
        new OpenOmsiNearbyVehicleState("ai-3", "ai", "AI 3", 130, 100, 0, 90, 7),
        new OpenOmsiNearbyVehicleState("ai-4", "ai", "AI 4", 140, 100, 0, 90, 30),
        new OpenOmsiNearbyVehicleState("player-1", "player", "Driver", 150, 100, 0, 90, 12)
    ]);

var state = OpenOmsiNavigationRuntime.Build(snapshot);
Require(state.SuggestedMapRadiusMeters == 900d, "Runtime urban zoom.");
Require(state.CongestionLevel == "moderate", $"Expected moderate congestion, got {state.CongestionLevel}.");
Require(state.NearbyStoppedAiCount == 1, "Stopped AI count.");
Require(state.NearbySlowAiCount == 2, "Slow AI count.");
Require(state.NearbyMovingAiCount == 1, "Moving AI count.");
Require(state.AverageNearbyTrafficSpeedKph is > 10d and < 11d, "Average traffic speed.");

var bridge = PluginExports.BuildStatus();
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiNavigationRuntime) == true,
    "Navigation capability was not advertised.");
Require(bridge.OpenOmsiSuggestedMapRadiusMeters is > 0d, "Navigation zoom was not published.");
Require(!string.IsNullOrWhiteSpace(bridge.OpenOmsiCongestionLevel), "Congestion level was not published.");

var defaults = OpenOmsiHudState.Current;
Require(defaults.MiniMapEnabled && defaults.AutoZoomEnabled, "Default HUD state.");

var updatedHud = OpenOmsiHudState.Apply(new PluginBridgeMessage(
    PluginBridgeProtocol.SetOpenOmsiHudConfiguration,
    PluginBridgeProtocol.Version,
    OpenOmsiMiniMapEnabled: false,
    OpenOmsiTrafficLayerEnabled: false,
    OpenOmsiRouteGuidanceEnabled: true));

Require(!updatedHud.MiniMapEnabled, "Minimap toggle.");
Require(!updatedHud.TrafficEnabled, "Traffic layer toggle.");
Require(updatedHud.RouteGuidanceEnabled, "Route guidance toggle.");
Require(updatedHud.TeleMatrixEnabled, "Unspecified HUD settings must remain unchanged.");

var configuredBridge = PluginExports.BuildStatus();
Require(configuredBridge.OpenOmsiMiniMapEnabled == false, "HUD state was not published.");
Require(configuredBridge.OpenOmsiTrafficLayerEnabled == false, "Traffic toggle was not published.");
Require(
    configuredBridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiHudConfiguration) == true,
    "HUD configuration capability was not advertised.");

OpenOmsiHudState.Reset();

Console.WriteLine("openOMSI phase 1 navigation + HUD runtime smoke passed.");

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
