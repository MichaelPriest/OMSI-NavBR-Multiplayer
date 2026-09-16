using System.Windows;
using System.Windows.Threading;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private readonly DispatcherTimer _trafficPublishTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(500)
    };

    private bool _trafficSyncInitialized;
    private bool _trafficPublishing;
    private long _trafficSequence;

    static MultiplayerWindow()
    {
        EventManager.RegisterClassHandler(
            typeof(MultiplayerWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnTrafficWindowLoaded));
    }

    private static void OnTrafficWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MultiplayerWindow window)
        {
            window.InitializeTrafficSync();
        }
    }

    private void InitializeTrafficSync()
    {
        if (_trafficSyncInitialized)
        {
            return;
        }

        _trafficSyncInitialized = true;
        _trafficPublishTimer.Tick += TrafficPublishTimer_Tick;

        _client.TrafficAuthorityChanged += _ =>
            Dispatcher.BeginInvoke(UpdateTrafficPublisherState);

        _client.TrafficSnapshotReceived += snapshot =>
            Dispatcher.BeginInvoke(() => ApplyRemoteTrafficSnapshot(snapshot));

        _client.ConnectionStateChanged += _ =>
            Dispatcher.BeginInvoke(UpdateTrafficPublisherState);

        Closed += (_, _) =>
        {
            _trafficPublishTimer.Stop();
            _ = OmsiTrafficBridgeRelay.ClearTrafficAsync();
        };

        UpdateTrafficPublisherState();
    }

    private void UpdateTrafficPublisherState()
    {
        if (_client.IsConnected && _client.IsTrafficAuthority)
        {
            if (!_trafficPublishTimer.IsEnabled)
            {
                _trafficPublishTimer.Start();
            }
        }
        else
        {
            _trafficPublishTimer.Stop();
        }
    }

    private async void TrafficPublishTimer_Tick(object? sender, EventArgs e)
    {
        if (_trafficPublishing || !_client.IsConnected || !_client.IsTrafficAuthority)
        {
            return;
        }

        if (Owner is not MainWindow mainWindow)
        {
            return;
        }

        var telemetry = _telemetrySource();
        if (telemetry is null || !telemetry.IsInGame)
        {
            return;
        }

        var activeMap = _activeMapSource();
        var vehicles = mainWindow.GetRoadTrafficForMultiplayer();

        _trafficPublishing = true;
        try
        {
            var snapshot = new TrafficSnapshot(
                _settings.PlayerId,
                Interlocked.Increment(ref _trafficSequence),
                DateTimeOffset.UtcNow,
                telemetry.MapName,
                activeMap?.CompatibilityId ?? telemetry.MapCompatibilityId,
                vehicles);

            await _client.PublishTrafficSnapshotAsync(snapshot);
        }
        catch
        {
            // Ordinary player telemetry remains authoritative for room health.
            // A traffic publish failure is allowed to recover on the next tick.
        }
        finally
        {
            _trafficPublishing = false;
        }
    }

    private void ApplyRemoteTrafficSnapshot(TrafficSnapshot snapshot)
    {
        if (_client.IsTrafficAuthority)
        {
            return;
        }

        var localTelemetry = _telemetrySource();
        var localMap = _activeMapSource();
        if (!TrafficMapMatches(
                localTelemetry?.MapName,
                localMap?.CompatibilityId ?? localTelemetry?.MapCompatibilityId,
                snapshot.MapName,
                snapshot.MapCompatibilityId))
        {
            _ = OmsiTrafficBridgeRelay.ClearTrafficAsync();
            return;
        }

        _ = OmsiTrafficBridgeRelay.ForwardTrafficSnapshotAsync(snapshot);
    }

    private static bool TrafficMapMatches(
        string? localMapName,
        string? localCompatibilityId,
        string? remoteMapName,
        string? remoteCompatibilityId)
    {
        if (!string.IsNullOrWhiteSpace(localCompatibilityId) &&
            !string.IsNullOrWhiteSpace(remoteCompatibilityId))
        {
            return string.Equals(
                localCompatibilityId,
                remoteCompatibilityId,
                StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(localMapName) &&
               !string.IsNullOrWhiteSpace(remoteMapName) &&
               string.Equals(
                   localMapName,
                   remoteMapName,
                   StringComparison.OrdinalIgnoreCase);
    }
}
