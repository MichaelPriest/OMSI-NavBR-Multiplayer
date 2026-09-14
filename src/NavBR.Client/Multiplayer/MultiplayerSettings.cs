namespace NavBR.Client.Multiplayer;

public sealed record MultiplayerSettings(
    string PlayerId,
    string ServerUrl,
    string RoomId,
    string DisplayName)
{
    public static MultiplayerSettings CreateDefault() => new(
        Guid.NewGuid().ToString("N"),
        "http://localhost:5000/hubs/multiplayer",
        "public",
        "Driver");
}
