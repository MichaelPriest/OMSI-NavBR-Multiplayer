using System.Numerics;
using NavBR.Client.Telemetry;

namespace NavBR.Client.Multiplayer;

internal readonly record struct RoleplayEgoCameraPose(
    double X,
    double Y,
    double Z,
    double HeadingDegrees,
    DateTimeOffset CapturedAtUtc);

/// <summary>
/// Converts OMSI's read-only render view matrix into a local camera pose.
/// This keeps the RP camera integration independent from any third-party code:
/// NavBR only consumes the camera matrix it already reads for HUD projection.
/// </summary>
internal static class RoleplayEgoCameraPoseResolver
{
    public static bool TryResolve(
        OmsiCameraProjectionSnapshot snapshot,
        out RoleplayEgoCameraPose pose)
    {
        pose = default;

        if (!Matrix4x4.Invert(snapshot.View, out var cameraWorld))
        {
            return false;
        }

        var x = (double)cameraWorld.M41;
        var y = (double)cameraWorld.M42;
        var z = (double)cameraWorld.M43;

        // The inverse view matrix carries the camera basis. Either sign of the
        // forward axis differs only by 180 degrees; the RP controller calibrates
        // a heading offset when F11 is engaged, so camera/build conventions do
        // not leak into character movement.
        var forwardX = (double)cameraWorld.M31;
        var forwardZ = (double)cameraWorld.M33;
        var horizontalLength = Math.Sqrt(
            forwardX * forwardX +
            forwardZ * forwardZ);

        if (!double.IsFinite(x) ||
            !double.IsFinite(y) ||
            !double.IsFinite(z) ||
            !double.IsFinite(horizontalLength) ||
            horizontalLength < 0.0001d)
        {
            return false;
        }

        var heading = Math.Atan2(
            forwardX / horizontalLength,
            forwardZ / horizontalLength) *
            180d / Math.PI;

        heading %= 360d;
        if (heading < 0d)
        {
            heading += 360d;
        }

        if (!double.IsFinite(heading))
        {
            return false;
        }

        pose = new RoleplayEgoCameraPose(
            x,
            y,
            z,
            heading,
            snapshot.CapturedAtUtc);
        return true;
    }
}
