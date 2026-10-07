using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiGroundArrowPrimitive(
    double X,
    double Y,
    double Z,
    double HeadingDegrees,
    double WidthMeters,
    double LengthMeters,
    double HeightOffsetMeters,
    double Opacity,
    double DistanceAheadMeters,
    string Kind);

internal static class OpenOmsiGroundArrowPrimitiveRuntime
{
    private const double DefaultWidthMeters = 1.8d;
    private const double DefaultLengthMeters = 4.6d;
    private const double HeightOffsetMeters = 0.06d;
    private const double FadeStartMeters = 150d;
    private const double CullDistanceMeters = 220d;

    public static OpenOmsiGroundArrowPrimitive[] Build(
        NavBR.Shared.PluginBridge.OpenOmsiGroundArrowState[] arrows)
    {
        return arrows
            .Where(arrow =>
                arrow.DistanceAheadMeters >= 0d &&
                arrow.DistanceAheadMeters <= CullDistanceMeters)
            .Select(arrow =>
            {
                var emphasis = arrow.Kind switch
                {
                    "left" or "right" or "slight-left" or "slight-right" or "uturn" => 1.18d,
                    "rejoin" => 1.12d,
                    _ => 1d
                };

                var opacity = arrow.DistanceAheadMeters <= FadeStartMeters
                    ? 1d
                    : Math.Clamp(
                        1d - (arrow.DistanceAheadMeters - FadeStartMeters) /
                        (CullDistanceMeters - FadeStartMeters),
                        0d,
                        1d);

                return new OpenOmsiGroundArrowPrimitive(
                    X: arrow.X,
                    Y: arrow.Y,
                    Z: (arrow.Z ?? 0d) + HeightOffsetMeters,
                    HeadingDegrees: NormalizeHeading(arrow.HeadingDegrees),
                    WidthMeters: Math.Round(DefaultWidthMeters * emphasis, 2),
                    LengthMeters: Math.Round(DefaultLengthMeters * emphasis, 2),
                    HeightOffsetMeters: HeightOffsetMeters,
                    Opacity: Math.Round(opacity, 3),
                    DistanceAheadMeters: arrow.DistanceAheadMeters,
                    Kind: arrow.Kind);
            })
            .Where(primitive => primitive.Opacity > 0d)
            .ToArray();
    }

    private static double NormalizeHeading(double heading)
    {
        var normalized = heading % 360d;
        return normalized < 0d
            ? normalized + 360d
            : normalized;
    }
}
