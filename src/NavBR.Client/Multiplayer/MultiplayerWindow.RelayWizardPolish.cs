using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private TextBlock? _relayWizardSummaryText;
    private DispatcherTimer? _relayWizardSummaryTimer;

    [ModuleInitializer]
    internal static void InitializeRelayWizardPolishBootstrap()
    {
        EventManager.RegisterClassHandler(
            typeof(MultiplayerWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(RelayWizardPolishWindowLoaded));
    }

    private static void RelayWizardPolishWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MultiplayerWindow window)
        {
            return;
        }

        _ = window.Dispatcher.BeginInvoke(
            new Action(window.InstallRelayWizardPolish),
            DispatcherPriority.SystemIdle);
    }

    private void InstallRelayWizardPolish()
    {
        if (_relayWizardSummaryTimer is not null ||
            _relayEnabledCheckBox?.Parent is not StackPanel relayPanel ||
            relayPanel.Parent is not StackPanel networkPage)
        {
            return;
        }

        _relayWizardSummaryText = networkPage.Children
            .OfType<TextBlock>()
            .LastOrDefault();

        if (_relayWizardSummaryText is not null)
        {
            networkPage.Children.Remove(relayPanel);
            var summaryIndex = networkPage.Children.IndexOf(_relayWizardSummaryText);
            networkPage.Children.Insert(
                summaryIndex >= 0 ? summaryIndex : networkPage.Children.Count,
                relayPanel);
        }

        _relayWizardSummaryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500d)
        };
        _relayWizardSummaryTimer.Tick += RelayWizardSummaryTimer_Tick;
        _relayWizardSummaryTimer.Start();
        Closed += RelayWizardPolishWindowClosed;
        RenderRelayWizardSummary();
    }

    private void RelayWizardSummaryTimer_Tick(object? sender, EventArgs e) => RenderRelayWizardSummary();

    private void RenderRelayWizardSummary()
    {
        if (_relayWizardSummaryText is null)
        {
            return;
        }

        var room = string.IsNullOrWhiteSpace(RoomTextBox.Text)
            ? RelayText(
                "gerada automaticamente",
                "generated automatically",
                "generada automáticamente",
                "automatisch erzeugt",
                "générée automatiquement")
            : RoomTextBox.Text.Trim();
        var privacy = PrivateRoomCheckBox.IsChecked == true
            ? RelayText("privada", "private", "privada", "privat", "privée")
            : RelayText("pública", "public", "pública", "öffentlich", "publique");
        var network = _relayEnabledCheckBox?.IsChecked == true
            ? "relay"
            : UpnpEnabledCheckBox.IsChecked == true
                ? "UPnP"
                : RelayText("rede local / manual", "LAN / manual", "red local / manual", "LAN / manuell", "réseau local / manuel");

        _relayWizardSummaryText.Text = RelayText(
            $"Sala: {room} • {privacy} • Rede: {network}",
            $"Room: {room} • {privacy} • Network: {network}",
            $"Sala: {room} • {privacy} • Red: {network}",
            $"Raum: {room} • {privacy} • Netzwerk: {network}",
            $"Salle : {room} • {privacy} • Réseau : {network}");
    }

    private void RelayWizardPolishWindowClosed(object? sender, EventArgs e)
    {
        if (_relayWizardSummaryTimer is not null)
        {
            _relayWizardSummaryTimer.Stop();
            _relayWizardSummaryTimer.Tick -= RelayWizardSummaryTimer_Tick;
            _relayWizardSummaryTimer = null;
        }
        Closed -= RelayWizardPolishWindowClosed;
    }
}
