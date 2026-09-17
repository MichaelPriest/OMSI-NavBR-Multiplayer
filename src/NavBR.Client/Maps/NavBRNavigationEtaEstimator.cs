namespace NavBR.Client.Maps;

internal readonly record struct NavBRNavigationEtaEstimate(
    TimeSpan? ToNextStop,
    TimeSpan? ToRouteEnd,
    double? PaceMetersPerSecond)
{
    public static NavBRNavigationEtaEstimate Unavailable => new(null, null, null);
}

/// <summary>
/// Learns a short rolling pace from real progress along the resolved OMSI route.
/// It deliberately returns no ETA until enough trustworthy movement samples exist.
/// </summary>
internal sealed class NavBRNavigationEtaEstimator
{
    private const int MinimumProgressSamples = 3;
    private const double MinimumSampleSeconds = 0.45d;
    private const double MaximumSampleSeconds = 5d;
    private const double MinimumProgressMeters = 0.5d;
    private const double MinimumReliablePaceMetersPerSecond = 0.75d;
    private const double MaximumPlausiblePaceMetersPerSecond = 45d;
    private const double PaceSmoothingFactor = 0.35d;
    private static readonly TimeSpan PaceStaleAfter = TimeSpan.FromSeconds(15d);
    private static readonly TimeSpan MaximumEta = TimeSpan.FromHours(8d);

    private DateTimeOffset? _lastObservedAtUtc;
    private DateTimeOffset? _lastProgressAtUtc;
    private double? _lastRemainingMeters;
    private double? _lastProgressPercent;
    private double? _smoothedPaceMetersPerSecond;
    private string? _lastDestinationName;
    private int _validProgressSamples;

    public NavBRNavigationEtaEstimate Observe(
        NavBRNavigationSnapshot snapshot,
        DateTimeOffset observedAtUtc)
    {
        if (!snapshot.RouteAvailable ||
            !snapshot.IsOnRoute ||
            !double.IsFinite(snapshot.DistanceRemainingMeters) ||
            snapshot.DistanceRemainingMeters < 0d)
        {
            Reset();
            return NavBRNavigationEtaEstimate.Unavailable;
        }

        if (ShouldResetForRouteChange(snapshot))
        {
            Reset();
        }

        if (_lastObservedAtUtc is DateTimeOffset previousAt &&
            _lastRemainingMeters is double previousRemaining)
        {
            var elapsedSeconds = (observedAtUtc - previousAt).TotalSeconds;
            var progressMeters = previousRemaining - snapshot.DistanceRemainingMeters;

            if (elapsedSeconds is >= MinimumSampleSeconds and <= MaximumSampleSeconds &&
                progressMeters >= MinimumProgressMeters)
            {
                var observedPace = progressMeters / elapsedSeconds;
                if (double.IsFinite(observedPace) &&
                    observedPace is >= MinimumReliablePaceMetersPerSecond and <= MaximumPlausiblePaceMetersPerSecond)
                {
                    _smoothedPaceMetersPerSecond = _smoothedPaceMetersPerSecond is double current
                        ? current + ((observedPace - current) * PaceSmoothingFactor)
                        : observedPace;
                    _validProgressSamples++;
                    _lastProgressAtUtc = observedAtUtc;
                }
            }
        }

        _lastObservedAtUtc = observedAtUtc;
        _lastRemainingMeters = snapshot.DistanceRemainingMeters;
        _lastProgressPercent = snapshot.RouteProgressPercent;
        _lastDestinationName = Normalize(snapshot.DestinationName);

        if (_validProgressSamples < MinimumProgressSamples ||
            _smoothedPaceMetersPerSecond is not double pace ||
            pace < MinimumReliablePaceMetersPerSecond ||
            _lastProgressAtUtc is not DateTimeOffset lastProgress ||
            observedAtUtc - lastProgress > PaceStaleAfter)
        {
            return NavBRNavigationEtaEstimate.Unavailable;
        }

        return new NavBRNavigationEtaEstimate(
            ToEta(snapshot.DistanceToNextStopMeters, pace),
            ToEta(snapshot.DistanceRemainingMeters, pace),
            pace);
    }

    public void Reset()
    {
        _lastObservedAtUtc = null;
        _lastProgressAtUtc = null;
        _lastRemainingMeters = null;
        _lastProgressPercent = null;
        _smoothedPaceMetersPerSecond = null;
        _lastDestinationName = null;
        _validProgressSamples = 0;
    }

    private bool ShouldResetForRouteChange(NavBRNavigationSnapshot snapshot)
    {
        var destination = Normalize(snapshot.DestinationName);
        if (!string.IsNullOrWhiteSpace(_lastDestinationName) &&
            !string.IsNullOrWhiteSpace(destination) &&
            !string.Equals(_lastDestinationName, destination, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (_lastRemainingMeters is double previousRemaining &&
            snapshot.DistanceRemainingMeters > previousRemaining + 250d)
        {
            return true;
        }

        return _lastProgressPercent is double previousProgress &&
               snapshot.RouteProgressPercent + 5d < previousProgress;
    }

    private static TimeSpan? ToEta(double? distanceMeters, double paceMetersPerSecond)
    {
        if (distanceMeters is not double distance ||
            !double.IsFinite(distance) ||
            distance < 0d ||
            !double.IsFinite(paceMetersPerSecond) ||
            paceMetersPerSecond <= 0d)
        {
            return null;
        }

        var seconds = distance / paceMetersPerSecond;
        if (!double.IsFinite(seconds) || seconds < 0d || seconds > MaximumEta.TotalSeconds)
        {
            return null;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
