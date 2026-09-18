namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    /// <summary>
    /// Read-only state consumed by the Figma Home. Values come from the live
    /// room snapshot/presence collection; no synthetic player count is used.
    /// </summary>
    internal int CurrentPlayerCountForShell => _players.Count;
    internal bool IsHostingRoomForShell => _host.IsRunning;
}
