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
    private DateTimeOffset? _tripStartedAtUtc;
    private double _tripDrivingSeconds;
    private double _tripDistanceKm;
    private double _tripHighestSpeedKph;
    private string? _tripMap;
    private string? _tripLine;
    private string? _tripRoute;
    private string? _tripVehicle;
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
        var now = DateTimeOffset.UtcNow;
        Flush(now);
        if (_tripActive)
        {
            EndTrip(_lastInGameUtc ?? now);
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        var elapsedSeconds = Math.Clamp((now - _lastTickUtc).TotalSeconds, 0d, 5d);
        _lastTickUtc = now;

        var telemetry = _telemetryProvider();
        if (telemetry is null || !telemetry.IsInGame)
        {
            if (_tripActive && _lastInGameUtc is DateTimeOffset lastInGame && now - lastInGame > TimeSpan.FromSeconds(90d))
            {
                EndTrip(lastInGame);
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
            StartTrip(now, telemetry);
        }
        else
        {
            UpdateTripMetadata(telemetry);
        }

        if (speed >= 0.5d && elapsedSeconds > 0d)
        {
            var distanceKm = speed * (elapsedSeconds / 3600d);
            _pendingDrivingSeconds += elapsedSeconds;
            _pendingDistanceKm += distanceKm;
            _tripDrivingSeconds += elapsedSeconds;
            _tripDistanceKm += distanceKm;
        }
        _pendingHighestSpeedKph = Math.Max(_pendingHighestSpeedKph, speed);
        _tripHighestSpeedKph = Math.Max(_tripHighestSpeedKph, speed);

        if (_pendingTripStart || now - _lastPersistUtc >= TimeSpan.FromSeconds(15d))
        {
            Flush(now);
        }
    }

    private void StartTrip(DateTimeOffset now, VehicleTelemetry telemetry)
    {
        _tripActive = true;
        _pendingTripStart = true;
        _tripStartedAtUtc = now;
        _tripDrivingSeconds = 0d;
        _tripDistanceKm = 0d;
        _tripHighestSpeedKph = 0d;
        _tripMap = null;
        _tripLine = null;
        _tripRoute = null;
        _tripVehicle = null;
        UpdateTripMetadata(telemetry);
    }

    private void UpdateTripMetadata(VehicleTelemetry telemetry)
    {
        _tripMap = Prefer(telemetry.MapName, _tripMap);
        _tripLine = Prefer(telemetry.Line, _tripLine);
        _tripRoute = Prefer(telemetry.Route, _tripRoute);
        _tripVehicle = Prefer(telemetry.VehicleName, _tripVehicle);
    }

    private void EndTrip(DateTimeOffset endedAtUtc)
    {
        if (!_tripActive || _tripStartedAtUtc is not DateTimeOffset startedAtUtc)
        {
            _tripActive = false;
            return;
        }

        DriverTripHistoryStore.Append(new DriverTripHistoryEntry(
            startedAtUtc,
            endedAtUtc,
            _tripDrivingSeconds,
            _tripDistanceKm,
            _tripHighestSpeedKph,
            _tripMap,
            _tripLine,
            _tripRoute,
            _tripVehicle));

        _tripActive = false;
        _tripStartedAtUtc = null;
        _tripDrivingSeconds = 0d;
        _tripDistanceKm = 0d;
        _tripHighestSpeedKph = 0d;
        _tripMap = null;
        _tripLine = null;
        _tripRoute = null;
        _tripVehicle = null;
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

    private static string? Prefer(string? incoming, string? current) =>
        string.IsNullOrWhiteSpace(incoming) ? current : incoming.Trim();
}
