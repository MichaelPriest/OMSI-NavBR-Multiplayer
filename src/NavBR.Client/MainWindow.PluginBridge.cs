namespace NavBR.Client;

public partial class MainWindow
{
    internal string? GetCurrentMapCompatibilityIdForPlugin() =>
        GetActiveMapForMultiplayer()?.CompatibilityId;
}
