using System.Windows;
using System.Windows.Controls;
using NavBR.Client.Diagnostics;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private CheckBox? _physicalVehiclesCheckBox;
    private CheckBox? _diagnosticsCheckBox;
    private bool _physicalVehiclesUiInstalled;

    public bool IsRemotePhysicalVehicleSpawned(string playerId) =>
        _client.IsRemotePhysicalVehicleSpawned(playerId);

    private void InitializePhysicalVehiclesPublicTest()
    {
        EnsurePhysicalVehiclesUi();
        RefreshDiagnosticsContext();
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
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        };
        _physicalVehiclesCheckBox.Click += PhysicalVehiclesCheckBox_Click;
        options.Children.Add(_physicalVehiclesCheckBox);

        _diagnosticsCheckBox = new CheckBox
        {
            IsChecked = DiagnosticsConsentStore.IsEnabled,
            Content = DiagnosticsLabel(),
            ToolTip = DiagnosticsWarning(),
            FontWeight = FontWeights.SemiBold
        };
        _diagnosticsCheckBox.Click += DiagnosticsCheckBox_Click;
        options.Children.Add(_diagnosticsCheckBox);

        parent.Children.Add(options);
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
        RefreshDiagnosticsContext();
        RemoteDiagnosticsService.Record(
            "physical-vehicle",
            "info",
            enabled ? "experimental-3d-enabled" : "experimental-3d-disabled");

        if (!enabled)
        {
            // The online client owns the single physical-vehicle coordinator.
            // Despawn is deliberately accepted by the plugin after opt-out so
            // disabling this switch always removes NavBR-owned remote buses.
            await _client.ClearPhysicalVehiclesAsync();
            StatusDetailText.Text = PhysicalVehiclesDisabledMessage();
            return;
        }

        // No fake readiness: capability is reported by the actual OMSI plugin.
        // If OMSI/plugin is not ready yet, the feature remains armed and will
        // start spawning compatible remote buses on subsequent telemetry frames.
        StatusDetailText.Text = _client.IsPhysicalMultiplayerAvailable
            ? PhysicalVehiclesEnabledMessage()
            : PhysicalVehiclesWaitingMessage();
    }

    private void DiagnosticsCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_diagnosticsCheckBox is null)
        {
            return;
        }

        var enabled = _diagnosticsCheckBox.IsChecked == true;
        DiagnosticsConsentStore.SetEnabled(enabled);
        RemoteDiagnosticsService.OnConsentChanged(enabled);
        RefreshDiagnosticsContext();

        StatusDetailText.Text = enabled
            ? DiagnosticsEnabledMessage()
            : DiagnosticsDisabledMessage();
    }

    private void RefreshDiagnosticsContext()
    {
        var telemetry = _telemetrySource();
        RemoteDiagnosticsService.UpdateContext(
            omsiVersion: null,
            mapName: telemetry?.MapName ?? _activeMapSource()?.FolderName,
            vehicleId: telemetry?.VehiclePath ?? telemetry?.VehicleName,
            physicalVehiclesEnabled: ExperimentalFeatureFlags.PhysicalVehiclesEnabled);
    }

    private static string PhysicalVehiclesLabel() =>
        Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Ônibus dos jogadores no OMSI (TESTE ALPHA)",
            "es" => "Autobuses de jugadores en OMSI (PRUEBA ALPHA)",
            "de" => "Spielerbusse in OMSI (ALPHA-TEST)",
            "fr" => "Bus des joueurs dans OMSI (TEST ALPHA)",
            _ => "Player buses in OMSI (ALPHA TEST)"
        };

    private static string PhysicalVehiclesWarning() =>
        Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Teste Alpha.12 limitado. Mostra o ônibus remoto fisicamente usando posição, rotação, velocidade, luzes e setas já suportadas. Portas, matriz e articulação ainda não fazem parte deste teste. Requer OMSI 2.3.004, plugin NavBR e o veículo remoto instalado localmente.",
            "es" => "Prueba Alpha.12 limitada. Muestra físicamente el autobús remoto con posición, rotación, velocidad, luces e intermitentes ya compatibles. Puertas, matriz y articulación aún no forman parte de esta prueba. Requiere OMSI 2.3.004, plugin NavBR y el vehículo remoto instalado localmente.",
            "de" => "Begrenzter Alpha.12-Test. Zeigt den entfernten Bus physisch mit bereits unterstützter Position, Rotation, Geschwindigkeit, Licht und Blinkern. Türen, Zielanzeige und Gelenk sind noch nicht Teil dieses Tests. Benötigt OMSI 2.3.004, NavBR-Plugin und das entfernte Fahrzeug lokal installiert.",
            "fr" => "Test Alpha.12 limité. Affiche physiquement le bus distant avec position, rotation, vitesse, feux et clignotants déjà pris en charge. Portes, girouette et articulation ne font pas encore partie de ce test. Nécessite OMSI 2.3.004, le plugin NavBR et le véhicule distant installé localement.",
            _ => "Limited Alpha.12 test. Physically renders the remote bus using already-supported position, rotation, speed, lights and turn signals. Doors, destination display and articulation are not part of this test yet. Requires OMSI 2.3.004, the NavBR plugin and the remote vehicle installed locally."
        };

    private static string DiagnosticsLabel() =>
        Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Enviar diagnósticos automáticos do teste",
            "es" => "Enviar diagnósticos automáticos de la prueba",
            "de" => "Automatische Testdiagnosen senden",
            "fr" => "Envoyer les diagnostics automatiques du test",
            _ => "Send automatic test diagnostics"
        };

    private static string DiagnosticsWarning() =>
        Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Opcional. Envia versão, mapa/ônibus técnicos, estado do plugin/3D e erros. Não envia chat, voz, senhas, tokens nem arquivos pessoais. Pode ser desligado a qualquer momento.",
            "es" => "Opcional. Envía versión, mapa/vehículo técnicos, estado del plugin/3D y errores. No envía chat, voz, contraseñas, tokens ni archivos personales.",
            "de" => "Optional. Sendet Version, technische Karten/Fahrzeugdaten, Plugin-/3D-Status und Fehler. Kein Chat, Audio, Passwörter, Tokens oder persönliche Dateien.",
            "fr" => "Optionnel. Envoie la version, les données techniques carte/véhicule, l’état plugin/3D et les erreurs. Aucun chat, audio, mot de passe, jeton ou fichier personnel.",
            _ => "Optional. Sends version, technical map/vehicle identifiers, plugin/3D state and errors. No chat, voice, passwords, tokens or personal files."
        };

    private static string PhysicalVehiclesEnabledMessage() =>
        Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Teste do ônibus online ativado e plugin pronto. Jogadores compatíveis podem aparecer fisicamente no OMSI com movimento, luzes e setas.",
            "es" => "Prueba de autobús online activada y plugin listo. Los jugadores compatibles pueden aparecer físicamente en OMSI.",
            "de" => "Online-Bus-Test aktiviert und Plugin bereit. Kompatible Spieler können physisch in OMSI erscheinen.",
            "fr" => "Test du bus en ligne activé et plugin prêt. Les joueurs compatibles peuvent apparaître physiquement dans OMSI.",
            _ => "Online bus test enabled and plugin ready. Compatible players can appear physically in OMSI."
        };

    private static string PhysicalVehiclesWaitingMessage() =>
        Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Teste do ônibus online ativado. Aguardando OMSI 2.3.004 + plugin NavBR com suporte físico; quando estiver disponível, os próximos dados online tentarão criar o ônibus remoto.",
            "es" => "Prueba de autobús online activada. Esperando OMSI 2.3.004 + plugin NavBR con soporte físico.",
            "de" => "Online-Bus-Test aktiviert. Warte auf OMSI 2.3.004 + NavBR-Plugin mit physischer Unterstützung.",
            "fr" => "Test du bus en ligne activé. En attente d’OMSI 2.3.004 + plugin NavBR avec prise en charge physique.",
            _ => "Online bus test enabled. Waiting for OMSI 2.3.004 + the NavBR plugin with physical support."
        };

    private static string PhysicalVehiclesDisabledMessage() =>
        Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Teste do ônibus online desativado. Ônibus remotos do NavBR foram removidos do OMSI.",
            "es" => "Prueba de autobús online desactivada. Los autobuses remotos de NavBR fueron eliminados de OMSI.",
            "de" => "Online-Bus-Test deaktiviert. NavBR-Remote-Busse wurden aus OMSI entfernt.",
            "fr" => "Test du bus en ligne désactivé. Les bus distants NavBR ont été retirés d’OMSI.",
            _ => "Online bus test disabled. NavBR remote buses were removed from OMSI."
        };

    private static string DiagnosticsEnabledMessage() =>
        Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Diagnósticos automáticos ativados. Os eventos técnicos serão enviados quando o coletor oficial estiver disponível.",
            "es" => "Diagnósticos automáticos activados.",
            "de" => "Automatische Diagnosen aktiviert.",
            "fr" => "Diagnostics automatiques activés.",
            _ => "Automatic diagnostics enabled."
        };

    private static string DiagnosticsDisabledMessage() =>
        Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Diagnósticos automáticos desativados e fila local apagada.",
            "es" => "Diagnósticos automáticos desactivados y cola local eliminada.",
            "de" => "Automatische Diagnosen deaktiviert und lokale Warteschlange gelöscht.",
            "fr" => "Diagnostics automatiques désactivés et file locale supprimée.",
            _ => "Automatic diagnostics disabled and local queue cleared."
        };
}
