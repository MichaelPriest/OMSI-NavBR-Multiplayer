using Microsoft.AspNetCore.SignalR.Client;
using NavBR.Client.Diagnostics;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private bool _diagnosticsLifecycleHooked;

    private void HookDiagnosticsLifecycle()
    {
        if (_diagnosticsLifecycleHooked)
        {
            return;
        }

        _diagnosticsLifecycleHooked = true;

        // Keep the diagnostic context aligned with the local player's current
        // map/vehicle while multiplayer telemetry is being published.
        _publishTimer.Tick += (_, _) => RefreshDiagnosticsContext();

        _client.ConnectionStateChanged += state =>
        {
            var severity = state == HubConnectionState.Connected ? "info" : "warning";
            RemoteDiagnosticsService.Record(
                "multiplayer",
                severity,
                $"connection-state={state}");
        };

        _client.PlayerJoined += _ => RemoteDiagnosticsService.Record(
            "multiplayer",
            "info",
            "remote-player-joined");
        _client.PlayerLeft += _ => RemoteDiagnosticsService.Record(
            "multiplayer",
            "info",
            "remote-player-left");

        Closed += (_, _) => RemoteDiagnosticsService.Record(
            "multiplayer",
            "info",
            "window-closed");
    }
}
