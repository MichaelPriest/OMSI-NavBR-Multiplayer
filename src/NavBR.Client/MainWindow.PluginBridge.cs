using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using NavBR.Client.PluginInstaller;

namespace NavBR.Client;

public partial class MainWindow
{
    private DispatcherTimer? _pluginDiagnosticsTimer;
    private TextBlock? _pluginDiagnosticsStatusText;
    private Button? _pluginInstallButton;
    private Button? _pluginRemoveButton;
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

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 9, 0, 4)
        };

        _pluginInstallButton = new Button
        {
            MinWidth = 170,
            Margin = new Thickness(0, 0, 8, 0)
        };
        _pluginInstallButton.Click += PluginInstallButton_Click;
        actions.Children.Add(_pluginInstallButton);

        _pluginRemoveButton = new Button
        {
            MinWidth = 130
        };
        _pluginRemoveButton.Click += PluginRemoveButton_Click;
        actions.Children.Add(_pluginRemoveButton);
        parent.Children.Add(actions);

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
        RefreshPluginActionText();
        RenderPluginBridgeDiagnostics();
    }

    private void PluginDiagnosticsTimer_Tick(object? sender, EventArgs e)
    {
        RefreshPluginActionText();
        RenderPluginBridgeDiagnostics();
    }

    private void RefreshPluginActionText()
    {
        var language = Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName;

        if (_pluginInstallButton is not null)
        {
            _pluginInstallButton.Content = language switch
            {
                "pt" => "Instalar / atualizar plugin",
                "es" => "Instalar / actualizar plugin",
                "de" => "Plugin installieren / aktualisieren",
                "fr" => "Installer / mettre à jour le plugin",
                _ => "Install / update plugin"
            };
            _pluginInstallButton.IsEnabled = OmsiPluginInstallationService.HasEmbeddedPackage;
        }

        if (_pluginRemoveButton is not null)
        {
            _pluginRemoveButton.Content = language switch
            {
                "pt" => "Remover plugin",
                "es" => "Eliminar plugin",
                "de" => "Plugin entfernen",
                "fr" => "Supprimer le plugin",
                _ => "Remove plugin"
            };
        }
    }

    private void PluginInstallButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var root = ResolveOmsiRootForPluginAction();
            if (root is null)
            {
                return;
            }

            var result = OmsiPluginInstallationService.InstallOrUpdate(root);
            MessageBox.Show(
                $"Plugin NavBR Native AOT instalado/atualizado com sucesso.\n\nOMSI: {result.OmsiRoot}\nDestino: {result.PluginsDirectory}\nArquivos: {result.InstalledFiles}\n\nNão é necessário instalar .NET Runtime x86 separadamente.",
                "OMSI NavBR Multiplayer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            RenderPluginBridgeDiagnostics();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "OMSI NavBR Multiplayer",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void PluginRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var root = ResolveOmsiRootForPluginAction();
            if (root is null)
            {
                return;
            }

            var result = OmsiPluginInstallationService.Remove(root);
            MessageBox.Show(
                $"Plugin NavBR removido.\n\nOMSI: {result.OmsiRoot}\nArquivos removidos: {result.RemovedFiles}",
                "OMSI NavBR Multiplayer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            RenderPluginBridgeDiagnostics();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "OMSI NavBR Multiplayer",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private string? ResolveOmsiRootForPluginAction()
    {
        var detected = OmsiPluginInstallationService.ResolveOmsiRoot(_currentOmsi?.InstallDirectory);
        if (!string.IsNullOrWhiteSpace(detected))
        {
            return detected;
        }

        var picker = new OpenFolderDialog
        {
            Title = "Selecione a pasta que contém Omsi.exe",
            Multiselect = false
        };

        if (picker.ShowDialog(this) != true)
        {
            return null;
        }

        detected = OmsiPluginInstallationService.ResolveOmsiRoot(picker.FolderName);
        if (!string.IsNullOrWhiteSpace(detected))
        {
            return detected;
        }

        MessageBox.Show(
            "Omsi.exe não foi encontrado na pasta selecionada.",
            "OMSI NavBR Multiplayer",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        return null;
    }

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
        var install = GetPluginInstallDiagnostics();

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

        var heartbeat = bridge.LastStatus;
        var heartbeatAge = heartbeat?.TimestampUnixMilliseconds is long heartbeatMs
            ? Math.Max(
                0d,
                (DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(heartbeatMs)).TotalSeconds)
            : (double?)null;
        var heartbeatState = heartbeatAge is null
            ? "NONE"
            : heartbeatAge <= 12d
                ? "LIVE"
                : "STALE";

        var callbackCount = heartbeat?.SystemVariableCallbacks?.ToString() ?? "-";
        var systemVariable = heartbeat?.LastSystemVariableIndex?.ToString() ?? "-";
        var remoteCount = heartbeat?.RemoteVehicleCount?.ToString() ?? "-";
        var compatibleCount = heartbeat?.CompatibleRemoteVehicleCount?.ToString() ?? "-";
        var staleRemoved = heartbeat?.StaleRemovedCount?.ToString() ?? "-";
        var heartbeatAgeText = heartbeatAge is double seconds
            ? $"{seconds:F1}s"
            : "-";

        _pluginDiagnosticsStatusText.Text =
            $"package={(OmsiPluginInstallationService.HasEmbeddedPackage ? "EMBEDDED" : "MISSING")}  deployment=NATIVE-AOT-X86  install={install.State}  files={install.RequiredFilesFound}/2  manifest={install.Manifest}\n" +
            $"runtime=BUILT-IN  status={status}  protocol=v1\n" +
            $"plugin-pid={pluginPid}  process-match={processMatch}  version={pluginVersion}\n" +
            $"connected-since={since}\n" +
            $"heartbeat={heartbeatState}  age={heartbeatAgeText}  callbacks={callbackCount}  system-var={systemVariable}\n" +
            $"remote={remoteCount}  compatible={compatibleCount}  stale-removed={staleRemoved}\n" +
            $"map={mapName}\n" +
            $"compatibility={compatibility}\n" +
            $"plugin-dir={install.DisplayPath}\n" +
            "log=%LOCALAPPDATA%\\OMSI NavBR Multiplayer\\navbr-plugin.log";

        var healthy = bridge.IsConnected &&
                      heartbeatState != "STALE" &&
                      install.State is "INSTALLED" or "UNTRACKED";

        _pluginDiagnosticsStatusText.Foreground = healthy
            ? TryFindResource("NavAccentBrush") as Brush ?? Brushes.LightGreen
            : TryFindResource("NavMutedBrush") as Brush ?? Brushes.LightGray;
    }

    private PluginInstallDiagnostics GetPluginInstallDiagnostics()
    {
        var installDirectory = _currentOmsi?.InstallDirectory ??
                               OmsiPluginInstallationService.ResolveOmsiRoot();
        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            return new PluginInstallDiagnostics("UNKNOWN", 0, "UNKNOWN", "-");
        }

        try
        {
            var pluginsDirectory = Path.Combine(installDirectory, "plugins");
            var requiredFiles = new[]
            {
                "NavBR.OmsiPlugin.dll",
                "NavBR.OmsiPlugin.opl"
            };

            var found = requiredFiles.Count(file =>
                File.Exists(Path.Combine(pluginsDirectory, file)));
            var manifestPath = Path.Combine(
                pluginsDirectory,
                "NavBR.OmsiPlugin.install-manifest.txt");
            var hasManifest = File.Exists(manifestPath);

            var state = found switch
            {
                0 => "MISSING",
                2 when hasManifest => "INSTALLED",
                2 => "UNTRACKED",
                _ => "PARTIAL"
            };

            return new PluginInstallDiagnostics(
                state,
                found,
                hasManifest ? "YES" : "NO",
                pluginsDirectory);
        }
        catch
        {
            return new PluginInstallDiagnostics("ERROR", 0, "UNKNOWN", "-");
        }
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

    private sealed record PluginInstallDiagnostics(
        string State,
        int RequiredFilesFound,
        string Manifest,
        string DisplayPath);
}
