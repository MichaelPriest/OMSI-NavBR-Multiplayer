namespace NavBR.Client.Multiplayer;

public sealed record MultiplayerSettings(
    string PlayerId,
    string ServerUrl,
    string RoomId,
    string DisplayName,
    string ChatHotkey = "F9",
    string VoiceHotkey = "F10")
{
    public static MultiplayerSettings CreateDefault() => new(
        Guid.NewGuid().ToString("N"),
        "http://127.0.0.1:27730",
        $"navbr-{Random.Shared.Next(1000, 9999)}",
        "Driver",
        "F9",
        "F10");
}
