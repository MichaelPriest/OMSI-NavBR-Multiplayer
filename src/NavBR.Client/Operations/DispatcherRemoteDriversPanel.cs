using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;

namespace NavBR.Client.Operations;

internal static class DispatcherRemoteDriversPanel
{
    private const string PanelTag = "alpha12-dispatcher-remote-drivers";

    public static void Attach(DispatcherWindow window)
    {
        var body = FindBody(window);
        if (body is null || body.Children.OfType<FrameworkElement>().Any(item => item.Tag as string == PanelTag))
        {
            return;
        }

        var title = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 14d,
            FontWeight = FontWeights.Bold
        };
        var status = new TextBlock
        {
            Foreground = Brush(128, 151, 166),
            FontSize = 10d,
            Margin = new Thickness(0d, 5d, 0d, 12d),
            TextWrapping = TextWrapping.Wrap
        };
        var rows = new StackPanel();

        var content = new StackPanel();
        content.Children.Add(title);
        content.Children.Add(status);
        content.Children.Add(rows);

        var panel = new Border
        {
            Tag = PanelTag,
            Margin = new Thickness(0d, 0d, 0d, 12d),
            Padding = new Thickness(16d),
            Background = Brush(10, 19, 25),
            BorderBrush = Brush(31, 47, 57),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Child = content
        };

        var insertIndex = Math.Max(0, body.Children.Count - 1);
        body.Children.Insert(insertIndex, panel);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500d) };
        string? lastSignature = null;

        void Refresh()
        {
            var snapshot = DispatcherSessionFeed.Snapshot();
            title.Text = Text("RemoteDrivers");
            status.Text = snapshot.Connected
                ? string.Format(Text("ConnectedStatus"), snapshot.RemoteDrivers.Count)
                : Text("OfflineStatus");

            var signature = string.Join('|', snapshot.RemoteDrivers.Select(driver =>
                $"{driver.PlayerId}:{driver.ReceivedAtUtc.UtcTicks}:{driver.SpeedKph:0.0}:{driver.DelaySeconds}"));
            signature = $"{snapshot.Connected}:{signature}:{LocalizationService.CurrentCulture.Name}";
            if (string.Equals(signature, lastSignature, StringComparison.Ordinal))
            {
                return;
            }

            lastSignature = signature;
            rows.Children.Clear();
            if (snapshot.RemoteDrivers.Count == 0)
            {
                rows.Children.Add(new TextBlock
                {
                    Text = snapshot.Connected ? Text("WaitingDrivers") : Text("JoinSession"),
                    Foreground = Brush(139, 158, 171),
                    FontSize = 10.5d,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0d, 2d, 0d, 2d)
                });
                return;
            }

            foreach (var driver in snapshot.RemoteDrivers)
            {
                rows.Children.Add(BuildDriverRow(driver));
            }
        }

        timer.Tick += (_, _) => Refresh();
        window.Closed += (_, _) => timer.Stop();
        Refresh();
        timer.Start();
    }

    private static Border BuildDriverRow(DispatcherRemoteDriver driver)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92d) });

        var driverBlock = BuildCell(driver.DisplayName, Safe(driver.MapName), true);
        var vehicleBlock = BuildCell(Safe(driver.VehicleName), Safe(driver.Destination), false);
        var service = string.Join(" • ", new[] { driver.Line, driver.Route }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var serviceBlock = BuildCell(string.IsNullOrWhiteSpace(service) ? "—" : service, Safe(driver.NextStop), false);
        var speedBlock = BuildCell($"{driver.SpeedKph:0.0} km/h", Text("Speed"), true);
        var delayBlock = BuildCell(FormatDelay(driver.DelaySeconds), Text("Schedule"), false);

        Add(grid, driverBlock, 0);
        Add(grid, vehicleBlock, 1);
        Add(grid, serviceBlock, 2);
        Add(grid, speedBlock, 3);
        Add(grid, delayBlock, 4);

        return new Border
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
            Padding = new Thickness(12d, 10d, 12d, 10d),
            Background = Brush(7, 15, 20),
            BorderBrush = Brush(25, 39, 48),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(9d),
            Child = grid
        };
    }

    private static StackPanel BuildCell(string primary, string secondary, bool emphasize)
    {
        var stack = new StackPanel { Margin = new Thickness(0d, 0d, 12d, 0d) };
        stack.Children.Add(new TextBlock
        {
            Text = primary,
            Foreground = emphasize ? Brushes.White : Brush(218, 229, 236),
            FontWeight = emphasize ? FontWeights.SemiBold : FontWeights.Normal,
            FontSize = 10.5d,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        stack.Children.Add(new TextBlock
        {
            Text = secondary,
            Foreground = Brush(111, 132, 145),
            FontSize = 8.8d,
            Margin = new Thickness(0d, 2d, 0d, 0d),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        return stack;
    }

    private static void Add(Grid grid, UIElement element, int column)
    {
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private static StackPanel? FindBody(DependencyObject root)
    {
        if (root is ScrollViewer { Content: StackPanel panel })
        {
            return panel;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindBody(VisualTreeHelper.GetChild(root, i)) is { } result)
            {
                return result;
            }
        }

        return null;
    }

    private static string FormatDelay(int? seconds)
    {
        if (seconds is null)
        {
            return "—";
        }
        if (Math.Abs(seconds.Value) < 30)
        {
            return Text("OnTime");
        }

        var minutes = (int)Math.Round(Math.Abs(seconds.Value) / 60d);
        return seconds.Value > 0 ? $"+{minutes} min" : $"-{minutes} min";
    }

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string Text(string key)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        var values = language switch
        {
            "pt" => Pt,
            "es" => Es,
            "de" => De,
            "fr" => Fr,
            _ => En
        };
        return values.TryGetValue(key, out var value) ? value : key;
    }

    private static readonly IReadOnlyDictionary<string, string> En = T(
        ("RemoteDrivers", "Drivers in the multiplayer session"),
        ("ConnectedStatus", "Multiplayer connected • {0} remote driver(s) visible to the control center."),
        ("OfflineStatus", "Multiplayer is offline. The local OMSI operation remains available above."),
        ("WaitingDrivers", "Connected. Waiting for telemetry from other drivers in the room."),
        ("JoinSession", "Open Multiplayer and join or create a room to populate this control-center view."),
        ("Speed", "Speed"), ("Schedule", "Schedule"), ("OnTime", "On time"));
    private static readonly IReadOnlyDictionary<string, string> Pt = T(
        ("RemoteDrivers", "Motoristas da sessão multiplayer"),
        ("ConnectedStatus", "Multiplayer conectado • {0} motorista(s) remoto(s) visível(is) no CCO."),
        ("OfflineStatus", "Multiplayer desconectado. A operação local do OMSI continua disponível acima."),
        ("WaitingDrivers", "Conectado. Aguardando telemetria dos outros motoristas da sala."),
        ("JoinSession", "Abra o Multiplayer e crie ou entre em uma sala para preencher esta visão do CCO."),
        ("Speed", "Velocidade"), ("Schedule", "Horário"), ("OnTime", "No horário"));
    private static readonly IReadOnlyDictionary<string, string> Es = T(
        ("RemoteDrivers", "Conductores de la sesión multijugador"),
        ("ConnectedStatus", "Multijugador conectado • {0} conductor(es) remoto(s) visible(s) en el CCO."),
        ("OfflineStatus", "Multijugador desconectado. La operación OMSI local sigue disponible arriba."),
        ("WaitingDrivers", "Conectado. Esperando telemetría de otros conductores de la sala."),
        ("JoinSession", "Abre Multijugador y crea o únete a una sala para llenar esta vista del CCO."),
        ("Speed", "Velocidad"), ("Schedule", "Horario"), ("OnTime", "En horario"));
    private static readonly IReadOnlyDictionary<string, string> De = T(
        ("RemoteDrivers", "Fahrer der Mehrspieler-Sitzung"),
        ("ConnectedStatus", "Mehrspieler verbunden • {0} Remote-Fahrer in der Leitstelle sichtbar."),
        ("OfflineStatus", "Mehrspieler ist offline. Der lokale OMSI-Betrieb bleibt oben sichtbar."),
        ("WaitingDrivers", "Verbunden. Warte auf Telemetrie anderer Fahrer im Raum."),
        ("JoinSession", "Mehrspieler öffnen und einen Raum erstellen oder beitreten, um diese Leitstellenansicht zu füllen."),
        ("Speed", "Geschwindigkeit"), ("Schedule", "Fahrplan"), ("OnTime", "Pünktlich"));
    private static readonly IReadOnlyDictionary<string, string> Fr = T(
        ("RemoteDrivers", "Conducteurs de la session multijoueur"),
        ("ConnectedStatus", "Multijoueur connecté • {0} conducteur(s) distant(s) visible(s) au PCC."),
        ("OfflineStatus", "Multijoueur hors ligne. L’exploitation OMSI locale reste disponible ci-dessus."),
        ("WaitingDrivers", "Connecté. En attente de la télémétrie des autres conducteurs de la salle."),
        ("JoinSession", "Ouvrez le multijoueur et créez ou rejoignez une salle pour alimenter cette vue PCC."),
        ("Speed", "Vitesse"), ("Schedule", "Horaire"), ("OnTime", "À l’heure"));

    private static IReadOnlyDictionary<string, string> T(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
