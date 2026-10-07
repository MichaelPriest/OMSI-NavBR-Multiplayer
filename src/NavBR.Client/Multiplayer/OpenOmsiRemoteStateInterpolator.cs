using System.Diagnostics;
using NavBR.Shared.OpenOmsi;

namespace NavBR.Client.Multiplayer;

/// <summary>
/// openOMSI-compatible remote pose playback. Network states are kept on the
/// sender's clock and drawn slightly in the past so UDP jitter and reordering
/// cannot pull a remote bus backwards on every late packet.
/// </summary>
internal sealed class OpenOmsiRemoteStateInterpolator
{
    private const double MinimumDelaySeconds = 0.12d;
    private const double MaximumDelaySeconds = 0.45d;
    private const double MaximumExtrapolationSeconds = 0.30d;
    private const double TeleportDistanceMeters = 40d;
    private const int MaximumSamples = 40;

    private readonly object _sync = new();
    private readonly List<Sample> _samples = [];
    private double? _clockOffsetSeconds;
    private double? _playTimeSeconds;
    private double? _playAtLocalSeconds;

    internal bool TryPushAndInterpolate(
        OpenOmsiLanVehicleState state,
        out OpenOmsiLanVehicleState interpolated)
    {
        lock (_sync)
        {
            if (state.SentMilliseconds == 0)
            {
                interpolated = state;
                return true;
            }

            var sentSeconds =
                state.SentMilliseconds / 1000d;
            if (_samples.Count > 0)
            {
                var previousSent =
                    _samples[^1].SentSeconds;
                if (sentSeconds <= previousSent)
                {
                    // The sender restarted and its monotonic clock began again.
                    if (previousSent - sentSeconds > 30d)
                    {
                        ResetCore();
                    }
                    else
                    {
                        interpolated = default!;
                        return false;
                    }
                }
            }

            var now = MonotonicSeconds();
            var observedOffset =
                now - sentSeconds;
            _clockOffsetSeconds =
                _clockOffsetSeconds is double current &&
                observedOffset >= current
                    ? current +
                      (observedOffset - current) * 0.01d
                    : observedOffset;

            _samples.Add(
                new Sample(sentSeconds, state));
            if (_samples.Count > MaximumSamples)
            {
                _samples.RemoveRange(
                    0,
                    _samples.Count - MaximumSamples);
            }

            interpolated =
                InterpolateCore(now) ?? state;
            return true;
        }
    }

    internal bool TryInterpolateCurrent(
        out OpenOmsiLanVehicleState interpolated)
    {
        lock (_sync)
        {
            var current =
                InterpolateCore(
                    MonotonicSeconds());
            if (current is null)
            {
                interpolated = default!;
                return false;
            }

            interpolated = current;
            return true;
        }
    }

    internal void Reset()
    {
        lock (_sync)
        {
            ResetCore();
        }
    }

    private void ResetCore()
    {
        _samples.Clear();
        _clockOffsetSeconds = null;
        _playTimeSeconds = null;
        _playAtLocalSeconds = null;
    }

    private OpenOmsiLanVehicleState? InterpolateCore(
        double now)
    {
        if (_samples.Count == 0 ||
            _clockOffsetSeconds is not double offset)
        {
            return null;
        }

        var gap = 0.05d;
        if (_samples.Count >= 2)
        {
            var first =
                Math.Max(0, _samples.Count - 4);
            gap = 0d;
            for (var i = first + 1;
                 i < _samples.Count;
                 i++)
            {
                gap = Math.Max(
                    gap,
                    _samples[i].SentSeconds -
                    _samples[i - 1].SentSeconds);
            }
        }

        var delay =
            Math.Clamp(
                gap * 2d + 0.02d,
                MinimumDelaySeconds,
                MaximumDelaySeconds);
        var wanted =
            now - offset - delay;
        var t =
            StepPlayClock(
                now,
                wanted,
                snapSeconds: 0.5d);

        var k = -1;
        for (var i = _samples.Count - 1;
             i >= 0;
             i--)
        {
            if (_samples[i].SentSeconds <= t)
            {
                k = i;
                break;
            }
        }

        if (k < 0)
        {
            return _samples[0].State;
        }

        if (k + 1 >= _samples.Count)
        {
            return Extrapolate(
                _samples[^1],
                t);
        }

        var a = _samples[k];
        var b = _samples[k + 1];
        var duration =
            b.SentSeconds - a.SentSeconds;
        var factor =
            duration > 0d
                ? Math.Clamp(
                    (t - a.SentSeconds) / duration,
                    0d,
                    1d)
                : 1d;

        var dx = b.State.X - a.State.X;
        var dy = b.State.Y - a.State.Y;
        if (Math.Sqrt(dx * dx + dy * dy) >
            TeleportDistanceMeters)
        {
            return factor < 0.5d
                ? a.State
                : b.State;
        }

        return InterpolateState(
            a.State,
            b.State,
            factor);
    }

    private double StepPlayClock(
        double now,
        double wanted,
        double snapSeconds)
    {
        double next;
        if (_playTimeSeconds is double t &&
            _playAtLocalSeconds is double then)
        {
            var elapsed =
                Math.Max(0d, now - then);
            var run = t + elapsed;
            var error = wanted - run;
            next =
                Math.Abs(error) > snapSeconds
                    ? wanted
                    : run +
                      Math.Clamp(
                          error,
                          -0.06d * elapsed,
                          0.06d * elapsed);
        }
        else
        {
            next = wanted;
        }

        _playTimeSeconds = next;
        _playAtLocalSeconds = now;
        return next;
    }

    private static OpenOmsiLanVehicleState Extrapolate(
        Sample sample,
        double targetSeconds)
    {
        var ahead =
            Math.Clamp(
                targetSeconds -
                sample.SentSeconds,
                0d,
                MaximumExtrapolationSeconds);
        if (ahead <= 0d)
        {
            return sample.State;
        }

        var state = sample.State;
        var headingRadians =
            state.HeadingDegrees *
            Math.PI / 180d;
        var distance =
            state.SpeedKph / 3.6d *
            ahead;
        var moveX =
            Math.Sin(headingRadians) *
            distance;
        var moveY =
            Math.Cos(headingRadians) *
            distance;

        var rear = state.RearSections
            .Select(section =>
                section with
                {
                    X = section.X + moveX,
                    Y = section.Y + moveY
                })
            .ToArray();

        OpenOmsiLanWalker? walker =
            state.Walker;
        if (walker is
            {
                Seated: false,
                AboardOwner: null
            } onFoot)
        {
            var course =
                float.IsFinite(
                    onFoot.CourseDegrees)
                    ? onFoot.CourseDegrees
                    : onFoot.HeadingDegrees;
            var courseRadians =
                course *
                Math.PI / 180d;
            var walkerDistance =
                onFoot.SpeedMps *
                ahead;
            walker =
                onFoot with
                {
                    X =
                        onFoot.X +
                        Math.Sin(courseRadians) *
                        walkerDistance,
                    Y =
                        onFoot.Y +
                        Math.Cos(courseRadians) *
                        walkerDistance
                };
        }

        return state with
        {
            X = state.X + moveX,
            Y = state.Y + moveY,
            RearSections = rear,
            Walker = walker
        };
    }

    private static OpenOmsiLanVehicleState
        InterpolateState(
            OpenOmsiLanVehicleState a,
            OpenOmsiLanVehicleState b,
            double factor)
    {
        var state =
            b with
            {
                X = Lerp(a.X, b.X, factor),
                Y = Lerp(a.Y, b.Y, factor),
                Z = Lerp(a.Z, b.Z, factor),
                HeadingDegrees =
                    (float)LerpAngle(
                        a.HeadingDegrees,
                        b.HeadingDegrees,
                        factor),
                PitchDegrees =
                    LerpFloat(
                        a.PitchDegrees,
                        b.PitchDegrees,
                        factor),
                BankDegrees =
                    LerpFloat(
                        a.BankDegrees,
                        b.BankDegrees,
                        factor),
                SpeedKph =
                    LerpFloat(
                        a.SpeedKph,
                        b.SpeedKph,
                        factor),
                SteeringDegrees =
                    LerpFloat(
                        a.SteeringDegrees,
                        b.SteeringDegrees,
                        factor),
                EngineRpm =
                    LerpFloat(
                        a.EngineRpm,
                        b.EngineRpm,
                        factor),
                Doors =
                    Mix(
                        a.Doors,
                        b.Doors,
                        factor),
                Suspension =
                    Mix(
                        a.Suspension,
                        b.Suspension,
                        factor),
                Values =
                    Mix(
                        a.Values,
                        b.Values,
                        factor)
            };

        if (a.RearSections.Count ==
            b.RearSections.Count)
        {
            state =
                state with
                {
                    RearSections =
                        b.RearSections
                            .Select(
                                (rearB, i) =>
                                {
                                    var rearA =
                                        a.RearSections[i];
                                    return rearB with
                                    {
                                        X =
                                            Lerp(
                                                rearA.X,
                                                rearB.X,
                                                factor),
                                        Y =
                                            Lerp(
                                                rearA.Y,
                                                rearB.Y,
                                                factor),
                                        Z =
                                            Lerp(
                                                rearA.Z,
                                                rearB.Z,
                                                factor),
                                        HeadingDegrees =
                                            (float)LerpAngle(
                                                rearA.HeadingDegrees,
                                                rearB.HeadingDegrees,
                                                factor)
                                    };
                                })
                            .ToArray()
                };
        }

        if (a.Walker is { } walkerA &&
            b.Walker is { } walkerB)
        {
            var aboard =
                InterpolateAboardLocal(
                    walkerA,
                    walkerB,
                    factor);
            state =
                state with
                {
                    Walker =
                        walkerB with
                        {
                            X =
                                Lerp(
                                    walkerA.X,
                                    walkerB.X,
                                    factor),
                            Y =
                                Lerp(
                                    walkerA.Y,
                                    walkerB.Y,
                                    factor),
                            Z =
                                Lerp(
                                    walkerA.Z,
                                    walkerB.Z,
                                    factor),
                            HeadingDegrees =
                                (float)LerpAngle(
                                    walkerA.HeadingDegrees,
                                    walkerB.HeadingDegrees,
                                    factor),
                            CourseDegrees =
                                float.IsFinite(
                                    walkerA.CourseDegrees) &&
                                float.IsFinite(
                                    walkerB.CourseDegrees)
                                    ? (float)LerpAngle(
                                        walkerA.CourseDegrees,
                                        walkerB.CourseDegrees,
                                        factor)
                                    : walkerB.CourseDegrees,
                            SpeedMps =
                                LerpFloat(
                                    walkerA.SpeedMps,
                                    walkerB.SpeedMps,
                                    factor),
                            AboardLocal = aboard
                        }
                };
        }

        return state;
    }

    private static float[]? InterpolateAboardLocal(
        OpenOmsiLanWalker a,
        OpenOmsiLanWalker b,
        double factor)
    {
        if (a.AboardOwner is not ushort ownerA ||
            b.AboardOwner is not ushort ownerB ||
            ownerA != ownerB ||
            a.AboardLocal is not { Length: >= 3 } localA ||
            b.AboardLocal is not { Length: >= 3 } localB)
        {
            return b.AboardLocal;
        }

        var mixed =
            b.AboardLocal.ToArray();
        for (var i = 0;
             i < Math.Min(3, mixed.Length);
             i++)
        {
            mixed[i] =
                LerpFloat(
                    localA[i],
                    localB[i],
                    factor);
        }

        return mixed;
    }

    private static float[] Mix(
        IReadOnlyList<float> a,
        IReadOnlyList<float> b,
        double factor)
    {
        if (a.Count != b.Count)
        {
            return b.ToArray();
        }

        var result = new float[b.Count];
        for (var i = 0;
             i < result.Length;
             i++)
        {
            result[i] =
                LerpFloat(
                    a[i],
                    b[i],
                    factor);
        }

        return result;
    }

    private static double Lerp(
        double a,
        double b,
        double factor) =>
        a + (b - a) * factor;

    private static float LerpFloat(
        float a,
        float b,
        double factor) =>
        a + (b - a) * (float)factor;

    private static double LerpAngle(
        double a,
        double b,
        double factor)
    {
        var delta =
            ((b - a + 540d) % 360d +
             360d) % 360d -
            180d;
        var value =
            (a + delta * factor) % 360d;
        return value < 0d
            ? value + 360d
            : value;
    }

    private static double MonotonicSeconds() =>
        Stopwatch.GetTimestamp() /
        (double)Stopwatch.Frequency;

    private sealed record Sample(
        double SentSeconds,
        OpenOmsiLanVehicleState State);
}
