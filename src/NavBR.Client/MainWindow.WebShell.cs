using System.Windows;

namespace NavBR.Client;

public partial class MainWindow
{
    private WebShellWindow? _webShellWindow;

    private void WebShellButton_Click(object sender, RoutedEventArgs e)
    {
        if (_webShellWindow is { IsLoaded: true })
        {
            _webShellWindow.Activate();
            return;
        }

        _webShellWindow = new WebShellWindow(BuildWebShellState, LaunchOmsiForShell)
        {
            Owner = this
        };
        _webShellWindow.Closed += (_, _) => _webShellWindow = null;
        _webShellWindow.Show();
    }

    private object BuildWebShellState()
    {
        var telemetry = _lastTelemetry;
        var omsi = _currentOmsi;

        return new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            appVersion = typeof(MainWindow).Assembly.GetName().Version?.ToString(),
            omsi = new
            {
                running = omsi is not null,
                processId = omsi?.ProcessId,
                version = omsi?.FileVersion,
                installDirectory = omsi?.InstallDirectory,
                compatible = omsi?.IsOmsi23004 ?? false
            },
            telemetry = telemetry is null
                ? null
                : new
                {
                    inGame = telemetry.IsInGame,
                    mapName = telemetry.MapName,
                    x = telemetry.X,
                    y = telemetry.Y,
                    z = telemetry.Z,
                    headingDegrees = telemetry.HeadingDegrees,
                    speedKph = telemetry.SpeedKph
                }
        };
    }
}
