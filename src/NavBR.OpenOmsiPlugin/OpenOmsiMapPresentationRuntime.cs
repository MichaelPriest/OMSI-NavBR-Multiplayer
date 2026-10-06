using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiMapPresentationState(
    bool Available,
    double? CenterX,
    double? CenterY,
    double RotationDegrees,
    double RadiusMeters,
    string OrientationMode,
    OpenOmsiMapMarkerState[] Markers);

internal static class OpenOmsiMapPresentationRuntime
{
    private const double ManualMapRadiusMeters = 900d;
    private const int MaximumOverlayMarkers = 160;
    private const int MaximumPlayerMarkers = 48;
    private const int MaximumStopMarkers = 64;
    public static OpenOmsiMapPresentationState Build(
        OpenOmsiLuaSnapshot? snapshot,
        OpenOmsiNavigationRuntimeState navigation,
        OpenOmsiGuidanceState guidance,
        OpenOmsiHudConfiguration hud,
        OpenOmsiMapMarkerState[] stopMarkers)
    {
        if (snapshot?.HasPosition != true ||
            snapshot.X is null ||
            snapshot.Y is null)
        {
            return new(
                false,
                null,
                null,
                0d,
                navigation.SuggestedMapRadiusMeters,
                "north-up",
                []);
        }

        var radius = hud.AutoZoomEnabled
            ? navigation.SuggestedMapRadiusMeters
            : ManualMapRadiusMeters;
        if (hud.AutoZoomEnabled &&
            guidance.Available &&
            guidance.DistanceToManeuverMeters is > 0d and < 450d)
        {
            radius = Math.Min(
                radius,
                Math.Clamp(
                    220d + guidance.DistanceToManeuverMeters.Value * 1.35d,
                    280d,
                    900d));
        }

        if (hud.AutoZoomEnabled && snapshot.OnFoot)
        {
            radius = Math.Min(radius, 250d);
        }

        var orientationMode = hud.FollowVehicleEnabled
            ? "heading-up"
            : "north-up";
        var rotation = hud.FollowVehicleEnabled
            ? NormalizeDegrees(-(snapshot.HeadingDegrees ?? 0d))
            : 0d;

        var centerX = snapshot.X.Value;
        var centerY = snapshot.Y.Value;

        var players = hud.MultiplayerEnabled
            ? snapshot.NearbyVehicles
                .Where(vehicle =>
                    string.Equals(vehicle.Kind, "player", StringComparison.Ordinal))
                .OrderBy(vehicle => DistanceSquared(centerX, centerY, vehicle.X, vehicle.Y))
                .Take(MaximumPlayerMarkers)
                .Select(ToMarker)
                .ToArray()
            : [];

        var stops = stopMarkers
            .OrderBy(marker => DistanceSquared(centerX, centerY, marker.X, marker.Y))
            .Take(MaximumStopMarkers)
            .ToArray();

        var remaining = Math.Max(
            0,
            MaximumOverlayMarkers - players.Length - stops.Length);
        var ai = hud.TrafficEnabled && remaining > 0
            ? snapshot.NearbyVehicles
                .Where(vehicle =>
                    string.Equals(vehicle.Kind, "ai", StringComparison.Ordinal))
                .OrderBy(vehicle => DistanceSquared(centerX, centerY, vehicle.X, vehicle.Y))
                .Take(remaining)
                .Select(ToMarker)
                .ToArray()
            : [];

        var markers = players
            .Concat(stops)
            .Concat(ai)
            .Take(MaximumOverlayMarkers)
            .ToArray();

        return new(
            Available: true,
            CenterX: snapshot.X,
            CenterY: snapshot.Y,
            RotationDegrees: Math.Round(rotation, 1),
            RadiusMeters: Math.Round(radius, 1),
            OrientationMode: orientationMode,
            Markers: markers);
    }

    private static OpenOmsiMapMarkerState ToMarker(
        OpenOmsiNearbyVehicleState vehicle) =>
        new(
            Id: vehicle.Id,
            Kind: vehicle.Kind,
            Label: vehicle.Name,
            X: vehicle.X,
            Y: vehicle.Y,
            Z: vehicle.Z,
            HeadingDegrees: vehicle.HeadingDegrees,
            SpeedKph: vehicle.SpeedKph);

    private static double DistanceSquared(
        double centerX,
        double centerY,
        double x,
        double y)
    {
        var dx = x - centerX;
        var dy = y - centerY;
        return dx * dx + dy * dy;
    }

    private static double NormalizeDegrees(double value)
    {
        var normalized = value % 360d;
        return normalized < 0d
            ? normalized + 360d
            : normalized;
    }
}
