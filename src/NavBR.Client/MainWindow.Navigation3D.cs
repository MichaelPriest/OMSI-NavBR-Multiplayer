using NavBR.Client.Maps;

namespace NavBR.Client;

public partial class MainWindow
{
    private Navigation3DWindow? _navigation3DWindow;

    internal void OpenNavigation3D()
    {
        if (_navigation3DWindow is not null)
        {
            if (_navigation3DWindow.WindowState == System.Windows.WindowState.Minimized)
            {
                _navigation3DWindow.WindowState = System.Windows.WindowState.Normal;
            }

            _navigation3DWindow.Show();
            _navigation3DWindow.Activate();
            return;
        }

        var window = new Navigation3DWindow(
            () => _lastTelemetry,
            GetActiveMapForMultiplayer)
        {
            Owner = this
        };
        window.Closed += (_, _) => _navigation3DWindow = null;
        _navigation3DWindow = window;
        window.Show();
    }
}
