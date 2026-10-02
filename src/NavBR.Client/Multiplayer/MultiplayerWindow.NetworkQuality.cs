using Microsoft.AspNetCore.SignalR.Client;
using NavBR.Client.Diagnostics;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private readonly SessionNetworkQualityMonitor _networkQuality = new();
    private bool _networkQualityHooked;

    private void InitializeNetworkQualityLifecycle()
    {
        if (_networkQualityHooked)
        {
            return;
        }

        _networkQualityHooked = true;
        _networkQuality.QualityChanged += NetworkQuality_Changed;
        _client.ConnectionStateChanged += NetworkQuality_ConnectionStateChanged;
        Closed += NetworkQuality_WindowClosed;

        if (_client.IsConnected)
        {
            _networkQuality.Start(_settings.ServerUrl);
        }
    }

    private void NetworkQuality_ConnectionStateChanged(HubConnectionState state)
    {
        if (state == HubConnectionState.Connected)
        {
            _networkQuality.Start(_settings.ServerUrl);
        }
        else if (state is HubConnectionState.Disconnected or HubConnectionState.Reconnecting)
        {
            _networkQuality.Stop();
            Dispatcher.BeginInvoke(() => _publishTimer.Interval = TimeSpan.FromMilliseconds(250d));
        }
    }

    private void NetworkQuality_Changed(SessionNetworkQualitySnapshot snapshot)
    {
        // Publish cadence has one owner: UpdatePublishTimerCadence combines
        // motion, network quality and OMSI callback pressure. Mutating the
        // DispatcherTimer independently here made the two governors overwrite
        // each other every tick.
        _ = Dispatcher.BeginInvoke(UpdatePublishTimerCadence);

        if (snapshot.Samples >= 2)
        {
            RemoteDiagnosticsService.Record(
                "network-quality",
                snapshot.Level == SessionNetworkQualityLevel.Poor ? "warning" : "info",
                $"level={snapshot.Level};rttMs={snapshot.RoundTripMs:F1};jitterMs={snapshot.JitterMs:F1};loss={snapshot.LossPercent:F1};samples={snapshot.Samples}");
        }
    }

    private async void NetworkQuality_WindowClosed(object? sender, EventArgs e)
    {
        _networkQuality.QualityChanged -= NetworkQuality_Changed;
        _client.ConnectionStateChanged -= NetworkQuality_ConnectionStateChanged;
        await _networkQuality.DisposeAsync();
    }
}
