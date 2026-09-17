using NavBR.Client.Maps;

namespace NavBR.Client;

public partial class MainWindow
{
    /// <summary>
    /// Read-only operations accessor so CCO surfaces use the same authoritative
    /// active-map resolution as HUD and multiplayer.
    /// </summary>
    internal OmsiMapInfo? GetActiveMapForOperations() => GetActiveMapForMultiplayer();
}
