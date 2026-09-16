using System.Windows.Threading;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Driver;

internal sealed class DriverStatisticsService : IDisposable
{
    private readonly Func<VehicleTelemetry?> _telemetryProvider;
    private readonly DispatcherTimer _timer;
    private DateTimeOffset _lastTickUtc = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastInGameUtc;
    private DateTimeOffset _lastPersistUtc = DateTimeOffset.MinValue;
    private bool _tripActive;
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
            return;
        }

        _lastInGameUtc = now;
        var speed = double.IsFinite(telemetry.SpeedKph)
            ? Math.Clamp(telemetry.SpeedKph, 0d, 220d)
            : 0d;

        var startTrip = !_tripActive;
        if (startTrip)
        {
            _tripActive = true;
        }

        var moving = speed >= 0.5d && elapsedSeconds > 0d;
        var distanceKm = moving ? speed * (elapsedSeconds / 3600d) : 0d;
        var shouldPersist = startTrip || moving || now - _lastPersistUtc >= TimeSpan.FromSeconds(20d);
        if (!shouldPersist)
        {
            return;
        }

        DriverProfileStore.Update(profile => profile with
        {
            TotalDrivingSeconds = profile.TotalDrivingSeconds + (moving ? elapsedSeconds : 0d),
            TotalDistanceKm = profile.TotalDistanceKm + distanceKm,
            Trips = profile.Trips + (startTrip ? 1 : 0),
            HighestSpeedKph = Math.Max(profile.HighestSpeedKph, speed),
            LastMap = string.IsNullOrWhiteSpace(telemetry.MapName) ? profile.LastMap : telemetry.MapName,
            LastLine = string.IsNullOrWhiteSpace(telemetry.Line) ? profile.LastLine : telemetry.Line,
            LastRoute = string.IsNullOrWhiteSpace(telemetry.Route) ? profile.LastRoute : telemetry.Route,
            LastDrivenAt = now
        });
        _lastPersistUtc = now;
    }
}
