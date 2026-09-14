using Microsoft.AspNetCore.SignalR;
using NavBR.Shared.Telemetry;

namespace NavBR.Server.Hubs;

public sealed class MultiplayerHub : Hub
{
    public Task JoinRoom(string roomId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, NormalizeRoom(roomId));
    }

    public Task LeaveRoom(string roomId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, NormalizeRoom(roomId));
    }

    public Task PublishTelemetry(string roomId, VehicleTelemetry telemetry)
    {
        return Clients
            .OthersInGroup(NormalizeRoom(roomId))
            .SendAsync("telemetry", telemetry);
    }

    private static string NormalizeRoom(string roomId)
    {
        var value = (roomId ?? string.Empty).Trim();
        if (value.Length is < 1 or > 64)
        {
            throw new HubException("Invalid room id.");
        }

        return value;
    }
}
