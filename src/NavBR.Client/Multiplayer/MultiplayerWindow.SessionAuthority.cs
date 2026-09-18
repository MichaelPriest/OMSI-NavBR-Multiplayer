using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private Border? _sessionAuthorityCard;
    private TextBlock? _sessionAuthorityText;
    private DispatcherTimer? _sessionAuthorityTimer;

    [ModuleInitializer]
    internal static void InitializeSessionAuthorityBootstrap()
    {
        EventManager.RegisterClassHandler(
            typeof(MultiplayerWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(SessionAuthorityWindowLoaded));
    }

    private static void SessionAuthorityWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MultiplayerWindow window)
        {
            return;
        }

        _ = window.Dispatcher.BeginInvoke(
            new Action(window.InstallSessionAuthorityCard),
            DispatcherPriority.SystemIdle);
    }

    private void InstallSessionAuthorityCard()
    {
        if (_sessionAuthorityCard is not null || StatusDetailText.Parent is not StackPanel sessionPanel)
        {
            return;
        }

        _sessionAuthorityText = new TextBlock
        {
            Foreground = AuthorityBrush(174, 200, 217),
            FontSize = 9.3d,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        _sessionAuthorityCard = new Border
        {
            Background = AuthorityBrush(8, 29, 42),
            BorderBrush = AuthorityBrush(31, 65, 87),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(8d),
            Padding = new Thickness(8d, 5d, 8d, 5d),
            Margin = new Thickness(0d, 6d, 0d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = _sessionAuthorityText,
            Visibility = Visibility.Collapsed
        };
        sessionPanel.Children.Add(_sessionAuthorityCard);

        _sessionAuthorityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(600d)
        };
        _sessionAuthorityTimer.Tick += SessionAuthorityTimer_Tick;
        _sessionAuthorityTimer.Start();
        Closed += SessionAuthorityWindowClosed;
        RenderSessionAuthority();
    }

    private void SessionAuthorityTimer_Tick(object? sender, EventArgs e) => RenderSessionAuthority();

    private void RenderSessionAuthority()
    {
        if (_sessionAuthorityCard is null || _sessionAuthorityText is null)
        {
            return;
        }

        if (!_client.IsConnected)
        {
            _sessionAuthorityCard.Visibility = Visibility.Collapsed;
            return;
        }

        _sessionAuthorityCard.Visibility = Visibility.Visible;
        var ownerName = ResolveAuthorityDisplayName(_client.RoomOwnerPlayerId);
        var trafficName = ResolveAuthorityDisplayName(_client.TrafficAuthorityPlayerId);
        var privacy = _client.CurrentRoomIsPrivate
            ? AuthorityText("privada", "private", "privada", "privat", "privée")
            : AuthorityText("pública", "public", "pública", "öffentlich", "publique");

        if (!string.IsNullOrWhiteSpace(_client.RoomOwnerPlayerId) &&
            string.Equals(
                _client.RoomOwnerPlayerId,
                _client.TrafficAuthorityPlayerId,
                StringComparison.OrdinalIgnoreCase))
        {
            _sessionAuthorityText.Text = AuthorityText(
                $"Sala {privacy} • dono e autoridade: {ownerName}",
                $"{privacy} room • owner and authority: {ownerName}",
                $"Sala {privacy} • propietario y autoridad: {ownerName}",
                $"{privacy} Raum • Besitzer und Autorität: {ownerName}",
                $"Salle {privacy} • propriétaire et autorité : {ownerName}");
        }
        else
        {
            _sessionAuthorityText.Text = AuthorityText(
                $"Sala {privacy} • dono: {ownerName} • tráfego: {trafficName}",
                $"{privacy} room • owner: {ownerName} • traffic: {trafficName}",
                $"Sala {privacy} • propietario: {ownerName} • tráfico: {trafficName}",
                $"{privacy} Raum • Besitzer: {ownerName} • Verkehr: {trafficName}",
                $"Salle {privacy} • propriétaire : {ownerName} • trafic : {trafficName}");
        }

        var localIsOwner = _client.IsRoomOwner;
        var localIsTrafficAuthority = _client.IsTrafficAuthority;
        _sessionAuthorityCard.BorderBrush = localIsOwner || localIsTrafficAuthority
            ? AuthorityBrush(47, 112, 150)
            : AuthorityBrush(31, 65, 87);
        _sessionAuthorityCard.ToolTip = AuthorityText(
            "O dono controla a identidade/privacidade da sala. A autoridade de tráfego publica o snapshot de tráfego compartilhado. A função pode migrar quando o responsável sai.",
            "The owner controls room identity/privacy. The traffic authority publishes the shared traffic snapshot. Authority can migrate when the responsible player leaves.",
            "El propietario controla la identidad/privacidad de la sala. La autoridad de tráfico publica el tráfico compartido. La función puede migrar cuando el responsable sale.",
            "Der Besitzer kontrolliert Raumidentität/Privatsphäre. Die Verkehrsautorität veröffentlicht den gemeinsamen Verkehr. Die Rolle kann beim Verlassen wechseln.",
            "Le propriétaire contrôle l’identité/confidentialité de la salle. L’autorité trafic publie le trafic partagé. Le rôle peut migrer au départ du responsable.");
    }

    private string ResolveAuthorityDisplayName(string? playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return AuthorityText("indefinido", "unknown", "desconocido", "unbekannt", "inconnu");
        }

        if (string.Equals(playerId, _settings.PlayerId, StringComparison.OrdinalIgnoreCase))
        {
            return AuthorityText("você", "you", "tú", "Sie", "vous");
        }

        return _players.TryGetValue(playerId, out var player) && !string.IsNullOrWhiteSpace(player.DisplayName)
            ? player.DisplayName
            : playerId;
    }

    private void SessionAuthorityWindowClosed(object? sender, EventArgs e)
    {
        if (_sessionAuthorityTimer is not null)
        {
            _sessionAuthorityTimer.Stop();
            _sessionAuthorityTimer.Tick -= SessionAuthorityTimer_Tick;
            _sessionAuthorityTimer = null;
        }
        Closed -= SessionAuthorityWindowClosed;
    }

    private static SolidColorBrush AuthorityBrush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));

    private static string AuthorityText(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
}
