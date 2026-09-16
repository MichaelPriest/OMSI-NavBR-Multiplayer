using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Operations;

namespace NavBR.Client.Multiplayer;

internal static class Alpha12MultiplayerStatusInstaller
{
    private const string CardTag = "alpha12-multiplayer-live-status";
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        var host = FindAncestor<StackPanel>(window.MultiplayerButton);
        if (host is null || host.Children.OfType<FrameworkElement>().Any(item => item.Tag as string == CardTag))
        {
            Installed.Remove(window);
            return;
        }

        var state = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 15d,
            FontWeight = FontWeights.SemiBold
        };
        var detail = new TextBlock
        {
            Foreground = Brush(142, 161, 174),
            FontSize = 10.5d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 5d, 0d, 0d)
        };
        var room = MetricValue();
        var drivers = MetricValue();
        var telemetry = MetricValue();
        var roomLabel = MetricLabel();
        var driversLabel = MetricLabel();
        var telemetryLabel = MetricLabel();

        var metricGrid = new Grid { Margin = new Thickness(0d, 14d, 0d, 0d) };
        for (var i = 0; i < 3; i++)
        {
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        }
        metricGrid.Children.Add(Metric(roomLabel, room, 0));
        metricGrid.Children.Add(Metric(driversLabel, drivers, 1));
        metricGrid.Children.Add(Metric(telemetryLabel, telemetry, 2));

        var healthButton = SecondaryButton();
        healthButton.Content = Text("OpenHealth");
        healthButton.Click += (_, _) =>
        {
            if (Application.Current is App app)
            {
                new SessionHealthWindow(
                    window,
                    window.GetCurrentTelemetryForAlpha11,
                    app.PluginBridge.GetConnectionInfo).ShowDialog();
            }
        };

        var connectivityButton = SecondaryButton();
        connectivityButton.Content = Alpha12ConnectivityWindow.ButtonText();
        connectivityButton.Margin = new Thickness(10d, 0d, 0d, 0d);
        connectivityButton.Click += (_, _) => new Alpha12ConnectivityWindow(window).ShowDialog();

        var natButton = SecondaryButton();
        natButton.Content = NatDiagnosticsWindow.ButtonText();
        natButton.Margin = new Thickness(10d, 0d, 0d, 0d);
        natButton.Click += (_, _) => new NatDiagnosticsWindow(window).ShowDialog();

        var actions = new WrapPanel
        {
            Margin = new Thickness(0d, 14d, 0d, 0d)
        };
        actions.Children.Add(healthButton);
        actions.Children.Add(connectivityButton);
        actions.Children.Add(natButton);

        var body = new StackPanel();
        body.Children.Add(state);
        body.Children.Add(detail);
        body.Children.Add(metricGrid);
        body.Children.Add(actions);

        var card = new Border
        {
            Tag = CardTag,
            Margin = new Thickness(0d, 0d, 0d, 18d),
            Padding = new Thickness(18d),
            CornerRadius = new CornerRadius(14d),
            Background = Brush(9, 18, 24),
            BorderBrush = Brush(36, 54, 66),
            BorderThickness = new Thickness(1d),
            Child = body
        };

        var cta = FindAncestor<Border>(window.MultiplayerButton);
        var insertAt = cta is null ? host.Children.Count : host.Children.IndexOf(cta);
        host.Children.Insert(Math.Max(0, insertAt), card);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750d) };
        void Refresh()
        {
            var snapshot = DispatcherSessionFeed.Snapshot();
            state.Text = snapshot.Connected ? Text("Connected") : Text("Disconnected");
            state.Foreground = snapshot.Connected ? Brush(101, 224, 154) : Brush(168, 180, 188);
            detail.Text = snapshot.Connected ? Text("ConnectedBody") : Text("DisconnectedBody");
            roomLabel.Text = Text("Room");
            driversLabel.Text = Text("Drivers");
            telemetryLabel.Text = Text("Telemetry");
            room.Text = string.IsNullOrWhiteSpace(snapshot.RoomId) ? "—" : snapshot.RoomId;
            drivers.Text = snapshot.Connected ? snapshot.RemoteDrivers.Count.ToString() : "—";

            if (!snapshot.Connected || snapshot.RemoteDrivers.Count == 0)
            {
                telemetry.Text = "—";
            }
            else
            {
                var newest = snapshot.RemoteDrivers.Max(driver => driver.ReceivedAtUtc);
                var age = Math.Max(0d, (DateTimeOffset.UtcNow - newest).TotalSeconds);
                telemetry.Text = age < 1d ? Text("Now") : string.Format(Text("Seconds"), Math.Round(age));
            }

            healthButton.Content = Text("OpenHealth");
            connectivityButton.Content = Alpha12ConnectivityWindow.ButtonText();
            natButton.Content = NatDiagnosticsWindow.ButtonText();
        }

        SelectionChangedEventHandler languageChanged = (_, _) => Refresh();
        window.LanguageComboBox.SelectionChanged += languageChanged;
        timer.Tick += (_, _) => Refresh();
        window.Closed += (_, _) =>
        {
            timer.Stop();
            window.LanguageComboBox.SelectionChanged -= languageChanged;
            Installed.Remove(window);
        };

        Refresh();
        timer.Start();
    }

    private static Button SecondaryButton() => new()
    {
        Height = 36d,
        MinWidth = 175d,
        Padding = new Thickness(14d, 6d, 14d, 6d),
        HorizontalAlignment = HorizontalAlignment.Left,
        Background = Brush(14, 27, 35),
        Foreground = Brush(218, 230, 238),
        BorderBrush = Brush(44, 65, 78),
        BorderThickness = new Thickness(1d),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static Border Metric(TextBlock label, TextBlock value, int column)
    {
        var stack = new StackPanel();
        stack.Children.Add(label);
        stack.Children.Add(value);
        var border = new Border
        {
            Margin = new Thickness(column == 0 ? 0d : 7d, 0d, column == 2 ? 0d : 7d, 0d),
            Padding = new Thickness(12d),
            Background = Brush(7, 14, 19),
            BorderBrush = Brush(28, 43, 52),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(9d),
            Child = stack
        };
        Grid.SetColumn(border, column);
        return border;
    }

    private static TextBlock MetricLabel() => new()
    {
        Foreground = Brush(116, 137, 151),
        FontSize = 8.8d,
        FontWeight = FontWeights.Bold
    };

    private static TextBlock MetricValue() => new()
    {
        Foreground = Brushes.White,
        FontSize = 13d,
        FontWeight = FontWeights.SemiBold,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Margin = new Thickness(0d, 4d, 0d, 0d)
    };

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        DependencyObject? current = start;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static string Text(string key)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        return (language, key) switch
        {
            ("pt", "Connected") => "Multiplayer conectado",
            ("pt", "Disconnected") => "Multiplayer desconectado",
            ("pt", "ConnectedBody") => "Sua sessão está ativa. O NavBR mostra aqui a sala e a telemetria recebida dos outros motoristas.",
            ("pt", "DisconnectedBody") => "Crie uma sala ou entre em uma existente. O restante da navegação do NavBR continua funcionando normalmente offline.",
            ("pt", "Room") => "SALA",
            ("pt", "Drivers") => "MOTORISTAS REMOTOS",
            ("pt", "Telemetry") => "ÚLTIMA TELEMETRIA",
            ("pt", "Now") => "agora",
            ("pt", "Seconds") => "há {0} s",
            ("pt", "OpenHealth") => "Ver saúde da sessão",
            ("es", "Connected") => "Multijugador conectado",
            ("es", "Disconnected") => "Multijugador desconectado",
            ("es", "ConnectedBody") => "La sesión está activa. NavBR muestra la sala y la telemetría recibida de otros conductores.",
            ("es", "DisconnectedBody") => "Crea una sala o únete a una existente. La navegación local sigue funcionando sin conexión.",
            ("es", "Room") => "SALA",
            ("es", "Drivers") => "CONDUCTORES REMOTOS",
            ("es", "Telemetry") => "ÚLTIMA TELEMETRÍA",
            ("es", "Now") => "ahora",
            ("es", "Seconds") => "hace {0} s",
            ("es", "OpenHealth") => "Ver salud de la sesión",
            ("de", "Connected") => "Mehrspieler verbunden",
            ("de", "Disconnected") => "Mehrspieler getrennt",
            ("de", "ConnectedBody") => "Die Sitzung ist aktiv. NavBR zeigt Raum und empfangene Telemetrie anderer Fahrer.",
            ("de", "DisconnectedBody") => "Raum erstellen oder beitreten. Die lokale NavBR-Navigation funktioniert weiterhin offline.",
            ("de", "Room") => "RAUM",
            ("de", "Drivers") => "REMOTE-FAHRER",
            ("de", "Telemetry") => "LETZTE TELEMETRIE",
            ("de", "Now") => "jetzt",
            ("de", "Seconds") => "vor {0} s",
            ("de", "OpenHealth") => "Sitzungsstatus öffnen",
            ("fr", "Connected") => "Multijoueur connecté",
            ("fr", "Disconnected") => "Multijoueur déconnecté",
            ("fr", "ConnectedBody") => "La session est active. NavBR affiche la salle et la télémétrie reçue des autres conducteurs.",
            ("fr", "DisconnectedBody") => "Créez ou rejoignez une salle. La navigation locale de NavBR continue de fonctionner hors ligne.",
            ("fr", "Room") => "SALLE",
            ("fr", "Drivers") => "CONDUCTEURS DISTANTS",
            ("fr", "Telemetry") => "DERNIÈRE TÉLÉMÉTRIE",
            ("fr", "Now") => "maintenant",
            ("fr", "Seconds") => "il y a {0} s",
            ("fr", "OpenHealth") => "Voir la santé de session",
            (_, "Connected") => "Multiplayer connected",
            (_, "Disconnected") => "Multiplayer disconnected",
            (_, "ConnectedBody") => "Your session is active. NavBR shows the room and telemetry received from other drivers here.",
            (_, "DisconnectedBody") => "Create or join a room. Local NavBR navigation continues to work normally while offline.",
            (_, "Room") => "ROOM",
            (_, "Drivers") => "REMOTE DRIVERS",
            (_, "Telemetry") => "LAST TELEMETRY",
            (_, "Now") => "now",
            (_, "Seconds") => "{0} s ago",
            (_, "OpenHealth") => "View session health",
            _ => key
        };
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
