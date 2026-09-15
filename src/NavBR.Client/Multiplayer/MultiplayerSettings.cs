namespace NavBR.Client.Multiplayer;

public sealed record MultiplayerSettings(
    string PlayerId,
    string ServerUrl,
    string RoomId,
    string DisplayName,
    string ChatHotkey = "F9",
    string VoiceHotkey = "F10",
    double HudX = 0.02d,
    double HudY = 1.0d,
    double HudZoom = 1.0d,
    double HudMapOpacity = 0.58d,
    int DashboardSettingsVersion = 1,
    bool DashboardEnabled = true,
    double DashboardX = 0.02d,
    double DashboardY = 0.58d,
    double DashboardScale = 1.0d,
    double DashboardOpacity = 0.92d,
    bool DashboardShowFuel = true,
    bool DashboardShowPedals = true,
    bool DashboardShowStatus = true)
{
    public static MultiplayerSettings CreateDefault() => new(
        Guid.NewGuid().ToString("N"),
        "http://127.0.0.1:27730",
        $"navbr-{Random.Shared.Next(1000, 9999)}",
        "Driver",
        "F9",
        "F10",
        0.02d,
        1.0d,
        1.0d,
        0.58d,
        1,
        true,
        0.02d,
        0.58d,
        1.0d,
        0.92d,
        true,
        true,
        true);
}
