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
    MapPath: null,
    TripName: "TripA",
    StopCount: 2,
    Destination: "Bahnhof",
    VehicleManufacturer: "Test",
    VehicleModel: "Bus",
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

var guidance = OpenOmsiGuidanceRuntime.Build(
[
    new OpenOmsiRoutePoint(0, 0),
    new OpenOmsiRoutePoint(0, 100),
    new OpenOmsiRoutePoint(100, 100),
    new OpenOmsiRoutePoint(200, 100)
],
0,
20);
Require(guidance.Available, "Turn guidance unavailable.");
Require(guidance.Maneuver == "right", $"Expected right turn, got {guidance.Maneuver}.");
Require(guidance.DistanceToManeuverMeters is >= 79d and <= 81d, "Turn distance mismatch.");
Require(guidance.TurnAngleDegrees is >= 89d and <= 91d, "Turn angle mismatch.");

var smoothCurve = Enumerable.Range(0, 19)
    .Select(i =>
    {
        var a = i * 5d * Math.PI / 180d;
        return new OpenOmsiRoutePoint(
            50d * (1d - Math.Cos(a)),
            50d * Math.Sin(a));
    })
    .ToArray();
var smoothGuidance = OpenOmsiGuidanceRuntime.Build(
    smoothCurve,
    smoothCurve[0].X,
    smoothCurve[0].Y);
Require(smoothGuidance.Available, "Smooth-curve guidance unavailable.");
Require(
    smoothGuidance.Maneuver is "right" or "slight-right",
    $"Smooth right curve not detected: {smoothGuidance.Maneuver}.");
Require(
    smoothGuidance.TurnAngleDegrees is > 22d,
    "Smooth-curve turn angle stayed below guidance threshold.");

var gentleCurve = Enumerable.Range(0, 19)
    .Select(i =>
    {
        var a = i * 5d * Math.PI / 180d;
        return new OpenOmsiRoutePoint(
            220d * (1d - Math.Cos(a)),
            220d * Math.Sin(a));
    })
    .ToArray();
var gentleGuidance = OpenOmsiGuidanceRuntime.Build(
    gentleCurve,
    gentleCurve[0].X,
    gentleCurve[0].Y);
Require(gentleGuidance.Available, "Gentle-curve guidance unavailable.");
Require(
    gentleGuidance.Maneuver == "continue",
    $"Gentle road bend became a false maneuver: {gentleGuidance.Maneuver}.");

var miniMap = OpenOmsiMiniMapRuntime.Build(
[
    new OpenOmsiRoutePoint(0, 0, 0),
    new OpenOmsiRoutePoint(0, 100, 0),
    new OpenOmsiRoutePoint(100, 100, 0)
],
0,
20);
Require(miniMap.Available, "Minimap runtime unavailable.");
Require(miniMap.RouteLengthMeters is >= 199d and <= 201d, "Minimap route length mismatch.");
Require(miniMap.ProgressMeters is >= 19d and <= 21d, "Minimap progress mismatch.");
Require(miniMap.RemainingMeters is >= 179d and <= 181d, "Minimap remaining distance mismatch.");
Require(miniMap.GuidanceWaypoints.Length > 0, "Guidance waypoints missing.");
Require(miniMap.GuidanceWaypoints.All(p => p.DistanceAheadMeters > 0d), "Invalid guidance waypoint distance.");

var visualRouteKey = OpenOmsiRouteRuntime.BuildRouteKey(snapshot);
Require(!string.IsNullOrWhiteSpace(visualRouteKey), "Visual route key.");

var visualRoute = OpenOmsiMapVisualRuntime.Build(
[
    new OpenOmsiRoutePoint(0, 0),
    new OpenOmsiRoutePoint(0, 50),
    new OpenOmsiRoutePoint(0, 100),
    new OpenOmsiRoutePoint(50, 100)
],
snapshot with { X = 0d, Y = 55d },
new OpenOmsiRouteRuntimeState(
    true,
    visualRouteKey,
    4,
    5d,
    false,
    1,
    1,
    0d,
    55d));
Require(visualRoute.Available, "Map visual state unavailable.");
Require(visualRoute.TraveledRoute.Length == 2, "Traveled route segmentation mismatch.");
Require(visualRoute.ForwardRoute.Length == 3, "Forward route segmentation mismatch.");
Require(visualRoute.RejoinRoute.Length == 0, "Unexpected rejoin segment.");

var longVisualPoints = Enumerable.Range(0, 2000)
    .Select(i => new OpenOmsiRoutePoint(0, i * 3d))
    .ToArray();
var longVisual = OpenOmsiMapVisualRuntime.Build(
    longVisualPoints,
    snapshot with { X = 0d, Y = 3000d },
    new OpenOmsiRouteRuntimeState(
        true,
        visualRouteKey,
        longVisualPoints.Length,
        0d,
        false,
        1000,
        1000,
        0d,
        3000d));
Require(longVisual.Available, "Long map visual unavailable.");
Require(longVisual.TraveledRoute.Length <= 72, "Traveled visual window exceeded point cap.");
Require(longVisual.ForwardRoute.Length <= 180, "Forward visual window exceeded point cap.");
Require(longVisual.TraveledRoute[^1].Y == 3000d, "Traveled visual window lost current point.");
Require(longVisual.ForwardRoute[0].Y == 3000d, "Forward visual window lost current point.");
Require(longVisual.ForwardRoute[^1].Y <= 5400d, "Forward visual window exceeded distance cap.");

var presentation = OpenOmsiMapPresentationRuntime.Build(
    snapshot,
    state,
    guidance,
    OpenOmsiHudState.Current,
    []);
Require(presentation.Available, "Map presentation unavailable.");
Require(presentation.OrientationMode == "heading-up", "Map orientation mode mismatch.");
Require(presentation.RadiusMeters < state.SuggestedMapRadiusMeters, "Turn-aware autozoom did not tighten.");

var manualZoomPresentation = OpenOmsiMapPresentationRuntime.Build(
    snapshot,
    state,
    guidance,
    OpenOmsiHudState.Current with { AutoZoomEnabled = false },
    []);
Require(
    Math.Abs(manualZoomPresentation.RadiusMeters - 900d) < 0.1d,
    "Manual map radius mismatch when autozoom is disabled.");
Require(presentation.Markers.Length == snapshot.NearbyVehicles.Length, "Map marker projection mismatch.");

var trafficHidden = OpenOmsiMapPresentationRuntime.Build(
    snapshot,
    state,
    guidance,
    OpenOmsiHudState.Current with { TrafficEnabled = false },
    []);
Require(
    trafficHidden.Markers.All(marker => marker.Kind != "ai"),
    "Traffic markers were not hidden.");

var bridge = PluginExports.BuildStatus();
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiNavigationRuntime) == true,
    "Navigation capability was not advertised.");
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiTurnGuidance) == true,
    "Turn guidance capability was not advertised.");
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiMiniMapRuntime) == true,
    "Minimap runtime capability was not advertised.");
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiGuidanceWaypoints) == true,
    "Guidance waypoint capability was not advertised.");
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiMapVisualState) == true,
    "Map visual-state capability was not advertised.");
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiMapPresentation) == true,
    "Map presentation capability was not advertised.");
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiTeleMatrixRuntime) == true,
    "TeleMatrix runtime capability was not advertised.");
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiCompactHud) == true,
    "Compact HUD capability was not advertised.");
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiGroundArrowPayload) == true,
    "Ground-arrow payload capability was not advertised.");
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiOverlayFrame) == true,
    "Overlay-frame capability was not advertised.");
Require(bridge.OpenOmsiMapMarkers is null, "Duplicate map markers leaked into normal bridge status.");
Require(bridge.OpenOmsiRouteSteps is null, "Detailed route steps leaked into normal bridge status.");
Require(bridge.OpenOmsiOverlayFrame is null, "Heavy overlay frame leaked into normal bridge status.");
Require(bridge.OpenOmsiOverlay2DFrame is null, "Heavy 2D overlay leaked into normal bridge status.");
Require(bridge.OpenOmsiWorldGuidanceFrame is null, "Heavy world overlay leaked into normal bridge status.");
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiOverlay2DFrame) == true,
    "2D overlay capability was not advertised.");
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiWorldGuidanceFrame) == true,
    "World guidance capability was not advertised.");
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiGroundArrowPrimitives) == true,
    "Ground-arrow primitive capability was not advertised.");
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiOverlayExportV1) == true,
    "Overlay export v1 capability was not advertised.");
Require(
    bridge.Capabilities?.Contains(PluginBridgeProtocol.CapabilityOpenOmsiOverlayExportV2) == true,
    "Overlay export v2 capability was not advertised.");

PluginExports.SetPerformanceProfile("diagnostics");
var diagnosticBridge = PluginExports.BuildStatus();
Require(diagnosticBridge.OpenOmsiOverlayFrame is not null, "Diagnostics overlay frame missing.");
Require(diagnosticBridge.OpenOmsiOverlay2DFrame is not null, "Diagnostics 2D overlay missing.");
Require(diagnosticBridge.OpenOmsiWorldGuidanceFrame is not null, "Diagnostics world overlay missing.");
Require(diagnosticBridge.OpenOmsiRouteSteps is not null, "Diagnostics route steps missing.");
Require(diagnosticBridge.OpenOmsiMapMarkers is not null, "Diagnostics map markers missing.");
PluginExports.SetPerformanceProfile("auto");

var bridgeJson = System.Text.Json.JsonSerializer.Serialize(
    bridge,
    OpenOmsiPluginJsonContext.Default.PluginBridgeMessage);
Require(
    bridgeJson.Length <= PluginBridgeProtocol.MaxMessageChars,
    $"Bridge status exceeded max message size: {bridgeJson.Length} chars.");

var exportTeleMatrix = OpenOmsiTeleMatrixRuntime.Build(snapshot, null);
var exportRouteState = new OpenOmsiRouteRuntimeState(
    RouteLoaded: false,
    RouteKey: null,
    RoutePointCount: 0,
    DistanceFromRouteMeters: null,
    OffRoute: false,
    NearestRoutePointIndex: null,
    RejoinRoutePointIndex: null,
    RejoinTargetX: null,
    RejoinTargetY: null);
var exportCompactHud = OpenOmsiCompactHudRuntime.Build(
    guidance,
    miniMap,
    exportTeleMatrix,
    exportRouteState);
var exportGroundArrows = OpenOmsiGroundArrowRuntime.Build(
    miniMap.GuidanceWaypoints,
    guidance,
    exportRouteState)
    .Select(arrow => new NavBR.Shared.PluginBridge.OpenOmsiGroundArrowState(
        arrow.X,
        arrow.Y,
        arrow.Z,
        arrow.HeadingDegrees,
        arrow.DistanceAheadMeters,
        arrow.Kind))
    .ToArray();
var exportFrames = OpenOmsiOverlayFrameRuntime.Build(
    OpenOmsiHudState.Current,
    presentation,
    visualRoute,
    miniMap,
    exportCompactHud,
    exportTeleMatrix,
    exportGroundArrows);
OpenOmsiOverlayExport.Publish(
    exportFrames.Full,
    exportFrames.Overlay2D,
    exportFrames.World);

var overlayRequired = OpenOmsiOverlayExport.RequiredBytes;
Require(overlayRequired > 1, "Overlay export did not cache a payload.");
var overlayBuffer = new byte[overlayRequired];
var overlayWritten = OpenOmsiOverlayExport.CopyLatest(overlayBuffer);
Require(overlayWritten > 0, "Overlay export copy failed.");
Require(overlayBuffer[overlayWritten] == 0, "Overlay export is not null-terminated.");
var overlayJson = System.Text.Encoding.UTF8.GetString(overlayBuffer, 0, overlayWritten);
string? exportedOrientation = null;
using (var overlayDoc = System.Text.Json.JsonDocument.Parse(overlayJson))
{
    Require(
        overlayDoc.RootElement.TryGetProperty("OrientationMode", out var orientation),
        "Overlay export JSON missing orientation.");
    exportedOrientation = orientation.GetString();
    Require(
        exportedOrientation is "heading-up" or "north-up",
        $"Overlay export JSON invalid orientation: {exportedOrientation ?? "<null>"}.");
}
var overlayRequiredV2 = OpenOmsiOverlayExport.RequiredBytesV2;
Require(overlayRequiredV2 > 1, "Overlay export v2 did not cache a payload.");
var overlayBufferV2 = new byte[overlayRequiredV2];
var overlayWrittenV2 = OpenOmsiOverlayExport.CopyLatestV2(overlayBufferV2);
Require(overlayWrittenV2 > 0, "Overlay export v2 copy failed.");
var overlayJsonV2 = System.Text.Encoding.UTF8.GetString(overlayBufferV2, 0, overlayWrittenV2);
using (var overlayDocV2 = System.Text.Json.JsonDocument.Parse(overlayJsonV2))
{
    Require(
        overlayDocV2.RootElement.GetProperty("Version").GetInt32() == 2,
        "Overlay export v2 version mismatch.");
    Require(
        overlayDocV2.RootElement.TryGetProperty("Overlay2D", out _),
        "Overlay export v2 missing 2D frame.");
    Require(
        overlayDocV2.RootElement.TryGetProperty("WorldGuidance", out _),
        "Overlay export v2 missing world frame.");
    var overlay2D = overlayDocV2.RootElement.GetProperty("Overlay2D");
    Require(
        overlay2D.GetProperty("OrientationMode").GetString() == exportedOrientation,
        "Overlay export v1/v2 orientation mismatch.");
    Require(overlay2D.GetProperty("TeleMatrixLine").GetString() == "76", "Overlay export v2 TeleMatrix line mismatch.");
    Require(overlay2D.GetProperty("TeleMatrixNextStop").GetString() == "Rathaus", "Overlay export v2 next stop mismatch.");
    Require(overlay2D.GetProperty("RouteGuidanceVisible").GetBoolean(), "Overlay export v2 route-guidance flag mismatch.");
    Require(overlay2D.GetProperty("TrafficVisible").GetBoolean(), "Overlay export v2 traffic flag mismatch.");
    Require(overlay2D.GetProperty("PlayersVisible").GetBoolean(), "Overlay export v2 player flag mismatch.");
    Require(
        overlay2D.GetProperty("RouteProgressPercent").GetDouble() is >= 9d and <= 11d,
        "Overlay export v2 route progress mismatch.");
}

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
Require(offRoute.RejoinRoutePointIndex == 2, "Distance-based rejoin point mismatch.");
Require(
    offRoute.RejoinTargetX is >= 159.9d and <= 160.1d &&
    offRoute.RejoinTargetY is >= 99.9d and <= 100.1d,
    "Distance-based rejoin target mismatch.");

OpenOmsiRouteRuntime.SetRoute(
[
    new OpenOmsiRoutePoint(0, -100),
    new OpenOmsiRoutePoint(0, 0),
    new OpenOmsiRoutePoint(0, 100),
    new OpenOmsiRoutePoint(100, 100),
    new OpenOmsiRoutePoint(100, 0),
    new OpenOmsiRoutePoint(0, 0),
    new OpenOmsiRoutePoint(-100, 0)
],
routeKey);
var crossingApproach = OpenOmsiRouteRuntime.Build(
    snapshot with { X = 0d, Y = -20d, HeadingDegrees = 0d });
Require(
    crossingApproach.NearestRoutePointIndex is 0 or 1,
    "Self-crossing approach selected the wrong route branch.");
var crossingAtJunction = OpenOmsiRouteRuntime.Build(
    snapshot with { X = 0d, Y = 0d, HeadingDegrees = 0d });
Require(
    crossingAtJunction.NearestRoutePointIndex is <= 2,
    "Self-crossing projection jumped to a later crossing branch.");

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

var parsedV4 = OpenOmsiLuaSnapshotReader.TryParsePayloadForSmoke(
    "4|9|" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() +
    "|1|100|200|3|90|Grundorf|76|1|1|Bahnhof|Rathaus|driver|0|0|30|Test%20Bus|36000|6|2026|1|20|32|36120|36140|4|2|maps%2FGrundorf%2Fglobal.cfg|TripA|2|Bahnhof|Test|Bus|ai-1,ai,AI%201,110,200,3,90,5");
Require(parsedV4 is not null, "Snapshot v4 did not parse.");
Require(parsedV4!.MapPath == "maps/Grundorf/global.cfg", "Snapshot v4 map path.");
Require(parsedV4.TripName == "TripA", "Snapshot v4 trip name.");
Require(parsedV4.StopCount == 2, "Snapshot v4 stop count.");
Require(parsedV4.VehicleManufacturer == "Test", "Snapshot v4 manufacturer.");
Require(parsedV4.NearbyVehicles.Length == 1, "Snapshot v4 nearby vehicles.");


var previousContentRoot = Environment.GetEnvironmentVariable("NAVBR_OPENOMSI_CONTENT_ROOT");
var smokeRoot = Path.Combine(Path.GetTempPath(), "NavBR-openOMSI-content-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(smokeRoot, "maps", "Grundorf", "TTData"));
Environment.SetEnvironmentVariable("NAVBR_OPENOMSI_CONTENT_ROOT", smokeRoot);
try
{
    var ttData = Path.Combine(smokeRoot, "maps", "Grundorf", "TTData");
    await File.WriteAllTextAsync(
        Path.Combine(ttData, "76.ttl"),
        "[newtour]\n1\nDepot\n0\n[addtrip]\nTripA\n0\n600\n[addtrip]\nTripB\n1\n630\n");
    await File.WriteAllTextAsync(
        Path.Combine(ttData, "TripA.ttp"),
        "[trip]\n\nBahnhof\n76\n[station_typ2]\n1001\n[station_typ2]\n1002\n");
    await File.WriteAllTextAsync(
        Path.Combine(ttData, "Busstops.cfg"),
        "[busstop]\nRathaus\n0\n1001\n0\n0\n0\n[busstop]\nBahnhof\n0\n1002\n0\n0\n0\n");

    await File.WriteAllTextAsync(
        Path.Combine(ttData, "StnLinks.cfg"),
        "[StnLink]\n120\n1001\n1002\n0\n0\n0\n0\n0\n0\n" +
        "[StnLink_entry]\n5001\n2\n0\n60\n-1\n0\n0\n" +
        "[StnLink_entry]\n5002\n0\n0\n60\n-1\n0\n0\n");

    var mapDir = Path.Combine(smokeRoot, "maps", "Grundorf");
    await File.WriteAllTextAsync(
        Path.Combine(mapDir, "global.cfg"),
        "[name]\nGrundorf\n[map]\n0\n0\ntile_0_0.map\n");

    await File.WriteAllTextAsync(
        Path.Combine(mapDir, "tile_0_0.map"),
        "[version]\n14\n" +
        "[object]\n0\nSceneryobjects\\Test\\road.sco\n5001\n100\n100\n0\n0\n0\n0\n0\n" +
        "[object]\n0\nSceneryobjects\\Test\\road.sco\n5002\n100\n160\n0\n0\n0\n0\n0\n" +
        "[object]\n0\nSceneryobjects\\Test\\stop.sco\n1001\n98\n95\n0\n0\n0\n0\n0\n" +
        "[object]\n0\nSceneryobjects\\Test\\stop.sco\n1002\n102\n225\n0\n180\n0\n0\n0\n");

    var sceneryDir = Path.Combine(smokeRoot, "Sceneryobjects", "Test");
    Directory.CreateDirectory(sceneryDir);
    await File.WriteAllTextAsync(
        Path.Combine(sceneryDir, "road.sco"),
        "[path]\n0\n0\n0\n0\n0\n20\n0\n0\n0\n3\n0\n0\n" +
        "[path]\n0\n0\n0\n0\n0\n20\n0\n0\n0\n3\n0\n0\n" +
        "[path]\n0\n0\n0\n0\n0\n60\n0\n0\n0\n3\n0\n0\n");

    var splineDir = Path.Combine(smokeRoot, "Splines", "Test");
    Directory.CreateDirectory(splineDir);
    await File.WriteAllTextAsync(
        Path.Combine(splineDir, "grade.sli"),
        "[halfcantwidth]\n1\n[path]\n0\n2\n0\n3\n0\n");

    await File.AppendAllTextAsync(
        Path.Combine(mapDir, "tile_0_0.map"),
        "[spline_h]\n0\nSplines\\Test\\grade.sli\n6001\n0\n0\n50\n5\n50\n0\n100\n0\n0\n0\n10\n10\n10\n");

    var content = OpenOmsiContentLocator.Resolve("Grundorf");
    Require(content.RootAvailable, "Configured content root was not found.");
    Require(content.MapAvailable, "Map directory was not found.");
    Require(content.TimetableAvailable, "TTData directory was not found.");

    var pathStyleContent = OpenOmsiContentLocator.Resolve("maps/Grundorf/global.cfg");
    Require(pathStyleContent.MapAvailable, "Path-style map name did not resolve.");

    var nativePathContent = OpenOmsiContentLocator.Resolve(
        "Nome Diferente",
        Path.Combine(mapDir, "global.cfg"));
    Require(nativePathContent.MapAvailable, "Native map_path did not resolve.");
    Require(
        string.Equals(
            nativePathContent.MapDirectory,
            mapDir,
            StringComparison.OrdinalIgnoreCase),
        "Native map_path resolved the wrong directory.");

    var resolvedTrip = OpenOmsiTimetableResolver.Resolve(content, snapshot);
    Require(resolvedTrip is not null, "Live TTData trip did not resolve.");
    Require(resolvedTrip!.TripName == "TripA", "Resolved trip name mismatch.");
    Require(resolvedTrip.Terminus == "Bahnhof", "Resolved trip terminus mismatch.");
    Require(resolvedTrip.Stops.SequenceEqual(["Rathaus", "Bahnhof"]), "Resolved stop sequence mismatch.");

    var stopMarkers = OpenOmsiStopMarkerResolver.Resolve(content, resolvedTrip);

    var resolvedSteps = OpenOmsiRouteStepResolver.Resolve(content, resolvedTrip);
    Require(resolvedSteps.Length == 2, "StnLinks route step count mismatch.");
    Require(resolvedSteps[0].ObjectId == 5001, "First route step object mismatch.");
    Require(resolvedSteps[0].PathIndex == 2, "First route step path mismatch.");
    Require(resolvedSteps[0].TileIndex == 0, "First route step tile mismatch.");
    Require(resolvedSteps[0].Leg == 0 && !resolvedSteps[0].IsTrack, "Station-link route step metadata mismatch.");

    var geometry = OpenOmsiRouteGeometryResolver.Resolve(content, resolvedSteps);
    Require(geometry.Length > 20, "Automatic route geometry point count.");
    Require(Math.Abs(geometry[0].X - 100d) < 0.2d, "Automatic route geometry start X.");
    Require(Math.Abs(geometry[0].Y - 100d) < 0.2d, "Automatic route geometry start Y.");
    Require(Math.Abs(geometry[^1].Y - 180d) < 0.5d, "Automatic route geometry end Y.");

    stopMarkers = OpenOmsiStopMarkerResolver.Resolve(content, resolvedTrip);
    Require(stopMarkers.Length == 2, "Timetable stop marker count mismatch.");
    Require(stopMarkers[0].Label == "Rathaus", "First stop marker label mismatch.");
    Require(Math.Abs(stopMarkers[0].X - 98d) < 0.2d, "First stop marker X mismatch.");
    Require(Math.Abs(stopMarkers[1].Y - 225d) < 0.2d, "Second stop marker Y mismatch.");

    var splineGeometry = OpenOmsiRouteGeometryResolver.Resolve(
        content,
        [new OpenOmsiRouteStep(0, 0, 6001, 0, 100d, false)]);
    Require(splineGeometry.Length > 20, "Spline geometry point count.");
    Require(Math.Abs((splineGeometry[0].Z ?? 0d) - 4.9d) < 0.15d, "Spline cant start height.");
    Require(Math.Abs((splineGeometry[^1].Z ?? 0d) - 14.9d) < 0.2d, "Spline_h end height.");

    OpenOmsiRouteRuntime.Clear();
    OpenOmsiTimetableRuntime.Reset();
    var cachedRuntime = OpenOmsiTimetableRuntime.Resolve(content, snapshot);
    Require(cachedRuntime.RouteSteps.Length == 2, "Cached timetable route steps mismatch.");
    Require(cachedRuntime.RoutePoints.Length == geometry.Length, "Cached automatic geometry mismatch.");

    var automaticRoute = OpenOmsiRouteRuntime.Build(snapshot);
    Require(automaticRoute.RouteLoaded, "Automatic route was not loaded into rejoin runtime.");
    Require(automaticRoute.RoutePointCount == geometry.Length, "Automatic route point count mismatch.");

    var duplicateMapDir = Path.Combine(smokeRoot, "maps", "DuplicateMap");
    Directory.CreateDirectory(duplicateMapDir);
    await File.WriteAllTextAsync(
        Path.Combine(duplicateMapDir, "global.cfg"),
        "[map]\n0\n0\ntile_0_0.map\n" +
        "[map]\n0\n0\ntile_0_0.map\n" +
        "[map]\n1\n0\ntile_1_0.map\n");
    var duplicateContent = OpenOmsiContentLocator.Resolve("DuplicateMap");
    var duplicateLayout = OpenOmsiMapLayoutReader.Read(duplicateContent);
    Require(duplicateLayout is not null, "Duplicate-map layout did not resolve.");
    Require(duplicateLayout!.Tiles.Length == 2, "Duplicate global tile was not deduplicated.");
    Require(duplicateLayout.Tiles[1].Index == 2, "Raw global tile index was not preserved.");
}
finally
{
    Environment.SetEnvironmentVariable("NAVBR_OPENOMSI_CONTENT_ROOT", previousContentRoot);
    try { Directory.Delete(smokeRoot, recursive: true); } catch { }
}

OpenOmsiRouteRuntime.Clear();
OpenOmsiTimetableRuntime.Reset();
OpenOmsiHudState.Reset();

Console.WriteLine("openOMSI phase 1 navigation + HUD + precise automatic route geometry runtime smoke passed.");

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
