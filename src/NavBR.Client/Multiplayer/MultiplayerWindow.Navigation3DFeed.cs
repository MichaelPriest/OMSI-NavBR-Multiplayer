using Microsoft.AspNetCore.SignalR.Client;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private bool _navigation3DFeedInitialized;

    private void InitializeNavigation3DFeed()
    {
        if (_navigation3DFeedInitialized)
        {
            return;
        }

        _navigation3DFeedInitialized = true;
        _client.TelemetryReceived += Navigation3DSessionFeed.Update;
        _client.PlayerLeft += Navigation3DSessionFeed.Remove;
        _client.ConnectionStateChanged += state =>
        {
            if (state == HubConnectionState.Disconnected)
            {
                Navigation3DSessionFeed.Clear();
            }
        };

        Closed += (_, _) => Navigation3DSessionFeed.Clear();
    }
}
