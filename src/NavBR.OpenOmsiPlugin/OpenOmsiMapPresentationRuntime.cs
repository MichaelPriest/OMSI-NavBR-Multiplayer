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
    public static OpenOmsiMapPresentationState Build(
        OpenOmsiLuaSnapshot? snapshot,
        OpenOmsiNavigationRuntimeState navigation,
        OpenOmsiGuidanceState guidance,
        OpenOmsiHudConfiguration hud)
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

        var radius = navigation.SuggestedMapRadiusMeters;
        if (guidance.Available &&
            guidance.DistanceToManeuverMeters is > 0d and < 450d)
        {
            radius = Math.Min(
                radius,
                Math.Clamp(
                    220d + guidance.DistanceToManeuverMeters.Value * 1.35d,
                    280d,
                    900d));
        }

        if (snapshot.OnFoot)
        {
            radius = Math.Min(radius, 250d);
        }

        var orientationMode = hud.FollowVehicleEnabled
            ? "heading-up"
            : "north-up";
        var rotation = hud.FollowVehicleEnabled
            ? NormalizeDegrees(-(snapshot.HeadingDegrees ?? 0d))
            : 0d;

        var markers = snapshot.NearbyVehicles
            .Where(vehicle =>
                (hud.TrafficEnabled &&
                 string.Equals(vehicle.Kind, "ai", StringComparison.Ordinal)) ||
                (hud.MultiplayerEnabled &&
                 string.Equals(vehicle.Kind, "player", StringComparison.Ordinal)))
            .Select(vehicle => new OpenOmsiMapMarkerState(
                Id: vehicle.Id,
                Kind: vehicle.Kind,
                Label: vehicle.Name,
                X: vehicle.X,
                Y: vehicle.Y,
                Z: vehicle.Z,
                HeadingDegrees: vehicle.HeadingDegrees,
                SpeedKph: vehicle.SpeedKph))
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

    private static double NormalizeDegrees(double value)
    {
        var normalized = value % 360d;
        return normalized < 0d
            ? normalized + 360d
            : normalized;
    }
}
