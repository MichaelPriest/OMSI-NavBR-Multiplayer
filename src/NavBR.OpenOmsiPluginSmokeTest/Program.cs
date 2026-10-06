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
    TripsCount: 4,
    NextStopNumber: 2,
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

var routeKey = OpenOmsiRouteRuntime.BuildRouteKey(snapshot);
Require(!string.IsNullOrWhiteSpace(routeKey), "Live duty route key.");

OpenOmsiRouteRuntime.SetRoute(
[
    new OpenOmsiRoutePoint(100, 100),
    new OpenOmsiRoutePoint(150, 100),
    new OpenOmsiRoutePoint(200, 100),
    new OpenOmsiRoutePoint(250, 100),
    new OpenOmsiRoutePoint(300, 100)
],
routeKey);

var onRoute = OpenOmsiRouteRuntime.Build(snapshot);
Require(onRoute.RouteLoaded, "Route must be loaded.");
Require(onRoute.RouteKey == routeKey, "Route key mismatch.");
Require(!onRoute.OffRoute, "Vehicle on route was marked off-route.");
Require(onRoute.NearestRoutePointIndex == 0, "Nearest route point mismatch.");

var offRouteSnapshot = snapshot with { X = 100d, Y = 180d };
var offRoute = OpenOmsiRouteRuntime.Build(offRouteSnapshot);
Require(offRoute.OffRoute, "Off-route vehicle was not detected.");
Require(offRoute.DistanceFromRouteMeters is >= 79d and <= 81d, "Off-route distance mismatch.");
Require(offRoute.RejoinRoutePointIndex == 3, "Rejoin look-ahead point mismatch.");
Require(offRoute.RejoinTargetX == 250d && offRoute.RejoinTargetY == 100d, "Rejoin target mismatch.");

var routeBridge = PluginExports.BuildStatus();
Require(
    routeBridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiRouteRejoin) == true,
    "Route rejoin capability was not advertised.");

var changedDuty = snapshot with { TripIndex = 2 };
var staleRoute = OpenOmsiRouteRuntime.Build(changedDuty);
Require(!staleRoute.RouteLoaded, "Route geometry survived a duty/trip change.");

var parsedV3 = OpenOmsiLuaSnapshotReader.TryParsePayloadForSmoke(
    "3|8|" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() +
    "|1|100|200|3|90|Grundorf|76|1|1|Bahnhof|Rathaus|driver|0|0|30|Test%20Bus|36000|6|2026|1|20|32|36120|36140|4|2|ai-1,ai,AI%201,110,200,3,90,5");
Require(parsedV3 is not null, "Snapshot v3 did not parse.");
Require(parsedV3!.TripsCount == 4, "Snapshot v3 trips count.");
Require(parsedV3.NextStopNumber == 2, "Snapshot v3 next stop number.");
Require(parsedV3.NearbyVehicles.Length == 1, "Snapshot v3 nearby vehicles.");

OpenOmsiRouteRuntime.Clear();
OpenOmsiHudState.Reset();

Console.WriteLine("openOMSI phase 1 navigation + HUD + rejoin runtime smoke passed.");

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
