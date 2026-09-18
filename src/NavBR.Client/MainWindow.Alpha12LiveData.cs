using NavBR.Client.Maps;
using NavBR.Client.Multiplayer;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client;

public partial class MainWindow
{
    private readonly object _alpha12NavigationSync = new();
    private string? _alpha12NavigationRouteKey;
    private IReadOnlyList<OmsiRouteTracePoint> _alpha12NavigationTrace = Array.Empty<OmsiRouteTracePoint>();
    private string? _alpha12NavigationStopsKey;
    private IReadOnlyList<OmsiBusStopPoint> _alpha12NavigationStops = Array.Empty<OmsiBusStopPoint>();

    internal NavBRNavigationSnapshot GetNavigationSnapshotForAlpha12()
    {
        var telemetry = _lastTelemetry;
        if (telemetry is null || string.IsNullOrWhiteSpace(telemetry.MapName))
        {
            return NavBRNavigationSnapshot.Unavailable(telemetry);
        }

        var map = FindActiveMap(telemetry.MapName);
        if (map is null)
        {
            return NavBRNavigationSnapshot.Unavailable(telemetry);
        }

        var layout = _loadedRoadmapLayout ?? OmsiMapLayoutReader.TryRead(map.GlobalConfigPath);
        if (layout is null)
        {
            return NavBRNavigationSnapshot.Unavailable(telemetry);
        }

        var routeKey = string.Join('|',
            map.DirectoryPath,
            telemetry.Line ?? string.Empty,
            telemetry.Route ?? string.Empty,
            telemetry.DestinationName ?? string.Empty);

        IReadOnlyList<OmsiRouteTracePoint> trace;
        IReadOnlyList<OmsiBusStopPoint> stops;
        lock (_alpha12NavigationSync)
        {
            if (!string.Equals(_alpha12NavigationRouteKey, routeKey, StringComparison.OrdinalIgnoreCase))
            {
                var lookupTarget = !string.IsNullOrWhiteSpace(telemetry.Route)
                    ? telemetry.Route
                    : telemetry.DestinationName;
                _alpha12NavigationTrace = OmsiRouteTraceReader.TryRead(
                    map,
                    layout,
                    lookupTarget,
                    telemetry.Line);
                _alpha12NavigationRouteKey = routeKey;
            }

            if (!string.Equals(_alpha12NavigationStopsKey, map.DirectoryPath, StringComparison.OrdinalIgnoreCase))
            {
                _alpha12NavigationStops = OmsiBusStopReader.TryRead(map);
                _alpha12NavigationStopsKey = map.DirectoryPath;
            }

            trace = _alpha12NavigationTrace;
            stops = _alpha12NavigationStops;
        }

        return NavBRNavigationEngine.Evaluate(telemetry, layout, trace, stops);
    }

    internal (string? Line, string? DestinationName) GetNavigationIdentityForAlpha12()
    {
        var telemetry = _lastTelemetry;
        return (telemetry?.Line, telemetry?.DestinationName);
    }

    internal OmsiCompatibilityManifest GetCompatibilityManifestForAlpha12()
    {
        var telemetry = _lastTelemetry;
        var map = !string.IsNullOrWhiteSpace(telemetry?.MapName)
            ? FindActiveMap(telemetry.MapName)
            : null;
        return OmsiCompatibilityManifestFactory.Create(
            telemetry,
            map,
            _currentOmsi?.FileVersion);
    }
}
