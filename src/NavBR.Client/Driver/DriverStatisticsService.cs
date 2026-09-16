using System.Windows.Threading;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Driver;

internal sealed class DriverStatisticsService : IDisposable
{
    private readonly Func<VehicleTelemetry?> _telemetryProvider;
    private readonly DispatcherTimer _timer;
    private DateTimeOffset _lastTickUtc = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastInGameUtc;
    private DateTimeOffset _lastPersistUtc = DateTimeOffset.UtcNow;
    private bool _tripActive;
    private bool _pendingTripStart;
    private double _pendingDrivingSeconds;
    private double _pendingDistanceKm;
    private double _pendingHighestSpeedKph;
    private VehicleTelemetry? _latestTelemetry;
    private bool _disposed;

    public DriverStatisticsService(Func<VehicleTelemetry?> telemetryProvider)
    {
        _telemetryProvider = telemetryProvider;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1d)
        };
        _timer.Tick += Timer_Tick;
    }

    public void Start()
    {
        _lastTickUtc = DateTimeOffset.UtcNow;
        _lastPersistUtc = _lastTickUtc;
        _timer.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        Flush(DateTimeOffset.UtcNow);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        var elapsedSeconds = Math.Clamp((now - _lastTickUtc).TotalSeconds, 0d, 5d);
        _lastTickUtc = now;

        var telemetry = _telemetryProvider();
        if (telemetry is null || !telemetry.IsInGame)
        {
            if (_lastInGameUtc is not null && now - _lastInGameUtc > TimeSpan.FromSeconds(90d))
            {
                _tripActive = false;
            }

            if (now - _lastPersistUtc >= TimeSpan.FromSeconds(15d))
            {
                Flush(now);
            }
            return;
        }

        _latestTelemetry = telemetry;
        _lastInGameUtc = now;
        var speed = double.IsFinite(telemetry.SpeedKph)
            ? Math.Clamp(telemetry.SpeedKph, 0d, 220d)
            : 0d;

        if (!_tripActive)
        {
            _tripActive = true;
            _pendingTripStart = true;
        }

        if (speed >= 0.5d && elapsedSeconds > 0d)
        {
            _pendingDrivingSeconds += elapsedSeconds;
            _pendingDistanceKm += speed * (elapsedSeconds / 3600d);
        }
        _pendingHighestSpeedKph = Math.Max(_pendingHighestSpeedKph, speed);

        if (_pendingTripStart || now - _lastPersistUtc >= TimeSpan.FromSeconds(15d))
        {
            Flush(now);
        }
    }

    private void Flush(DateTimeOffset now)
    {
        if (!_pendingTripStart &&
            _pendingDrivingSeconds <= 0d &&
            _pendingDistanceKm <= 0d &&
            _pendingHighestSpeedKph <= 0d &&
            _latestTelemetry is null)
        {
            _lastPersistUtc = now;
            return;
        }

        var tripIncrement = _pendingTripStart ? 1 : 0;
        var drivingSeconds = _pendingDrivingSeconds;
        var distanceKm = _pendingDistanceKm;
        var highestSpeed = _pendingHighestSpeedKph;
        var telemetry = _latestTelemetry;

        _pendingTripStart = false;
        _pendingDrivingSeconds = 0d;
        _pendingDistanceKm = 0d;
        _pendingHighestSpeedKph = 0d;
        _lastPersistUtc = now;

        DriverProfileStore.Update(profile => profile with
        {
            TotalDrivingSeconds = profile.TotalDrivingSeconds + drivingSeconds,
            TotalDistanceKm = profile.TotalDistanceKm + distanceKm,
            Trips = profile.Trips + tripIncrement,
            HighestSpeedKph = Math.Max(profile.HighestSpeedKph, highestSpeed),
            LastMap = string.IsNullOrWhiteSpace(telemetry?.MapName) ? profile.LastMap : telemetry.MapName,
            LastLine = string.IsNullOrWhiteSpace(telemetry?.Line) ? profile.LastLine : telemetry.Line,
            LastRoute = string.IsNullOrWhiteSpace(telemetry?.Route) ? profile.LastRoute : telemetry.Route,
            LastDrivenAt = telemetry is null ? profile.LastDrivenAt : now
        });
    }
}
