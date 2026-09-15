using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace NavBR.Client;

public partial class MainWindow
{
    private DispatcherTimer? _pluginDiagnosticsTimer;
    private TextBlock? _pluginDiagnosticsStatusText;
    private bool _pluginDiagnosticsUiCreated;

    internal string? GetCurrentMapCompatibilityIdForPlugin() =>
        GetActiveMapForMultiplayer()?.CompatibilityId;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        EnsurePluginBridgeDiagnostics();
    }

    private void EnsurePluginBridgeDiagnostics()
    {
        if (_pluginDiagnosticsUiCreated || ProcessDetailsText.Parent is not StackPanel parent)
        {
            return;
        }

        _pluginDiagnosticsUiCreated = true;

        parent.Children.Add(new Border
        {
            Height = 1,
            Margin = new Thickness(0, 14, 0, 14),
            Background = TryFindResource("NavBorderBrush") as Brush ?? Brushes.DimGray
        });

        parent.Children.Add(new TextBlock
        {
            Text = "PLUGIN BRIDGE v1 • EXP",
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = TryFindResource("NavTextBrush") as Brush ?? Brushes.White
        });

        _pluginDiagnosticsStatusText = new TextBlock
        {
            Margin = new Thickness(0, 6, 0, 0),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Foreground = TryFindResource("NavMutedBrush") as Brush ?? Brushes.LightGray,
            TextWrapping = TextWrapping.Wrap
        };
        parent.Children.Add(_pluginDiagnosticsStatusText);

        _pluginDiagnosticsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _pluginDiagnosticsTimer.Tick += PluginDiagnosticsTimer_Tick;
        _pluginDiagnosticsTimer.Start();

        Closed += PluginDiagnosticsWindow_Closed;
        RenderPluginBridgeDiagnostics();
    }

    private void PluginDiagnosticsTimer_Tick(object? sender, EventArgs e) =>
        RenderPluginBridgeDiagnostics();

    private void PluginDiagnosticsWindow_Closed(object? sender, EventArgs e)
    {
        if (_pluginDiagnosticsTimer is not null)
        {
            _pluginDiagnosticsTimer.Stop();
            _pluginDiagnosticsTimer.Tick -= PluginDiagnosticsTimer_Tick;
            _pluginDiagnosticsTimer = null;
        }

        Closed -= PluginDiagnosticsWindow_Closed;
    }

    private void RenderPluginBridgeDiagnostics()
    {
        if (_pluginDiagnosticsStatusText is null || Application.Current is not App app)
        {
            return;
        }

        var bridge = app.PluginBridge.GetConnectionInfo();
        var activeMap = GetActiveMapForMultiplayer();
        var mapName = activeMap?.FolderName ?? _lastTelemetry?.MapName ?? "-";
        var compatibility = ShortFingerprint(activeMap?.CompatibilityId);

        var processMatch = bridge.PluginProcessId is null || _currentOmsi?.ProcessId is null
            ? "UNKNOWN"
            : bridge.PluginProcessId == _currentOmsi.ProcessId
                ? "YES"
                : "NO";

        var status = bridge.IsConnected ? "CONNECTED" : "WAITING";
        var pluginPid = bridge.PluginProcessId?.ToString() ?? "-";
        var pluginVersion = string.IsNullOrWhiteSpace(bridge.PluginComponentVersion)
            ? "-"
            : bridge.PluginComponentVersion;
        var since = bridge.ConnectedAtUtc is DateTimeOffset connectedAt
            ? connectedAt.ToLocalTime().ToString("HH:mm:ss")
            : "-";

        _pluginDiagnosticsStatusText.Text =
            $"status={status}  protocol=v1\n" +
            $"plugin-pid={pluginPid}  process-match={processMatch}  version={pluginVersion}\n" +
            $"connected-since={since}\n" +
            $"map={mapName}\n" +
            $"compatibility={compatibility}\n" +
            "log=%LOCALAPPDATA%\\OMSI NavBR Multiplayer\\navbr-plugin.log";

        _pluginDiagnosticsStatusText.Foreground = bridge.IsConnected
            ? TryFindResource("NavAccentBrush") as Brush ?? Brushes.LightGreen
            : TryFindResource("NavMutedBrush") as Brush ?? Brushes.LightGray;
    }

    private static string ShortFingerprint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var normalized = value.Trim();
        return normalized.Length <= 20
            ? normalized
            : $"{normalized[..12]}…{normalized[^6..]}";
    }
}
