namespace NavBR.Client.Ghost;

internal sealed record GhostReplayAnalytics(
    double DurationSeconds,
    double EstimatedDistanceKm,
    double AverageSpeedKph,
    double MaximumSpeedKph,
    int ValidSpeedSamples);

internal static class GhostReplayAnalyticsCalculator
{
    public static GhostReplayAnalytics Analyze(GhostReplayDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var frames = document.Frames;
        if (frames.Count == 0)
        {
            return new GhostReplayAnalytics(0d, 0d, 0d, 0d, 0);
        }

        var durationMilliseconds = Math.Max(
            0L,
            frames[^1].OffsetMilliseconds - frames[0].OffsetMilliseconds);
        var durationSeconds = durationMilliseconds / 1000d;

        var validSpeedSamples = 0;
        var maximumSpeedKph = 0d;
        foreach (var frame in frames)
        {
            var speed = frame.Telemetry.SpeedKph;
            if (!IsValidSpeed(speed))
            {
                continue;
            }

            validSpeedSamples++;
            maximumSpeedKph = Math.Max(maximumSpeedKph, speed);
        }

        var distanceMeters = 0d;
        for (var index = 1; index < frames.Count; index++)
        {
            var previous = frames[index - 1];
            var current = frames[index];
            var previousSpeed = previous.Telemetry.SpeedKph;
            var currentSpeed = current.Telemetry.SpeedKph;
            if (!IsValidSpeed(previousSpeed) || !IsValidSpeed(currentSpeed))
            {
                continue;
            }

            var elapsedMilliseconds = current.OffsetMilliseconds - previous.OffsetMilliseconds;
            if (elapsedMilliseconds <= 0)
            {
                continue;
            }

            var elapsedSeconds = elapsedMilliseconds / 1000d;
            var averageMetersPerSecond = ((previousSpeed + currentSpeed) / 2d) / 3.6d;
            distanceMeters += averageMetersPerSecond * elapsedSeconds;
        }

        var estimatedDistanceKm = Math.Max(0d, distanceMeters / 1000d);
        var averageSpeedKph = durationSeconds > 0d
            ? estimatedDistanceKm / (durationSeconds / 3600d)
            : 0d;

        return new GhostReplayAnalytics(
            durationSeconds,
            estimatedDistanceKm,
            Math.Max(0d, averageSpeedKph),
            Math.Max(0d, maximumSpeedKph),
            validSpeedSamples);
    }

    private static bool IsValidSpeed(double speed) =>
        double.IsFinite(speed) && speed >= 0d;
}
