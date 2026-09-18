using NavBR.Client.Maps;

namespace NavBR.Client;

public partial class MainWindow
{
    internal void OpenNavigation3D() =>
        NavigatePrimaryWebShell("navigation-3d");
}
