using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Maps;
using NavBR.Client.Multiplayer;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Windows;

/// <summary>
/// Replaces Figma-only preview content with live OMSI/NavBR data. The visual
/// shell stays presentation-focused while this installer owns refresh and
/// compatibility logic.
/// </summary>
internal static class Alpha12FigmaLiveDataInstaller
{
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        var navigationBody = FindCardBody(window, "ROTA ATIVA");
        var roomsBody = FindCardBody(window, "SALAS");
        if (navigationBody is null && roomsBody is null)
        {
            Installed.Remove(window);
            return;
        }

        var timers = new List<DispatcherTimer>();
        var cancellation = new CancellationTokenSource();
        PublicRoomDirectoryClient? directory = null;

        if (navigationBody is not null)
        {
            timers.Add(InstallNavigationSnapshot(window, navigationBody));
        }

        if (roomsBody is not null)
        {
            directory = new PublicRoomDirectoryClient();
            timers.Add(InstallPublicRooms(window, roomsBody, directory, cancellation.Token));
        }

        window.Closed += (_, _) =>
        {
            foreach (var timer in timers)
            {
                timer.Stop();
            }
            cancellation.Cancel();
            cancellation.Dispose();
            directory?.Dispose();
            Installed.Remove(window);
        };
    }

    private static DispatcherTimer InstallNavigationSnapshot(MainWindow window, StackPanel body)
    {
        KeepHeadingOnly(body);

        var routeState = Text(T("Aguardando rota real do OMSI", "Waiting for the real OMSI route", "Esperando la ruta real de OMSI", "Warte auf die echte OMSI-Route", "En attente de l’itinéraire OMSI réel"), 12d, Muted(), FontWeights.SemiBold);
        routeState.Margin = new Thickness(0d, 12d, 0d, 18d);
        body.Children.Add(routeState);

        var nextStop = Value("—");
        var stopDistance = Value("—");
        var remaining = Value("—");
        var currentStreet = Value("—");
        var maneuver = Value("—");
        var progressValue = Value("—");

        var firstRow = TwoColumn(
            DataBlock(T("Próxima parada", "Next stop", "Próxima parada", "Nächste Haltestelle", "Prochain arrêt"), nextStop),
            DataBlock(T("Distância até a parada", "Distance to stop", "Distancia a la parada", "Entfernung zur Haltestelle", "Distance jusqu’à l’arrêt"), stopDistance));
        body.Children.Add(firstRow);

        var secondRow = TwoColumn(
            DataBlock(T("Distância restante", "Remaining distance", "Distancia restante", "Verbleibende Strecke", "Distance restante"), remaining),
            DataBlock(T("Rua atual", "Current street", "Calle actual", "Aktuelle Straße", "Rue actuelle"), currentStreet));
        secondRow.Margin = new Thickness(0d, 12d, 0d, 0d);
        body.Children.Add(secondRow);

        var maneuverBlock = DataBlock(T("Próxima manobra", "Next maneuver", "Próxima maniobra", "Nächstes Manöver", "Prochaine manœuvre"), maneuver);
        maneuverBlock.Margin = new Thickness(0d, 12d, 0d, 0d);
        body.Children.Add(maneuverBlock);

        var progressHeader = new Grid { Margin = new Thickness(0d, 18d, 0d, 7d) };
        progressHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        progressHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        progressHeader.Children.Add(Text(T("Progresso da rota", "Route progress", "Progreso de la ruta", "Routenfortschritt", "Progression de l’itinéraire"), 10d, Muted(), FontWeights.SemiBold));
        Grid.SetColumn(progressValue, 1);
        progressHeader.Children.Add(progressValue);
        body.Children.Add(progressHeader);

        var progress = new ProgressBar
        {
            Minimum = 0d,
            Maximum = 100d,
            Height = 7d,
            Value = 0d,
            Foreground = Accent(),
            Background = Brush(28, 42, 51),
            BorderThickness = new Thickness(0d)
        };
        body.Children.Add(progress);

        var refreshing = false;
        async Task RefreshAsync()
        {
            if (refreshing || !window.IsLoaded)
            {
                return;
            }

            refreshing = true;
            try
            {
                var snapshot = await Task.Run(window.GetNavigationSnapshotForAlpha12);
                if (!snapshot.RouteAvailable)
                {
                    routeState.Text = T("Aguardando geometria da rota do OMSI", "Waiting for OMSI route geometry", "Esperando la geometría de ruta de OMSI", "Warte auf OMSI-Routengeometrie", "En attente de la géométrie de l’itinéraire OMSI");
                    routeState.Foreground = Warning();
                    nextStop.Text = Safe(snapshot.NextStopName);
                    stopDistance.Text = "—";
                    remaining.Text = "—";
                    currentStreet.Text = Safe(snapshot.CurrentStreetName);
                    maneuver.Text = "—";
                    progress.Value = 0d;
                    progressValue.Text = "—";
                    return;
                }

                routeState.Text = snapshot.IsOnRoute
                    ? T("Na rota", "On route", "En ruta", "Auf Route", "Sur l’itinéraire")
                    : $"{T("Fora da rota", "Off route", "Fuera de ruta", "Abseits der Route", "Hors itinéraire")} • {FormatDistance(snapshot.OffRouteDistanceMeters)}";
                routeState.Foreground = snapshot.IsOnRoute ? Success() : Error();
                nextStop.Text = Safe(snapshot.NextStopName);
                stopDistance.Text = snapshot.DistanceToNextStopMeters.HasValue
                    ? FormatDistance(snapshot.DistanceToNextStopMeters.Value)
                    : "—";
                remaining.Text = FormatDistance(snapshot.DistanceRemainingMeters);
                currentStreet.Text = Safe(snapshot.CurrentStreetName);
                maneuver.Text = FormatManeuver(snapshot.Maneuver, snapshot.DistanceToManeuverMeters);
                progress.Value = Math.Clamp(snapshot.RouteProgressPercent, 0d, 100d);
                progressValue.Text = $"{snapshot.RouteProgressPercent:0}%";
            }
            catch
            {
                routeState.Text = T("Navegação temporariamente indisponível", "Navigation temporarily unavailable", "Navegación temporalmente no disponible", "Navigation vorübergehend nicht verfügbar", "Navigation temporairement indisponible");
                routeState.Foreground = Error();
            }
            finally
            {
                refreshing = false;
            }
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650d) };
        timer.Tick += async (_, _) => await RefreshAsync();
        timer.Start();
        _ = RefreshAsync();
        return timer;
    }

    private static DispatcherTimer InstallPublicRooms(
        MainWindow window,
        StackPanel body,
        PublicRoomDirectoryClient directory,
        CancellationToken cancellationToken)
    {
        KeepHeadingOnly(body);

        var status = Text(T("Consultando salas públicas…", "Loading public rooms…", "Consultando salas públicas…", "Öffentliche Räume werden geladen…", "Chargement des salons publics…"), 11d, Muted(), FontWeights.Normal);
        status.Margin = new Thickness(0d, 10d, 0d, 12d);
        body.Children.Add(status);

        var roomList = new StackPanel();
        body.Children.Add(roomList);

        var actions = new WrapPanel { Margin = new Thickness(0d, 14d, 0d, 0d) };
        var refreshButton = SecondaryButton(T("Atualizar salas", "Refresh rooms", "Actualizar salas", "Räume aktualisieren", "Actualiser les salons"));
        var openCentral = SecondaryButton(T("Abrir Central Multiplayer", "Open Multiplayer Center", "Abrir Central Multiplayer", "Multiplayer-Zentrale öffnen", "Ouvrir la centrale multijoueur"));
        openCentral.Margin = new Thickness(10d, 0d, 0d, 0d);
        openCentral.Click += (_, _) => window.MultiplayerButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        actions.Children.Add(refreshButton);
        actions.Children.Add(openCentral);
        body.Children.Add(actions);

        var refreshing = false;
        async Task RefreshAsync()
        {
            if (refreshing || cancellationToken.IsCancellationRequested || !window.IsLoaded)
            {
                return;
            }

            refreshing = true;
            refreshButton.IsEnabled = false;
            try
            {
                var settings = MultiplayerSettingsStore.Load();
                status.Text = $"{T("Servidor", "Server", "Servidor", "Server", "Serveur")}: {settings.ServerUrl}";
                var rooms = await directory.GetRoomsAsync(settings.ServerUrl, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                roomList.Children.Clear();
                if (rooms.Count == 0)
                {
                    roomList.Children.Add(EmptyState(T(
                        "Nenhuma sala pública ativa. Salas privadas não aparecem aqui.",
                        "No public rooms are active. Private rooms are not shown here.",
                        "No hay salas públicas activas. Las salas privadas no aparecen aquí.",
                        "Keine öffentlichen Räume aktiv. Private Räume werden hier nicht angezeigt.",
                        "Aucun salon public actif. Les salons privés ne sont pas affichés ici.")));
                    return;
                }

                var local = window.GetCompatibilityManifestForAlpha12();
                foreach (var room in rooms
                             .OrderByDescending(item => item.PlayerCount)
                             .ThenBy(item => item.RoomId, StringComparer.CurrentCultureIgnoreCase)
                             .Take(3))
                {
                    var card = LiveRoomCard(room, local);
                    card.Margin = new Thickness(0d, roomList.Children.Count == 0 ? 0d : 10d, 0d, 0d);
                    roomList.Children.Add(card);
                }

                status.Text = $"{rooms.Count} {T("sala(s) pública(s)", "public room(s)", "sala(s) pública(s)", "öffentliche Räume", "salon(s) public(s)")} • {settings.ServerUrl}";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                roomList.Children.Clear();
                roomList.Children.Add(EmptyState(T(
                    "Diretório de salas indisponível. A Central Multiplayer continua acessível.",
                    "Room directory unavailable. The Multiplayer Center is still available.",
                    "Directorio de salas no disponible. La Central Multiplayer sigue disponible.",
                    "Raumverzeichnis nicht verfügbar. Die Multiplayer-Zentrale bleibt verfügbar.",
                    "Répertoire des salons indisponible. La centrale multijoueur reste accessible.")));
                status.Text = $"{T("Falha ao consultar", "Could not query", "Error al consultar", "Abfrage fehlgeschlagen", "Échec de la requête")}: {ex.Message}";
            }
            finally
            {
                refreshing = false;
                refreshButton.IsEnabled = true;
            }
        }

        refreshButton.Click += async (_, _) => await RefreshAsync();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20d) };
        timer.Tick += async (_, _) => await RefreshAsync();
        timer.Start();
        _ = RefreshAsync();
        return timer;
    }

    private static Border LiveRoomCard(PublicRoomSummary room, OmsiCompatibilityManifest local)
    {
        var remote = new OmsiCompatibilityManifest(
            room.OmsiVersion,
            room.NavBRVersion,
            room.MapName,
            room.MapCompatibilityId,
            room.VehiclePath,
            room.VehicleCompatibilityId,
            room.HofName,
            room.HofCompatibilityId,
            room.PluginProtocolVersion,
            null,
            null);

        var report = OmsiCompatibilityEvaluator.Compare(local, remote);
        var missingMap = string.IsNullOrWhiteSpace(room.MapName);
        var hasWarnings = report.Issues.Any(issue => issue.Severity == CompatibilityIssueSeverity.Warning);
        var compatibilityText = missingMap
            ? T("BLOQUEADO • mapa não informado", "BLOCKED • map not reported", "BLOQUEADO • mapa no informado", "GESPERRT • Karte nicht gemeldet", "BLOQUÉ • carte non indiquée")
            : report.HasBlockingIssues
                ? T("ATENÇÃO • requisitos incompatíveis", "ATTENTION • incompatible requirements", "ATENCIÓN • requisitos incompatibles", "ACHTUNG • inkompatible Anforderungen", "ATTENTION • prérequis incompatibles")
                : hasWarnings
                    ? T("PARCIAL • verifique os avisos", "PARTIAL • check warnings", "PARCIAL • comprueba los avisos", "TEILWEISE • Hinweise prüfen", "PARTIEL • vérifier les avertissements")
                    : T("COMPATÍVEL", "COMPATIBLE", "COMPATIBLE", "KOMPATIBEL", "COMPATIBLE");
        var compatibilityBrush = missingMap || report.HasBlockingIssues
            ? Error()
            : hasWarnings ? Warning() : Success();

        var stack = new StackPanel();
        stack.Children.Add(Text(room.RoomId, 15d, White(), FontWeights.SemiBold));
        stack.Children.Add(Text(compatibilityText, 9.5d, compatibilityBrush, FontWeights.Bold, new Thickness(0d, 8d, 0d, 0d)));
        stack.Children.Add(Text($"{T("MAPA OBRIGATÓRIO", "REQUIRED MAP", "MAPA OBLIGATORIO", "ERFORDERLICHE KARTE", "CARTE REQUISE")}: {Safe(room.MapName)}", 11.5d, White(), FontWeights.Medium, new Thickness(0d, 7d, 0d, 0d)));
        stack.Children.Add(Text($"{room.PlayerCount} {T("jogador(es)", "player(s)", "jugador(es)", "Spieler", "joueur(s)")} • OMSI {Safe(room.OmsiVersion)} • NavBR {Safe(room.NavBRVersion)}", 10.5d, Muted(), FontWeights.Normal, new Thickness(0d, 5d, 0d, 0d)));
        if (!string.IsNullOrWhiteSpace(room.VehiclePath) || !string.IsNullOrWhiteSpace(room.HofName))
        {
            stack.Children.Add(Text($"{T("Ônibus", "Bus", "Autobús", "Bus", "Bus")}: {Safe(room.VehiclePath)} • HOF: {Safe(room.HofName)}", 10d, Muted(), FontWeights.Normal, new Thickness(0d, 4d, 0d, 0d)));
        }

        return new Border
        {
            Padding = new Thickness(14d),
            Background = Brush(13, 26, 36),
            BorderBrush = compatibilityBrush,
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(10d),
            Child = stack
        };
    }

    private static void KeepHeadingOnly(StackPanel body)
    {
        while (body.Children.Count > 1)
        {
            body.Children.RemoveAt(1);
        }
    }

    private static StackPanel? FindCardBody(DependencyObject root, string title)
    {
        return Enumerate<Border>(root)
            .Select(border => border.Child as StackPanel)
            .FirstOrDefault(stack => stack is not null &&
                stack.Children.OfType<TextBlock>().FirstOrDefault()?.Text.Equals(title, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static Border DataBlock(string label, TextBlock value)
    {
        var stack = new StackPanel();
        stack.Children.Add(Text(label, 9.5d, Muted(), FontWeights.SemiBold));
        value.Margin = new Thickness(0d, 5d, 0d, 0d);
        stack.Children.Add(value);
        return new Border
        {
            Padding = new Thickness(12d),
            Background = Brush(13, 26, 36),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(9d),
            Child = stack
        };
    }

    private static Grid TwoColumn(UIElement left, UIElement right)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private static Border EmptyState(string text) => new()
    {
        Padding = new Thickness(14d),
        Background = Brush(13, 26, 36),
        BorderBrush = Brush(28, 42, 51),
        BorderThickness = new Thickness(1d),
        CornerRadius = new CornerRadius(10d),
        Child = Text(text, 11d, Muted(), FontWeights.Normal)
    };

    private static Button SecondaryButton(string text) => new()
    {
        Content = text,
        MinHeight = 36d,
        Padding = new Thickness(14d, 7d, 14d, 7d),
        Background = Brush(13, 26, 36),
        Foreground = White(),
        BorderBrush = Brush(28, 42, 51),
        BorderThickness = new Thickness(1d),
        FontSize = 11.5d,
        FontWeight = FontWeights.SemiBold,
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static TextBlock Value(string text) => Text(text, 13d, White(), FontWeights.SemiBold);

    private static TextBlock Text(string value, double size, Brush color, FontWeight weight, Thickness? margin = null) => new()
    {
        Text = value,
        FontSize = size,
        Foreground = color,
        FontWeight = weight,
        TextWrapping = TextWrapping.Wrap,
        Margin = margin ?? new Thickness(0d)
    };

    private static string FormatDistance(double meters) => meters >= 1000d
        ? $"{meters / 1000d:0.0} km"
        : $"{Math.Max(0d, meters):0} m";

    private static string FormatManeuver(NavBRManeuverKind maneuver, double? distance)
    {
        var label = maneuver switch
        {
            NavBRManeuverKind.SlightLeft => T("↖ Leve à esquerda", "↖ Slight left", "↖ Leve a la izquierda", "↖ Leicht links", "↖ Légèrement à gauche"),
            NavBRManeuverKind.Left => T("← Vire à esquerda", "← Turn left", "← Gira a la izquierda", "← Links abbiegen", "← Tournez à gauche"),
            NavBRManeuverKind.SharpLeft => T("↙ Curva fechada à esquerda", "↙ Sharp left", "↙ Giro cerrado a la izquierda", "↙ Scharf links", "↙ Virage serré à gauche"),
            NavBRManeuverKind.SlightRight => T("↗ Leve à direita", "↗ Slight right", "↗ Leve a la derecha", "↗ Leicht rechts", "↗ Légèrement à droite"),
            NavBRManeuverKind.Right => T("→ Vire à direita", "→ Turn right", "→ Gira a la derecha", "→ Rechts abbiegen", "→ Tournez à droite"),
            NavBRManeuverKind.SharpRight => T("↘ Curva fechada à direita", "↘ Sharp right", "↘ Giro cerrado a la derecha", "↘ Scharf rechts", "↘ Virage serré à droite"),
            NavBRManeuverKind.RejoinRoute => T("↺ Retorne à rota", "↺ Rejoin route", "↺ Vuelve a la ruta", "↺ Zur Route zurückkehren", "↺ Rejoindre l’itinéraire"),
            _ => T("↑ Siga a rota", "↑ Follow the route", "↑ Sigue la ruta", "↑ Route folgen", "↑ Suivre l’itinéraire")
        };
        return distance.HasValue ? $"{label} • {FormatDistance(distance.Value)}" : label;
    }

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string T(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    private static IEnumerable<TNode> Enumerate<TNode>(DependencyObject root) where TNode : DependencyObject
    {
        if (root is TNode match)
        {
            yield return match;
        }
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in Enumerate<TNode>(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }

    private static SolidColorBrush White() => Brush(218, 230, 238);
    private static SolidColorBrush Muted() => Brush(151, 171, 185);
    private static SolidColorBrush Accent() => Brush(113, 198, 255);
    private static SolidColorBrush Success() => Brush(56, 201, 140);
    private static SolidColorBrush Warning() => Brush(242, 184, 75);
    private static SolidColorBrush Error() => Brush(239, 91, 100);
    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
