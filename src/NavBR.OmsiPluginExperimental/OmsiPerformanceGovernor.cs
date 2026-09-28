using System.Diagnostics;

namespace NavBR.OmsiPluginExperimental;

/// <summary>
/// Adaptive guard for work executed on OMSI's callback/render thread.
///
/// The goal is not to "speed up OMSI" by changing its renderer. Instead this
/// governor makes NavBR back off automatically when its own native/plugin work
/// starts consuming too much of the simulator frame budget.
/// </summary>
internal static class OmsiPerformanceGovernor
{
    private const double TargetPluginWorkMs = 1.25d;
    private const double SeverePluginWorkMs = 4.0d;
    private const double EwmaAlpha = 0.12d;

    private static readonly object Sync = new();

    private static int _pressureLevel;
    private static int _healthySlices;
    private static double _lastWorkMs;
    private static double _averageWorkMs;
    private static double _averageFrameIntervalMs;
    private static long _lastFrameTickMs;

    public static void Reset()
    {
        lock (Sync)
        {
            _pressureLevel = 0;
            _healthySlices = 0;
            _lastWorkMs = 0d;
            _averageWorkMs = 0d;
            _averageFrameIntervalMs = 0d;
            _lastFrameTickMs = 0;
        }
    }

    public static void ObserveFrame(long nowTickMs)
    {
        lock (Sync)
        {
            if (_lastFrameTickMs > 0 &&
                nowTickMs >= _lastFrameTickMs)
            {
                var interval = nowTickMs - _lastFrameTickMs;
                if (interval is > 0 and < 1_000)
                {
                    _averageFrameIntervalMs = _averageFrameIntervalMs <= 0d
                        ? interval
                        : Lerp(_averageFrameIntervalMs, interval, 0.08d);

                    // If the simulator itself is already falling below roughly
                    // 28 FPS, NavBR should reduce optional work instead of
                    // competing for more time on the same thread.
                    if (_averageFrameIntervalMs >= 36d &&
                        _pressureLevel < 3)
                    {
                        _pressureLevel++;
                        _healthySlices = 0;
                    }
                }
            }

            _lastFrameTickMs = nowTickMs;
        }
    }

    public static OmsiWorkBudget GetBudget(
        int activePhysicalVehicles,
        int activeRoleplayCharacters,
        int pendingCommands)
    {
        lock (Sync)
        {
            var baseIntervalMs = activePhysicalVehicles switch
            {
                >= 9 => 33L,
                >= 5 => 24L,
                _ => 16L
            };
            var baseCommands = activePhysicalVehicles switch
            {
                >= 9 => 2,
                >= 5 => 3,
                _ => 4
            };

            // A large queue should keep making forward progress, but never at
            // the cost of an unbounded callback spike.
            if (pendingCommands >= 24 && _pressureLevel == 0)
            {
                baseCommands = Math.Min(baseCommands + 1, 5);
            }

            var intervalMs = _pressureLevel switch
            {
                1 => Math.Max(baseIntervalMs, 24L),
                2 => Math.Max(baseIntervalMs, 40L),
                3 => Math.Max(baseIntervalMs, 60L),
                _ => baseIntervalMs
            };
            var maxCommands = _pressureLevel switch
            {
                1 => Math.Min(baseCommands, 3),
                2 => Math.Min(baseCommands, 2),
                3 => 1,
                _ => baseCommands
            };
            var statusIntervalMs = _pressureLevel switch
            {
                1 => 300L,
                2 => 500L,
                3 => 750L,
                _ => 200L
            };
            var motionMinimumIntervalMs = _pressureLevel switch
            {
                1 => 25L,
                2 => 50L,
                3 => 75L,
                _ => 0L
            };

            // Keep RP responsive. One possessed human is cheap relative to
            // physical bus synchronization, so do not push the work cadence
            // above 50 ms solely because RP is active.
            if (activeRoleplayCharacters > 0)
            {
                intervalMs = Math.Min(intervalMs, 50L);
            }

            return new OmsiWorkBudget(
                intervalMs,
                maxCommands,
                statusIntervalMs,
                motionMinimumIntervalMs,
                _pressureLevel,
                _lastWorkMs,
                _averageWorkMs,
                _averageFrameIntervalMs);
        }
    }

    public static long BeginWorkSlice() => Stopwatch.GetTimestamp();

    public static void EndWorkSlice(long startTimestamp)
    {
        if (startTimestamp <= 0)
        {
            return;
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
        if (elapsedTicks < 0)
        {
            return;
        }

        var elapsedMs =
            elapsedTicks * 1000d / Stopwatch.Frequency;

        lock (Sync)
        {
            _lastWorkMs = elapsedMs;
            _averageWorkMs = _averageWorkMs <= 0d
                ? elapsedMs
                : Lerp(_averageWorkMs, elapsedMs, EwmaAlpha);

            if (elapsedMs >= SeverePluginWorkMs ||
                _averageWorkMs >= TargetPluginWorkMs * 2d)
            {
                _pressureLevel = Math.Min(3, _pressureLevel + 1);
                _healthySlices = 0;
                return;
            }

            if (elapsedMs > TargetPluginWorkMs ||
                _averageWorkMs > TargetPluginWorkMs)
            {
                _pressureLevel = Math.Min(3, _pressureLevel + 1);
                _healthySlices = 0;
                return;
            }

            if (_averageWorkMs <= TargetPluginWorkMs * 0.55d &&
                _averageFrameIntervalMs is > 0d and < 28d)
            {
                _healthySlices++;
                if (_healthySlices >= 90 && _pressureLevel > 0)
                {
                    _pressureLevel--;
                    _healthySlices = 0;
                }
            }
            else
            {
                _healthySlices = 0;
            }
        }
    }

    public static string Summary
    {
        get
        {
            lock (Sync)
            {
                return
                    $"pressure={_pressureLevel} " +
                    $"work={_lastWorkMs:F2}ms " +
                    $"avgWork={_averageWorkMs:F2}ms " +
                    $"avgFrame={_averageFrameIntervalMs:F1}ms";
            }
        }
    }

    private static double Lerp(double current, double sample, double alpha) =>
        current + ((sample - current) * alpha);
}

internal readonly record struct OmsiWorkBudget(
    long MinimumWorkIntervalMs,
    int MaxCommands,
    long StatusIntervalMs,
    long MotionMinimumIntervalMs,
    int PressureLevel,
    double LastWorkMilliseconds,
    double AverageWorkMilliseconds,
    double AverageFrameIntervalMilliseconds);
