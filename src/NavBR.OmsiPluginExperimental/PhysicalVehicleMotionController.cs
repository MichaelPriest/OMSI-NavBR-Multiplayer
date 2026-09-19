using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

namespace NavBR.OmsiPluginExperimental;

/// <summary>
/// Smooths NavBR-owned remote buses between multiplayer telemetry frames.
/// All OMSI writes remain on the OMSI callback thread.
/// </summary>
internal static class PhysicalVehicleMotionController
{
    private const long MinimumTickIntervalMs = 16;
    private const double MinimumInterpolationMs = 70d;
    private const double MaximumInterpolationMs = 220d;
    private const double DefaultInterpolationMs = 120d;
    private const double TeleportDistanceMeters = 30d;
    private const long TeleportGapMs = 1_500;
    private const long StaleTargetAfterMs = 5_000;

    private static readonly Dictionary<string, MotionState> States =
        new(StringComparer.OrdinalIgnoreCase);
    private static long _lastTickMs;

    public static int ActiveCount => States.Count;

    public static bool TryInitialize(
        PhysicalVehicleInstance instance,
        PluginBridgeMessage command,
        out string? errorCode,
        out string? errorMessage)
    {
        errorCode = null;
        errorMessage = null;

        if (!TryReadSnapshot(command, out var snapshot))
        {
            errorCode = "invalid-pose";
            errorMessage = "Physical vehicle updates require finite local position and quaternion values.";
            return false;
        }

        if (!TryApplyVisualState(instance, command))
        {
            errorCode = "visual-state-write-failed";
            errorMessage = "OMSI rejected the guarded vehicle visual-state write.";
            return false;
        }

        if (!TryApplyTransform(instance, snapshot))
        {
            errorCode = "transform-write-failed";
            errorMessage = "OMSI rejected the guarded vehicle transform write.";
            return false;
        }

        var now = Environment.TickCount64;
        States[instance.InstanceId] = new MotionState
        {
            Current = snapshot,
            Start = snapshot,
            Target = snapshot,
            StartTickMs = now,
            LastTargetTickMs = now,
            DurationMs = DefaultInterpolationMs,
            LastSourceTimestampMs = command.TimestampUnixMilliseconds
        };
        return true;
    }

    public static bool TrySetTarget(
        PhysicalVehicleInstance instance,
        PluginBridgeMessage command,
        out string? errorCode,
        out string? errorMessage)
    {
        errorCode = null;
        errorMessage = null;

        if (!TryReadSnapshot(command, out var target))
        {
            errorCode = "invalid-pose";
            errorMessage = "Physical vehicle updates require finite local position and quaternion values.";
            return false;
        }

        if (!States.TryGetValue(instance.InstanceId, out var state))
        {
            return TryInitialize(instance, command, out errorCode, out errorMessage);
        }

        var sourceTimestamp = command.TimestampUnixMilliseconds;
        if (sourceTimestamp is long sourceMs &&
            state.LastSourceTimestampMs is long previousSourceMs &&
            sourceMs <= previousSourceMs)
        {
            // Reconnects/network jitter can surface an older telemetry sample.
            // Never rewind a physical bus to an older state.
            return true;
        }

        if (!TryApplyVisualState(instance, command))
        {
            errorCode = "visual-state-write-failed";
            errorMessage = "OMSI rejected the guarded vehicle visual-state write.";
            return false;
        }

        var now = Environment.TickCount64;
        var gapMs = Math.Max(0L, now - state.LastTargetTickMs);
        var distance = Distance(state.Current, target);

        if (gapMs >= TeleportGapMs || distance >= TeleportDistanceMeters)
        {
            // Map teleports/repositions should snap instead of dragging the bus
            // through unrelated scenery.
            if (!TryApplyTransform(instance, target))
            {
                errorCode = "transform-write-failed";
                errorMessage = "OMSI rejected the guarded vehicle transform write.";
                return false;
            }

            state.Current = target;
            state.Start = target;
            state.Target = target;
            state.StartTickMs = now;
            state.LastTargetTickMs = now;
            state.DurationMs = DefaultInterpolationMs;
            state.LastSourceTimestampMs = sourceTimestamp;
            return true;
        }

        state.Start = state.Current;
        state.Target = target;
        state.StartTickMs = now;
        state.DurationMs = gapMs <= 0
            ? DefaultInterpolationMs
            : Math.Clamp(gapMs * 0.85d, MinimumInterpolationMs, MaximumInterpolationMs);
        state.LastTargetTickMs = now;
        state.LastSourceTimestampMs = sourceTimestamp;
        return true;
    }

    public static void Tick()
    {
        var now = Environment.TickCount64;
        if (_lastTickMs > 0 && now - _lastTickMs < MinimumTickIntervalMs)
        {
            return;
        }

        _lastTickMs = now;
        foreach (var pair in States.ToArray())
        {
            var instanceId = pair.Key;
            var state = pair.Value;

            if (!PhysicalVehicleInstanceRegistry.TryGet(instanceId, out var instance) ||
                OmsiNativeInterop.IsRoadVehiclePointer(instance.VehiclePointer) != 1)
            {
                States.Remove(instanceId);
                continue;
            }

            if (now - state.LastTargetTickMs >= StaleTargetAfterMs)
            {
                // If the desktop app, SignalR connection or named pipe dies
                // without a clean despawn, never leave an orphan NavBR bus in
                // OMSI indefinitely.
                if (OmsiNativeInterop.MarkVehicleForKilling(instance.VehiclePointer) == 1)
                {
                    PhysicalVehicleInstanceRegistry.TryRemove(instanceId, out _);
                    States.Remove(instanceId);
                }
                else
                {
                    // Preserve ownership and retry at a bounded cadence instead
                    // of forgetting a still-live OMSI object.
                    state.LastTargetTickMs =
                        now - StaleTargetAfterMs + 1_000;
                }
                continue;
            }

            var duration = Math.Max(1d, state.DurationMs);
            var amount = Math.Clamp((now - state.StartTickMs) / duration, 0d, 1d);
            var next = Interpolate(state.Start, state.Target, amount);

            if (!TryApplyTransform(instance, next))
            {
                States.Remove(instanceId);
                continue;
            }

            state.Current = next;
        }
    }

    public static void Remove(string? instanceId)
    {
        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            States.Remove(instanceId);
        }
    }

    public static void Clear()
    {
        States.Clear();
        _lastTickMs = 0;
    }

    private static bool TryApplyTransform(
        PhysicalVehicleInstance instance,
        MotionSnapshot snapshot) =>
        OmsiNativeInterop.SetVehicleTransform(
            instance.VehiclePointer,
            snapshot.X,
            snapshot.Y,
            snapshot.Z,
            snapshot.RotationX,
            snapshot.RotationY,
            snapshot.RotationZ,
            snapshot.RotationW,
            snapshot.SpeedMps) == 1;

    private static bool TryApplyVisualState(
        PhysicalVehicleInstance instance,
        PluginBridgeMessage command)
    {
        var lightFlags = command.LightFlags ?? 0;
        if (command.BrakePercent is double brakePercent &&
            double.IsFinite(brakePercent) &&
            brakePercent > 1d)
        {
            lightFlags |= (int)VehicleLightFlags.Brake;
        }

        return OmsiNativeInterop.SetVehicleVisualState(
            instance.VehiclePointer,
            lightFlags,
            command.TurnSignal ?? 0) == 1;
    }

    private static bool TryReadSnapshot(
        PluginBridgeMessage command,
        out MotionSnapshot snapshot)
    {
        snapshot = default;
        if (command.LocalX is not double x || !double.IsFinite(x) ||
            command.LocalY is not double y || !double.IsFinite(y) ||
            command.LocalZ is not double z || !double.IsFinite(z) ||
            command.RotationX is not double rotationX || !double.IsFinite(rotationX) ||
            command.RotationY is not double rotationY || !double.IsFinite(rotationY) ||
            command.RotationZ is not double rotationZ || !double.IsFinite(rotationZ) ||
            command.RotationW is not double rotationW || !double.IsFinite(rotationW))
        {
            return false;
        }

        if (Math.Abs(x) > 100000d || Math.Abs(y) > 100000d || Math.Abs(z) > 100000d)
        {
            return false;
        }

        var length = Math.Sqrt(
            rotationX * rotationX +
            rotationY * rotationY +
            rotationZ * rotationZ +
            rotationW * rotationW);
        if (!double.IsFinite(length) || length < 0.0001d)
        {
            return false;
        }

        var inverse = 1d / length;
        var speedMps = command.SpeedKph is double speedKph && double.IsFinite(speedKph)
            ? (float)Math.Clamp(Math.Abs(speedKph) / 3.6d, 0d, 150d)
            : 0f;

        snapshot = new MotionSnapshot(
            (float)x,
            (float)y,
            (float)z,
            (float)(rotationX * inverse),
            (float)(rotationY * inverse),
            (float)(rotationZ * inverse),
            (float)(rotationW * inverse),
            speedMps);
        return true;
    }

    private static MotionSnapshot Interpolate(
        MotionSnapshot from,
        MotionSnapshot to,
        double amount)
    {
        var t = (float)Math.Clamp(amount, 0d, 1d);

        var dot =
            from.RotationX * to.RotationX +
            from.RotationY * to.RotationY +
            from.RotationZ * to.RotationZ +
            from.RotationW * to.RotationW;
        var sign = dot < 0f ? -1f : 1f;

        var qx = Lerp(from.RotationX, to.RotationX * sign, t);
        var qy = Lerp(from.RotationY, to.RotationY * sign, t);
        var qz = Lerp(from.RotationZ, to.RotationZ * sign, t);
        var qw = Lerp(from.RotationW, to.RotationW * sign, t);
        var qLength = MathF.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
        if (float.IsFinite(qLength) && qLength > 0.0001f)
        {
            var inverse = 1f / qLength;
            qx *= inverse;
            qy *= inverse;
            qz *= inverse;
            qw *= inverse;
        }
        else
        {
            qx = to.RotationX;
            qy = to.RotationY;
            qz = to.RotationZ;
            qw = to.RotationW;
        }

        return new MotionSnapshot(
            Lerp(from.X, to.X, t),
            Lerp(from.Y, to.Y, t),
            Lerp(from.Z, to.Z, t),
            qx,
            qy,
            qz,
            qw,
            Lerp(from.SpeedMps, to.SpeedMps, t));
    }

    private static float Lerp(float from, float to, float amount) =>
        from + (to - from) * amount;

    private static double Distance(MotionSnapshot left, MotionSnapshot right)
    {
        var dx = (double)right.X - left.X;
        var dy = (double)right.Y - left.Y;
        var dz = (double)right.Z - left.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private sealed class MotionState
    {
        public MotionSnapshot Current { get; set; }
        public MotionSnapshot Start { get; set; }
        public MotionSnapshot Target { get; set; }
        public long StartTickMs { get; set; }
        public long LastTargetTickMs { get; set; }
        public double DurationMs { get; set; }
        public long? LastSourceTimestampMs { get; set; }
    }

    private readonly record struct MotionSnapshot(
        float X,
        float Y,
        float Z,
        float RotationX,
        float RotationY,
        float RotationZ,
        float RotationW,
        float SpeedMps);
}
