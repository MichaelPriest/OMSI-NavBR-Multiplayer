using System.Windows;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private bool _loadingUpnpUi;

    private void UpnpEnabledCheckBox_Loaded(object sender, RoutedEventArgs e)
    {
        _loadingUpnpUi = true;
        try
        {
            _settings = MultiplayerSettingsStore.Load();
            UpnpEnabledCheckBox.IsChecked = _settings.EnableAutomaticUpnp;
            ApplyUpnpLocalization();
        }
        finally
        {
            _loadingUpnpUi = false;
        }
    }

    private void UpnpEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingUpnpUi)
        {
            return;
        }

        if (_host.IsRunning)
        {
            _loadingUpnpUi = true;
            try
            {
                UpnpEnabledCheckBox.IsChecked = _settings.EnableAutomaticUpnp;
                StatusDetailText.Text = UpnpText("RestartRequired");
            }
            finally
            {
                _loadingUpnpUi = false;
            }
            return;
        }

        _settings = _settings with
        {
            EnableAutomaticUpnp = UpnpEnabledCheckBox.IsChecked == true
        };
        MultiplayerSettingsStore.Save(_settings);
        StatusDetailText.Text = _settings.EnableAutomaticUpnp
            ? UpnpText("Enabled")
            : UpnpText("Disabled");
    }

    private void ApplyUpnpLocalization()
    {
        UpnpEnabledCheckBox.Content = UpnpText("Title");
        UpnpDescriptionText.Text = UpnpText("Description");
    }

    private static string UpnpText(string key)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        return (language, key) switch
        {
            ("pt", "Title") => "Tentar abrir a porta automaticamente com UPnP",
            ("pt", "Description") => "Opcional. Ao hospedar uma sala, o NavBR pede ao roteador para encaminhar TCP 27730 e remove o mapeamento ao encerrar. Se o roteador não suportar UPnP, a sala local continua funcionando.",
            ("pt", "Enabled") => "UPnP automático ativado para a próxima sala hospedada.",
            ("pt", "Disabled") => "UPnP automático desativado.",
            ("pt", "RestartRequired") => "Pare a hospedagem atual antes de alterar o UPnP.",

            ("es", "Title") => "Intentar abrir el puerto automáticamente con UPnP",
            ("es", "Description") => "Opcional. Al alojar una sala, NavBR solicita al router mapear TCP 27730 y elimina el mapeo al terminar. Si UPnP no está disponible, la sala local sigue funcionando.",
            ("es", "Enabled") => "UPnP automático activado para la próxima sala alojada.",
            ("es", "Disabled") => "UPnP automático desactivado.",
            ("es", "RestartRequired") => "Detén el alojamiento actual antes de cambiar UPnP.",

            ("de", "Title") => "Port automatisch per UPnP freigeben",
            ("de", "Description") => "Optional. Beim Hosten versucht NavBR TCP 27730 am Router freizugeben und entfernt die Freigabe beim Beenden. Ohne UPnP funktioniert der lokale Raum weiterhin.",
            ("de", "Enabled") => "Automatisches UPnP ist für den nächsten gehosteten Raum aktiviert.",
            ("de", "Disabled") => "Automatisches UPnP ist deaktiviert.",
            ("de", "RestartRequired") => "Beende zuerst das aktuelle Hosting, bevor UPnP geändert wird.",

            ("fr", "Title") => "Essayer d’ouvrir automatiquement le port avec UPnP",
            ("fr", "Description") => "Optionnel. Lors de l’hébergement, NavBR demande au routeur de rediriger TCP 27730 et supprime la redirection à l’arrêt. Sans UPnP, la salle locale continue de fonctionner.",
            ("fr", "Enabled") => "UPnP automatique activé pour la prochaine salle hébergée.",
            ("fr", "Disabled") => "UPnP automatique désactivé.",
            ("fr", "RestartRequired") => "Arrêtez l’hébergement actuel avant de modifier UPnP.",

            (_, "Title") => "Try to open the port automatically with UPnP",
            (_, "Description") => "Optional. When hosting a room, NavBR asks the router to forward TCP 27730 and removes the mapping when hosting stops. If UPnP is unavailable, local hosting still works.",
            (_, "Enabled") => "Automatic UPnP is enabled for the next hosted room.",
            (_, "Disabled") => "Automatic UPnP is disabled.",
            (_, "RestartRequired") => "Stop the current host before changing UPnP.",
            _ => key
        };
    }
}
