using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private Border? _transportBadge;
    private TextBlock? _transportBadgeText;
    private DispatcherTimer? _transportBadgeTimer;

    [ModuleInitializer]
    internal static void InitializeTransportBadgeBootstrap()
    {
        EventManager.RegisterClassHandler(
            typeof(MultiplayerWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(TransportBadgeWindowLoaded));
    }

    private static void TransportBadgeWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MultiplayerWindow window)
        {
            return;
        }

        _ = window.Dispatcher.BeginInvoke(
            new Action(window.InstallTransportBadge),
            DispatcherPriority.ContextIdle);
    }

    private void InstallTransportBadge()
    {
        if (_transportBadge is not null || StatusDetailText.Parent is not StackPanel sessionPanel)
        {
            return;
        }

        _transportBadgeText = new TextBlock
        {
            FontSize = 8.5d,
            FontWeight = FontWeights.Bold
        };
        _transportBadge = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(8d),
            BorderThickness = new Thickness(1d),
            Padding = new Thickness(7d, 3d, 7d, 3d),
            Margin = new Thickness(0d, 7d, 0d, 0d),
            Child = _transportBadgeText
        };
        sessionPanel.Children.Add(_transportBadge);

        _transportBadgeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500d)
        };
        _transportBadgeTimer.Tick += TransportBadgeTimer_Tick;
        _transportBadgeTimer.Start();
        Closed += TransportBadgeWindowClosed;
        RenderTransportBadge();
    }

    private void TransportBadgeTimer_Tick(object? sender, EventArgs e) => RenderTransportBadge();

    private void RenderTransportBadge()
    {
        if (_transportBadge is null || _transportBadgeText is null)
        {
            return;
        }

        var relay = _client.IsConnected &&
                    !_host.IsRunning &&
                    _settings.EnableApplicationRelay &&
                    !IsLoopbackServerUrl(ServerTextBox.Text.Trim());
        var directHost = _host.IsRunning;
        var remoteClient = _client.IsConnected && !directHost && !relay;

        var (text, foreground, background, border) = relay
            ? (
                TransportText("RELAY • EXPERIMENTAL", "RELAY • EXPERIMENTAL", "RELAY • EXPERIMENTAL", "RELAY • EXPERIMENTELL", "RELAIS • EXPÉRIMENTAL"),
                TransportBrush(116, 197, 255),
                TransportBrush(9, 38, 57),
                TransportBrush(40, 101, 139))
            : directHost
                ? (
                    TransportText("HOST DIRETO • TCP 27730", "DIRECT HOST • TCP 27730", "HOST DIRECTO • TCP 27730", "DIREKTER HOST • TCP 27730", "HÔTE DIRECT • TCP 27730"),
                    TransportBrush(105, 224, 158),
                    TransportBrush(8, 42, 31),
                    TransportBrush(34, 102, 74))
                : remoteClient
                    ? (
                        TransportText("CONECTADO AO HOST", "CONNECTED TO HOST", "CONECTADO AL HOST", "MIT HOST VERBUNDEN", "CONNECTÉ À L’HÔTE"),
                        TransportBrush(154, 193, 221),
                        TransportBrush(12, 34, 49),
                        TransportBrush(40, 77, 102))
                    : (
                        TransportText("SEM SESSÃO", "NO SESSION", "SIN SESIÓN", "KEINE SITZUNG", "AUCUNE SESSION"),
                        TransportBrush(124, 145, 160),
                        TransportBrush(17, 28, 36),
                        TransportBrush(48, 62, 72));

        _transportBadgeText.Text = text;
        _transportBadgeText.Foreground = foreground;
        _transportBadge.Background = background;
        _transportBadge.BorderBrush = border;
        _transportBadge.ToolTip = relay
            ? TransportText(
                "A sala usa um servidor NavBR remoto como transporte. O host local TCP 27730 não está aberto.",
                "The room uses a remote NavBR server as transport. The local TCP 27730 host is not open.",
                "La sala usa un servidor NavBR remoto como transporte. El host local TCP 27730 no está abierto.",
                "Der Raum verwendet einen entfernten NavBR-Server als Transport. Der lokale TCP-27730-Host ist nicht geöffnet.",
                "La salle utilise un serveur NavBR distant comme transport. L’hôte local TCP 27730 n’est pas ouvert.")
            : directHost
                ? TransportText(
                    "Este PC está hospedando a sala diretamente.",
                    "This PC is hosting the room directly.",
                    "Este PC está alojando la sala directamente.",
                    "Dieser PC hostet den Raum direkt.",
                    "Ce PC héberge directement la salle.")
                : null;
    }

    private void TransportBadgeWindowClosed(object? sender, EventArgs e)
    {
        if (_transportBadgeTimer is not null)
        {
            _transportBadgeTimer.Stop();
            _transportBadgeTimer.Tick -= TransportBadgeTimer_Tick;
            _transportBadgeTimer = null;
        }
        Closed -= TransportBadgeWindowClosed;
    }

    private static SolidColorBrush TransportBrush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

    private static string TransportText(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
}
