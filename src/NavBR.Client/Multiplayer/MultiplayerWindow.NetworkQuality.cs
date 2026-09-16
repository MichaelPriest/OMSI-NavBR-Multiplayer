using Microsoft.AspNetCore.SignalR.Client;
using NavBR.Client.Diagnostics;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private readonly SessionNetworkQualityMonitor _networkQuality = new();
    private bool _networkQualityHooked;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
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
        _ = Dispatcher.BeginInvoke(() =>
        {
            _publishTimer.Interval = snapshot.Level switch
            {
                SessionNetworkQualityLevel.Poor => TimeSpan.FromMilliseconds(650d),
                SessionNetworkQualityLevel.Degraded => TimeSpan.FromMilliseconds(400d),
                _ => TimeSpan.FromMilliseconds(250d)
            };
        });

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
