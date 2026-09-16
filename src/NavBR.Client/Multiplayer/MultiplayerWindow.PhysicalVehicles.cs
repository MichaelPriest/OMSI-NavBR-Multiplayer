using System.Windows;
using System.Windows.Controls;
using Microsoft.AspNetCore.SignalR.Client;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private readonly RemotePhysicalVehicleCoordinator _physicalVehicles = new();
    private CheckBox? _physicalVehiclesCheckBox;
    private bool _physicalVehiclesUiInstalled;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        EnsurePhysicalVehiclesUi();
        HookPhysicalVehicleLifecycle();
    }

    private void EnsurePhysicalVehiclesUi()
    {
        if (_physicalVehiclesUiInstalled)
        {
            return;
        }

        _physicalVehiclesUiInstalled = true;

        // Persisted user consent is the public-test opt-in consumed by both the
        // desktop client and the Native AOT plugin running inside Omsi.exe.
        ExperimentalFeatureFlags.SetPhysicalVehiclesEnabled(
            _settings.ExperimentalPhysicalVehiclesEnabled);

        if (VoiceEnabledCheckBox.Parent is not Grid parent)
        {
            return;
        }

        parent.Children.Remove(VoiceEnabledCheckBox);

        var options = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 11)
        };
        Grid.SetRow(options, 2);
        Grid.SetColumn(options, 2);
        Grid.SetColumnSpan(options, 2);

        VoiceEnabledCheckBox.Margin = new Thickness(0, 0, 0, 6);
        options.Children.Add(VoiceEnabledCheckBox);

        _physicalVehiclesCheckBox = new CheckBox
        {
            IsChecked = _settings.ExperimentalPhysicalVehiclesEnabled,
            Content = PhysicalVehiclesLabel(),
            ToolTip = PhysicalVehiclesWarning(),
            FontWeight = FontWeights.SemiBold
        };
        _physicalVehiclesCheckBox.Click += PhysicalVehiclesCheckBox_Click;
        options.Children.Add(_physicalVehiclesCheckBox);

        parent.Children.Add(options);
    }

    private void HookPhysicalVehicleLifecycle()
    {
        // OnContentRendered may run again after a visual rebuild. Event wiring
        // is guarded by the same flag used for UI installation.
        if (_physicalVehiclesCheckBox?.Tag is "hooked")
        {
            return;
        }

        if (_physicalVehiclesCheckBox is not null)
        {
            _physicalVehiclesCheckBox.Tag = "hooked";
        }

        _client.TelemetryReceived += frame => Dispatcher.BeginInvoke(() =>
        {
            _physicalVehicles.SetLocalManifest(
                OmsiCompatibilityManifestFactory.Create(
                    _telemetrySource(),
                    _activeMapSource()));
            _ = _physicalVehicles.ApplyAsync(frame);
        });

        _client.PlayerLeft += playerId => Dispatcher.BeginInvoke(() =>
            _ = _physicalVehicles.DespawnAsync(playerId));

        _client.ConnectionStateChanged += state =>
        {
            if (state == HubConnectionState.Disconnected)
            {
                _ = Dispatcher.BeginInvoke(() => _ = _physicalVehicles.ClearAsync());
            }
        };

        Closed += (_, _) => _ = _physicalVehicles.ClearAsync();
    }

    private async void PhysicalVehiclesCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_physicalVehiclesCheckBox is null)
        {
            return;
        }

        var enabled = _physicalVehiclesCheckBox.IsChecked == true;
        _settings = _settings with { ExperimentalPhysicalVehiclesEnabled = enabled };
        MultiplayerSettingsStore.Save(_settings);
        ExperimentalFeatureFlags.SetPhysicalVehiclesEnabled(enabled);

        if (!enabled)
        {
            // Despawn is deliberately accepted by the plugin even after the
            // opt-in flag is removed, so disabling this switch always cleans up.
            await _physicalVehicles.ClearAsync();
        }

        StatusDetailText.Text = enabled
            ? PhysicalVehiclesEnabledMessage()
            : PhysicalVehiclesDisabledMessage();
    }

    private static string PhysicalVehiclesLabel() =>
        Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Ônibus remoto 3D (EXPERIMENTAL)",
            "es" => "Autobús remoto 3D (EXPERIMENTAL)",
            "de" => "Entfernter 3D-Bus (EXPERIMENTELL)",
            "fr" => "Bus distant 3D (EXPÉRIMENTAL)",
            _ => "Remote 3D bus (EXPERIMENTAL)"
        };

    private static string PhysicalVehiclesWarning() =>
        Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Teste público da Alpha.11 Test 2. Requer OMSI 2.3.004, plugin NavBR instalado e o mesmo veículo disponível localmente. Pode causar instabilidade; desligue se houver travamentos.",
            "es" => "Prueba pública Alpha.11 Test 2. Requiere OMSI 2.3.004, el plugin NavBR y el mismo vehículo instalado localmente. Puede ser inestable.",
            "de" => "Öffentlicher Alpha.11-Test. Erfordert OMSI 2.3.004, das NavBR-Plugin und dasselbe lokal installierte Fahrzeug. Kann instabil sein.",
            "fr" => "Test public Alpha.11. Nécessite OMSI 2.3.004, le plugin NavBR et le même véhicule installé localement. Peut être instable.",
            _ => "Alpha.11 Test 2 public test. Requires OMSI 2.3.004, the NavBR plugin and the same vehicle installed locally. May be unstable."
        };

    private static string PhysicalVehiclesEnabledMessage() =>
        Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Ônibus remoto 3D experimental ativado. Jogadores compatíveis poderão aparecer fisicamente no OMSI.",
            "es" => "Autobús remoto 3D experimental activado.",
            "de" => "Experimenteller entfernter 3D-Bus aktiviert.",
            "fr" => "Bus distant 3D expérimental activé.",
            _ => "Experimental remote 3D bus enabled."
        };

    private static string PhysicalVehiclesDisabledMessage() =>
        Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Ônibus remoto 3D experimental desativado.",
            "es" => "Autobús remoto 3D experimental desactivado.",
            "de" => "Experimenteller entfernter 3D-Bus deaktiviert.",
            "fr" => "Bus distant 3D expérimental désactivé.",
            _ => "Experimental remote 3D bus disabled."
        };
}
