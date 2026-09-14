using System.Windows;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private async void FirewallButton_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyFirewallButtonText();
        await RefreshFirewallStatusAsync();
    }

    private async void FirewallButton_Click(object sender, RoutedEventArgs e)
    {
        FirewallButton.IsEnabled = false;
        FirewallStatusText.Text = GetFirewallMessage("requesting");

        try
        {
            var ok = await WindowsFirewallService.EnsureInboundRuleAsync(DefaultHostPort);
            FirewallStatusText.Text = ok
                ? GetFirewallMessage("ready")
                : GetFirewallMessage("failed");
        }
        finally
        {
            FirewallButton.IsEnabled = true;
        }
    }

    private async Task RefreshFirewallStatusAsync()
    {
        var ready = await WindowsFirewallService.IsInboundRulePresentAsync(DefaultHostPort);
        FirewallStatusText.Text = ready
            ? GetFirewallMessage("ready")
            : GetFirewallMessage("missing");
    }

    private void ApplyFirewallButtonText()
    {
        FirewallButton.Content = LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Permitir TCP 27730",
            "es" => "Permitir TCP 27730",
            "de" => "TCP 27730 erlauben",
            "fr" => "Autoriser TCP 27730",
            _ => "Allow TCP 27730"
        };
    }

    private static string GetFirewallMessage(string state)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        return (language, state) switch
        {
            ("pt", "ready") => "Regra de entrada do Windows Firewall encontrada para TCP 27730 (perfil Privado).",
            ("pt", "missing") => "Para receber jogadores pela rede, permita TCP 27730 no Windows Firewall. Encaminhamento no roteador é uma etapa separada para acesso pela Internet.",
            ("pt", "requesting") => "Solicitando permissão administrativa do Windows para criar a regra de firewall…",
            ("pt", _) => "Não foi possível confirmar a regra do firewall. Você pode tentar novamente ou configurá-la manualmente.",

            ("es", "ready") => "Regla de entrada de Windows Firewall encontrada para TCP 27730 (perfil Privado).",
            ("es", "missing") => "Para recibir jugadores por red, permite TCP 27730 en Windows Firewall. El reenvío del router es independiente.",
            ("es", "requesting") => "Solicitando permiso administrativo de Windows para crear la regla…",
            ("es", _) => "No se pudo confirmar la regla del firewall.",

            ("de", "ready") => "Windows-Firewall-Eingangsregel für TCP 27730 (Privates Profil) gefunden.",
            ("de", "missing") => "Für eingehende Spieler TCP 27730 in der Windows-Firewall erlauben. Router-Portweiterleitung ist separat.",
            ("de", "requesting") => "Windows-Administratorberechtigung für die Firewall-Regel wird angefordert…",
            ("de", _) => "Die Firewall-Regel konnte nicht bestätigt werden.",

            ("fr", "ready") => "Règle entrante du Pare-feu Windows trouvée pour TCP 27730 (profil Privé).",
            ("fr", "missing") => "Pour recevoir des joueurs, autorisez TCP 27730 dans le Pare-feu Windows. La redirection du routeur est séparée.",
            ("fr", "requesting") => "Demande d’autorisation administrateur Windows pour créer la règle…",
            ("fr", _) => "Impossible de confirmer la règle du pare-feu.",

            (_, "ready") => "Windows Firewall inbound rule found for TCP 27730 (Private profile).",
            (_, "missing") => "To receive players over the network, allow TCP 27730 in Windows Firewall. Router port forwarding is a separate Internet step.",
            (_, "requesting") => "Requesting Windows administrator permission to create the firewall rule…",
            _ => "The firewall rule could not be confirmed."
        };
    }
}
