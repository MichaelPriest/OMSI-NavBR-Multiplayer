using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Windows;

internal static class Alpha12DashboardPolishInstaller
{
    private static readonly Dictionary<MainWindow, DispatcherTimer> Timers = new();

    public static void Install(MainWindow window)
    {
        if (Timers.ContainsKey(window))
        {
            return;
        }

        var homePage = FindAncestor<ScrollViewer>(window.StatusHeadingText);
        var navigationPage = FindAncestor<ScrollViewer>(window.GpsHeadingText);
        if (homePage?.Content is not StackPanel homeStack ||
            navigationPage?.Content is not StackPanel navigationStack)
        {
            return;
        }

        var refs = new DashboardRefs();
        var homeDashboard = BuildHomeDashboard(window, homePage, navigationPage, refs);
        homeStack.Children.Insert(Math.Min(1, homeStack.Children.Count), homeDashboard);

        var navigationCard = FindAncestor<Border>(window.GpsHeadingText);
        if (navigationCard is not null)
        {
            InstallNavigationWorkspace(navigationStack, navigationCard, refs);
        }

        void Refresh()
        {
            var telemetry = window.GetCurrentTelemetryForAlpha11();
            RefreshValues(refs, telemetry);
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500d) };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
        Timers[window] = timer;
        Refresh();

        window.Closed += (_, _) =>
        {
            timer.Stop();
            Timers.Remove(window);
        };
    }

    private static FrameworkElement BuildHomeDashboard(
        MainWindow window,
        ScrollViewer homePage,
        ScrollViewer navigationPage,
        DashboardRefs refs)
    {
        var grid = new Grid { Margin = new Thickness(0d, 0d, 0d, 20d) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var current = BuildCurrentOperationCard(refs);
        Grid.SetColumn(current, 0);
        grid.Children.Add(current);

        var navigation = BuildActionCard(
            "⌖",
            Text("StartNavigation"),
            Text("StartNavigationBody"),
            Text("OpenNavigation"),
            () => ShowPage(navigationPage));
        Grid.SetColumn(navigation, 2);
        grid.Children.Add(navigation);

        var multiplayer = BuildActionCard(
            "●",
            Text("Multiplayer"),
            Text("MultiplayerBody"),
            Text("OpenMultiplayer"),
            () => window.MultiplayerButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
        Grid.SetColumn(multiplayer, 4);
        grid.Children.Add(multiplayer);

        return grid;
    }

    private static Border BuildCurrentOperationCard(DashboardRefs refs)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = Text("ContinueOperation").ToUpperInvariant(),
            Foreground = Brush(91, 157, 194),
            FontSize = 9d,
            FontWeight = FontWeights.Bold
        });
        refs.HomeMap.Foreground = Brushes.White;
        refs.HomeMap.FontSize = 20d;
        refs.HomeMap.FontWeight = FontWeights.Bold;
        refs.HomeMap.Margin = new Thickness(0d, 7d, 0d, 0d);
        stack.Children.Add(refs.HomeMap);
        refs.HomeService.Foreground = Brush(153, 175, 188);
        refs.HomeService.FontSize = 11d;
        refs.HomeService.Margin = new Thickness(0d, 5d, 0d, 0d);
        refs.HomeService.TextWrapping = TextWrapping.Wrap;
        stack.Children.Add(refs.HomeService);
        refs.HomeVehicle.Foreground = Brush(112, 143, 161);
        refs.HomeVehicle.FontSize = 10d;
        refs.HomeVehicle.Margin = new Thickness(0d, 4d, 0d, 0d);
        refs.HomeVehicle.TextWrapping = TextWrapping.Wrap;
        stack.Children.Add(refs.HomeVehicle);

        var state = new Border
        {
            Margin = new Thickness(0d, 14d, 0d, 0d),
            Padding = new Thickness(10d, 7d, 10d, 7d),
            Background = Brush(7, 37, 29),
            BorderBrush = Brush(27, 90, 67),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(8d),
            Child = refs.HomeState
        };
        refs.HomeState.Foreground = Brush(95, 221, 155);
        refs.HomeState.FontSize = 9.5d;
        refs.HomeState.FontWeight = FontWeights.Bold;
        stack.Children.Add(state);

        return NewCard(stack);
    }

    private static Border BuildActionCard(
        string icon,
        string title,
        string body,
        string buttonText,
        Action click)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = icon,
            Foreground = Brush(76, 190, 255),
            FontSize = 21d
        });
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontSize = 14d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 7d, 0d, 0d)
        });
        stack.Children.Add(new TextBlock
        {
            Text = body,
            Foreground = Brush(125, 149, 165),
            FontSize = 10d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 6d, 0d, 12d)
        });
        var button = new Button
        {
            Content = buttonText,
            Height = 36d,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brush(17, 98, 165),
            Foreground = Brushes.White,
            BorderBrush = Brush(49, 151, 219),
            BorderThickness = new Thickness(1d),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += (_, _) => click();
        stack.Children.Add(button);
        return NewCard(stack);
    }

    private static void InstallNavigationWorkspace(
        StackPanel navigationStack,
        Border navigationCard,
        DashboardRefs refs)
    {
        var parent = VisualTreeHelper.GetParent(navigationCard) ?? LogicalTreeHelper.GetParent(navigationCard);
        if (parent is Panel oldPanel)
        {
            oldPanel.Children.Remove(navigationCard);
        }

        var workspace = new Grid();
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14d) });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270d) });

        navigationCard.Margin = new Thickness(0d);
        navigationCard.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(navigationCard, 0);
        workspace.Children.Add(navigationCard);

        var rail = BuildNavigationRail(refs);
        Grid.SetColumn(rail, 2);
        workspace.Children.Add(rail);
        navigationStack.Children.Add(workspace);
    }

    private static Border BuildNavigationRail(DashboardRefs refs)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = Text("TripStatus").ToUpperInvariant(),
            Foreground = Brush(91, 157, 194),
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 0d, 0d, 13d)
        });

        stack.Children.Add(BuildRailValue(Text("Line"), refs.NavLine, true));
        stack.Children.Add(BuildRailValue(Text("Destination"), refs.NavDestination, false));
        stack.Children.Add(BuildRailValue(Text("NextStop"), refs.NavNextStop, false));
        stack.Children.Add(BuildRailValue(Text("Street"), refs.NavStreet, false));

        var metricGrid = new Grid { Margin = new Thickness(0d, 7d, 0d, 0d) };
        metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8d) });
        metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        var speed = BuildMiniMetric(Text("Speed"), refs.NavSpeed);
        var delay = BuildMiniMetric(Text("Delay"), refs.NavDelay);
        Grid.SetColumn(speed, 0);
        Grid.SetColumn(delay, 2);
        metricGrid.Children.Add(speed);
        metricGrid.Children.Add(delay);
        stack.Children.Add(metricGrid);

        stack.Children.Add(new TextBlock
        {
            Text = Text("TelemetryNote"),
            Foreground = Brush(92, 119, 136),
            FontSize = 9d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 14d, 0d, 0d)
        });

        return new Border
        {
            Padding = new Thickness(16d),
            Background = Brush(7, 18, 25),
            BorderBrush = Brush(25, 51, 66),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Child = stack
        };
    }

    private static FrameworkElement BuildRailValue(string label, TextBlock value, bool emphasize)
    {
        var stack = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 13d) };
        stack.Children.Add(new TextBlock
        {
            Text = label.ToUpperInvariant(),
            Foreground = Brush(97, 126, 144),
            FontSize = 8.5d,
            FontWeight = FontWeights.Bold
        });
        value.Foreground = emphasize ? Brush(82, 196, 255) : Brushes.White;
        value.FontSize = emphasize ? 24d : 13d;
        value.FontWeight = emphasize ? FontWeights.Bold : FontWeights.SemiBold;
        value.TextWrapping = TextWrapping.Wrap;
        value.Margin = new Thickness(0d, 4d, 0d, 0d);
        stack.Children.Add(value);
        return stack;
    }

    private static Border BuildMiniMetric(string label, TextBlock value)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = label.ToUpperInvariant(),
            Foreground = Brush(97, 126, 144),
            FontSize = 8d,
            FontWeight = FontWeights.Bold
        });
        value.Foreground = Brushes.White;
        value.FontSize = 17d;
        value.FontWeight = FontWeights.Bold;
        value.Margin = new Thickness(0d, 4d, 0d, 0d);
        stack.Children.Add(value);
        return new Border
        {
            Padding = new Thickness(10d),
            Background = Brush(5, 14, 20),
            BorderBrush = Brush(23, 45, 58),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(9d),
            Child = stack
        };
    }

    private static void RefreshValues(DashboardRefs refs, VehicleTelemetry? telemetry)
    {
        var active = telemetry?.IsInGame == true;
        refs.HomeMap.Text = Safe(telemetry?.MapName, Text("NoActiveOperation"));
        refs.HomeService.Text = active
            ? string.Join("  •  ", new[] { telemetry?.Line, telemetry?.Route, telemetry?.DestinationName }
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            : Text("WaitingOmsi");
        if (string.IsNullOrWhiteSpace(refs.HomeService.Text))
        {
            refs.HomeService.Text = Text("ServiceNotDefined");
        }
        refs.HomeVehicle.Text = Safe(telemetry?.VehicleName, Text("NoVehicle"));
        refs.HomeState.Text = active ? "●  " + Text("Connected") : "○  " + Text("Waiting");

        refs.NavLine.Text = Safe(telemetry?.Line);
        refs.NavDestination.Text = Safe(telemetry?.DestinationName);
        refs.NavNextStop.Text = Safe(telemetry?.NextStopName);
        refs.NavStreet.Text = Safe(telemetry?.CurrentStreetName);
        refs.NavSpeed.Text = active ? $"{telemetry!.SpeedKph:0} km/h" : "—";
        refs.NavDelay.Text = FormatDelay(telemetry?.DelaySeconds);
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

    private static string Safe(string? value, string fallback = "—") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static void ShowPage(ScrollViewer target)
    {
        if (VisualTreeHelper.GetParent(target) is not Panel host)
        {
            target.Visibility = Visibility.Visible;
            return;
        }

        foreach (var child in host.Children.OfType<FrameworkElement>())
        {
            child.Visibility = ReferenceEquals(child, target) ? Visibility.Visible : Visibility.Hidden;
        }
    }

    private static Border NewCard(UIElement child) => new()
    {
        Padding = new Thickness(16d),
        Background = Brush(7, 18, 25),
        BorderBrush = Brush(25, 49, 63),
        BorderThickness = new Thickness(1d),
        CornerRadius = new CornerRadius(12d),
        Child = child
    };

    private static string Text(string key)
    {
        var lang = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        var pt = lang == "pt";
        var es = lang == "es";
        var de = lang == "de";
        var fr = lang == "fr";
        return key switch
        {
            "StartNavigation" => pt ? "Iniciar navegação" : es ? "Iniciar navegación" : de ? "Navigation starten" : fr ? "Démarrer la navigation" : "Start navigation",
            "StartNavigationBody" => pt ? "Veja rota, paradas e dados da viagem em um único painel." : "See route, stops and trip data in one workspace.",
            "OpenNavigation" => pt ? "Navegar" : es ? "Navegar" : de ? "Öffnen" : fr ? "Ouvrir" : "Open",
            "Multiplayer" => pt ? "Entrar / Criar sala" : es ? "Entrar / Crear sala" : de ? "Raum beitreten / erstellen" : fr ? "Rejoindre / créer une salle" : "Join / Create room",
            "MultiplayerBody" => pt ? "Jogue com outros motoristas e acompanhe a sessão em tempo real." : "Drive with other players and follow the session in real time.",
            "OpenMultiplayer" => pt ? "Multiplayer" : es ? "Multijugador" : de ? "Mehrspieler" : fr ? "Multijoueur" : "Multiplayer",
            "ContinueOperation" => pt ? "Continuar operação" : es ? "Continuar operación" : de ? "Betrieb fortsetzen" : fr ? "Continuer l’exploitation" : "Continue operation",
            "TripStatus" => pt ? "Viagem em andamento" : es ? "Viaje en curso" : de ? "Aktuelle Fahrt" : fr ? "Trajet en cours" : "Trip in progress",
            "Line" => pt ? "Linha" : es ? "Línea" : de ? "Linie" : fr ? "Ligne" : "Line",
            "Destination" => pt ? "Destino" : es ? "Destino" : de ? "Ziel" : fr ? "Destination" : "Destination",
            "NextStop" => pt ? "Próxima parada" : es ? "Próxima parada" : de ? "Nächste Haltestelle" : fr ? "Prochain arrêt" : "Next stop",
            "Street" => pt ? "Rua atual" : es ? "Calle actual" : de ? "Aktuelle Straße" : fr ? "Rue actuelle" : "Current street",
            "Speed" => pt ? "Velocidade" : es ? "Velocidad" : de ? "Geschwindigkeit" : fr ? "Vitesse" : "Speed",
            "Delay" => pt ? "Horário" : es ? "Horario" : de ? "Fahrplan" : fr ? "Horaire" : "Schedule",
            "TelemetryNote" => pt ? "ETA e distância só serão exibidos quando a telemetria fornecer dados reais para esses campos." : "ETA and distance will only appear when telemetry provides real values for them.",
            "NoActiveOperation" => pt ? "Nenhuma operação ativa" : es ? "Sin operación activa" : de ? "Kein aktiver Betrieb" : fr ? "Aucune exploitation active" : "No active operation",
            "WaitingOmsi" => pt ? "Aguardando serviço no OMSI" : es ? "Esperando servicio OMSI" : de ? "Warte auf OMSI-Dienst" : fr ? "En attente d’un service OMSI" : "Waiting for OMSI service",
            "ServiceNotDefined" => pt ? "Linha/rota ainda não definidas" : "Line/route not defined yet",
            "NoVehicle" => pt ? "Nenhum ônibus ativo" : "No active bus",
            "Connected" => pt ? "OMSI conectado" : es ? "OMSI conectado" : de ? "OMSI verbunden" : fr ? "OMSI connecté" : "OMSI connected",
            "Waiting" => pt ? "Aguardando OMSI" : es ? "Esperando OMSI" : de ? "Warte auf OMSI" : fr ? "En attente d’OMSI" : "Waiting for OMSI",
            "OnTime" => pt ? "No horário" : es ? "En horario" : de ? "Pünktlich" : fr ? "À l’heure" : "On time",
            _ => key
        };
    }

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject? current = child;
        while (current is not null)
        {
            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
            if (current is T match)
            {
                return match;
            }
        }
        return null;
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

    private sealed class DashboardRefs
    {
        public TextBlock HomeMap { get; } = new();
        public TextBlock HomeService { get; } = new();
        public TextBlock HomeVehicle { get; } = new();
        public TextBlock HomeState { get; } = new();
        public TextBlock NavLine { get; } = new();
        public TextBlock NavDestination { get; } = new();
        public TextBlock NavNextStop { get; } = new();
        public TextBlock NavStreet { get; } = new();
        public TextBlock NavSpeed { get; } = new();
        public TextBlock NavDelay { get; } = new();
    }
}