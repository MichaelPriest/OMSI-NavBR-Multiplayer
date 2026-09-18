using System.Numerics;

namespace NavBR.Client.Telemetry;

/// <summary>
/// Read-only current OMSI camera matrices. Used only for projecting NavBR
/// overlays; no camera or game memory is ever written by this path.
/// </summary>
internal readonly record struct OmsiCameraProjectionSnapshot(
    Matrix4x4 View,
    Matrix4x4 Projection,
    DateTimeOffset CapturedAtUtc);
