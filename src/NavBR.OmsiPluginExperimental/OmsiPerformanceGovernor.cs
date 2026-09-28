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
    private static double _lastFrameIntervalMs;
    private static double _peakFrameIntervalMs;
    private static long _frameStallCount;
    private static long _lastFrameTickMs;
    private static string _profile = "auto";

    public static string CurrentProfile
    {
        get
        {
            lock (Sync)
            {
                return _profile;
            }
        }
    }

    public static void SetProfile(string? profile)
    {
        lock (Sync)
        {
            _profile = NormalizeProfile(profile);
            _pressureLevel = 0;
            _healthySlices = 0;
        }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            _pressureLevel = 0;
            _healthySlices = 0;
            _lastWorkMs = 0d;
            _averageWorkMs = 0d;
            _averageFrameIntervalMs = 0d;
            _lastFrameIntervalMs = 0d;
            _peakFrameIntervalMs = 0d;
            _frameStallCount = 0;
            _lastFrameTickMs = 0;
        }
    }

    public static void ObserveFrame(long nowTickMs)
    {
        lock (Sync)
        {
            ObserveFrameUnsafe(nowTickMs);
        }
    }

    public static OmsiWorkBudget ObserveFrameAndGetBudget(
        long nowTickMs,
        int activePhysicalVehicles,
        int activeRoleplayCharacters,
        int pendingCommands)
    {
        lock (Sync)
        {
            ObserveFrameUnsafe(nowTickMs);
            return BuildBudgetUnsafe(
                activePhysicalVehicles,
                activeRoleplayCharacters,
                pendingCommands);
        }
    }

    private static void ObserveFrameUnsafe(long nowTickMs)
    {
        if (_lastFrameTickMs > 0 &&
            nowTickMs >= _lastFrameTickMs)
        {
            var interval = nowTickMs - _lastFrameTickMs;
            if (interval is > 0 and < 1_000)
            {
                _lastFrameIntervalMs = interval;
                _peakFrameIntervalMs = Math.Max(_peakFrameIntervalMs, interval);
                if (interval >= 100d)
                {
                    _frameStallCount++;
                }

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

    public static OmsiWorkBudget GetBudget(
        int activePhysicalVehicles,
        int activeRoleplayCharacters,
        int pendingCommands)
    {
        lock (Sync)
        {
            return BuildBudgetUnsafe(
                activePhysicalVehicles,
                activeRoleplayCharacters,
                pendingCommands);
        }
    }

    private static OmsiWorkBudget BuildBudgetUnsafe(
        int activePhysicalVehicles,
        int activeRoleplayCharacters,
        int pendingCommands)
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

            (baseIntervalMs, baseCommands) = _profile switch
            {
                "stability" => (Math.Max(baseIntervalMs, 33L), Math.Min(baseCommands, 2)),
                "multiplayer" => (Math.Max(baseIntervalMs, 20L), Math.Min(baseCommands + 1, 5)),
                "quality" => (Math.Max(12L, baseIntervalMs - 4L), Math.Min(baseCommands + 1, 5)),
                "diagnostics" => (Math.Max(baseIntervalMs, 24L), Math.Min(baseCommands, 2)),
                _ => (baseIntervalMs, baseCommands)
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
            statusIntervalMs = _profile switch
            {
                "stability" => Math.Max(statusIntervalMs, 500L),
                "diagnostics" => 150L,
                _ => statusIntervalMs
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
                _averageFrameIntervalMs,
                _lastFrameIntervalMs,
                _peakFrameIntervalMs,
                _frameStallCount);
    }

    public static long BeginWorkSlice() => Stopwatch.GetTimestamp();

    public static void EndWorkSlice(long startTimestamp)
    {
        if (!TryMeasureWorkSlice(startTimestamp, out var elapsedMs))
        {
            return;
        }

        lock (Sync)
        {
            ApplyWorkSliceUnsafe(elapsedMs);
        }
    }

    public static OmsiWorkBudget EndWorkSliceAndGetBudget(
        long startTimestamp,
        int activePhysicalVehicles,
        int activeRoleplayCharacters,
        int pendingCommands)
    {
        if (!TryMeasureWorkSlice(startTimestamp, out var elapsedMs))
        {
            return GetBudget(
                activePhysicalVehicles,
                activeRoleplayCharacters,
                pendingCommands);
        }

        lock (Sync)
        {
            ApplyWorkSliceUnsafe(elapsedMs);
            return BuildBudgetUnsafe(
                activePhysicalVehicles,
                activeRoleplayCharacters,
                pendingCommands);
        }
    }

    private static bool TryMeasureWorkSlice(
        long startTimestamp,
        out double elapsedMs)
    {
        elapsedMs = 0d;
        if (startTimestamp <= 0)
        {
            return false;
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
        if (elapsedTicks < 0)
        {
            return false;
        }

        elapsedMs = elapsedTicks * 1000d / Stopwatch.Frequency;
        return true;
    }

    private static void ApplyWorkSliceUnsafe(double elapsedMs)
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
                    $"avgFrame={_averageFrameIntervalMs:F1}ms " +
                    $"lastFrame={_lastFrameIntervalMs:F1}ms " +
                    $"peakFrame={_peakFrameIntervalMs:F1}ms " +
                    $"stalls100ms={_frameStallCount}";
            }
        }
    }

    private static string NormalizeProfile(string? profile) =>
        profile?.Trim().ToLowerInvariant() switch
        {
            "stability" => "stability",
            "multiplayer" => "multiplayer",
            "quality" => "quality",
            "diagnostics" => "diagnostics",
            _ => "auto"
        };

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
    double AverageFrameIntervalMilliseconds,
    double LastFrameIntervalMilliseconds,
    double PeakFrameIntervalMilliseconds,
    long FrameStallCount);
