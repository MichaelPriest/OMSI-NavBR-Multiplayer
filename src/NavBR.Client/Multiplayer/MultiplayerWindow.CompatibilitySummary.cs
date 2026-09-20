using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private Border? _compatibilitySummaryCard;
    private TextBlock? _compatibilitySummaryState;
    private TextBlock? _compatibilitySummaryDetail;
    private DispatcherTimer? _compatibilitySummaryTimer;

    [ModuleInitializer]
    internal static void InitializeCompatibilitySummaryBootstrap()
    {
        EventManager.RegisterClassHandler(
            typeof(MultiplayerWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(CompatibilitySummaryWindowLoaded));
    }

    private static void CompatibilitySummaryWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MultiplayerWindow window)
        {
            return;
        }

        _ = window.Dispatcher.BeginInvoke(
            new Action(window.InstallCompatibilitySummary),
            DispatcherPriority.ApplicationIdle);
    }

    private void InstallCompatibilitySummary()
    {
        if (_compatibilitySummaryCard is not null || PlayersListBox.Parent is not DockPanel host)
        {
            return;
        }

        _compatibilitySummaryState = new TextBlock
        {
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _compatibilitySummaryDetail = new TextBlock
        {
            Foreground = CompatibilityBrush(129, 153, 170),
            FontSize = 9.2d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 5d, 0d, 0d)
        };

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock
        {
            Text = CompatibilityText(
                "COMPATIBILIDADE DA SALA",
                "ROOM COMPATIBILITY",
                "COMPATIBILIDAD DE SALA",
                "RAUMKOMPATIBILITÄT",
                "COMPATIBILITÉ DE LA SALLE"),
            Foreground = CompatibilityBrush(122, 151, 171),
            FontSize = 8.5d,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(title, 0);
        header.Children.Add(title);

        var stateHost = new Border
        {
            CornerRadius = new CornerRadius(8d),
            Padding = new Thickness(7d, 3d, 7d, 3d),
            BorderThickness = new Thickness(1d),
            Child = _compatibilitySummaryState
        };
        stateHost.SetValue(FrameworkElement.TagProperty, "NavBRCompatibilityStateHost");
        Grid.SetColumn(stateHost, 1);
        header.Children.Add(stateHost);

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(_compatibilitySummaryDetail);

        _compatibilitySummaryCard = new Border
        {
            Background = CompatibilityBrush(7, 22, 32),
            BorderBrush = CompatibilityBrush(27, 50, 67),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(11d),
            Padding = new Thickness(10d),
            Margin = new Thickness(0d, 0d, 0d, 9d),
            Child = body
        };
        DockPanel.SetDock(_compatibilitySummaryCard, Dock.Top);
        host.Children.Insert(Math.Min(1, host.Children.Count), _compatibilitySummaryCard);

        _compatibilitySummaryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(750d)
        };
        _compatibilitySummaryTimer.Tick += CompatibilitySummaryTimer_Tick;
        _compatibilitySummaryTimer.Start();
        Closed += CompatibilitySummaryWindowClosed;
        RenderCompatibilitySummary();
    }

    private void CompatibilitySummaryTimer_Tick(object? sender, EventArgs e) => RenderCompatibilitySummary();

    private void RenderCompatibilitySummary()
    {
        if (_compatibilitySummaryCard is null ||
            _compatibilitySummaryState is null ||
            _compatibilitySummaryDetail is null)
        {
            return;
        }

        _compatibilitySummaryCard.Visibility = _client.IsConnected
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (!_client.IsConnected)
        {
            return;
        }

        var remotes = _players.Values
            .Where(player => !string.Equals(player.PlayerId, _settings.PlayerId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(player => player.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        if (remotes.Length == 0)
        {
            SetCompatibilitySummaryState(
                CompatibilityText("AGUARDANDO", "WAITING", "ESPERANDO", "WARTET", "EN ATTENTE"),
                CompatibilityBrush(114, 157, 187),
                CompatibilityText(
                    "A compatibilidade aparecerá quando outro motorista entrar na sala.",
                    "Compatibility appears when another driver joins the room.",
                    "La compatibilidad aparecerá cuando otro conductor entre en la sala.",
                    "Die Kompatibilität wird angezeigt, sobald ein weiterer Fahrer den Raum betritt.",
                    "La compatibilité apparaîtra lorsqu’un autre conducteur rejoindra la salle."));
            return;
        }

        var localManifest = OmsiCompatibilityManifestFactory.Create(
            _telemetrySource(),
            _activeMapSource());
        // Different players are allowed to drive different buses/HOFs.
        // Physical rendering resolves the remote vehicle asset by its own
        // fingerprint, so enabling Remote 3D must not turn a legitimate
        // vehicle/HOF difference into a room-level blocker.
        const bool requireSameVehicleDefinition = false;
        var reports = remotes
            .Select(player => new PlayerCompatibility(
                player,
                OmsiCompatibilityEvaluator.Compare(
                    localManifest,
                    player.Compatibility,
                    requireSameVehicleDefinition)))
            .ToArray();

        var blocking = reports.Count(item => item.Report.HasBlockingIssues);
        var warnings = reports.Count(item =>
            !item.Report.HasBlockingIssues &&
            item.Report.Issues.Any(issue => issue.Severity == CompatibilityIssueSeverity.Warning));
        var partial = reports.Count(item =>
            !item.Report.HasBlockingIssues &&
            !item.Report.Issues.Any(issue => issue.Severity == CompatibilityIssueSeverity.Warning) &&
            item.Report.Issues.Any(issue => IsUnknownCompatibilityIssue(issue.Code)));

        var affectedAreas = reports
            .SelectMany(item => item.Report.Issues)
            .Where(issue => issue.Severity != CompatibilityIssueSeverity.Info || IsUnknownCompatibilityIssue(issue.Code))
            .Select(issue => CompatibilityArea(issue.Code))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Take(4)
            .ToArray();

        var detail = CompatibilityDetail(remotes.Length, blocking, warnings, partial, affectedAreas);
        if (blocking > 0)
        {
            SetCompatibilitySummaryState(
                CompatibilityText("BLOQUEIO", "BLOCKED", "BLOQUEO", "BLOCKIERT", "BLOQUÉ"),
                CompatibilityBrush(232, 91, 91),
                detail);
        }
        else if (warnings > 0)
        {
            SetCompatibilitySummaryState(
                CompatibilityText("ATENÇÃO", "WARNING", "ATENCIÓN", "ACHTUNG", "ATTENTION"),
                CompatibilityBrush(237, 184, 75),
                detail);
        }
        else if (partial > 0)
        {
            SetCompatibilitySummaryState(
                CompatibilityText("PARCIAL", "PARTIAL", "PARCIAL", "TEILWEISE", "PARTIEL"),
                CompatibilityBrush(111, 174, 218),
                detail);
        }
        else
        {
            SetCompatibilitySummaryState(
                CompatibilityText("COMPATÍVEL", "COMPATIBLE", "COMPATIBLE", "KOMPATIBEL", "COMPATIBLE"),
                CompatibilityBrush(82, 215, 145),
                detail);
        }
    }

    private void SetCompatibilitySummaryState(string label, SolidColorBrush accent, string detail)
    {
        if (_compatibilitySummaryState is null || _compatibilitySummaryDetail is null)
        {
            return;
        }

        _compatibilitySummaryState.Text = label;
        _compatibilitySummaryState.Foreground = accent;
        _compatibilitySummaryDetail.Text = detail;

        if (_compatibilitySummaryState.Parent is Border host)
        {
            host.BorderBrush = accent;
            host.Background = new SolidColorBrush(Color.FromArgb(35, accent.Color.R, accent.Color.G, accent.Color.B));
        }
    }

    private static string CompatibilityDetail(
        int players,
        int blocking,
        int warnings,
        int partial,
        IReadOnlyList<string> affectedAreas)
    {
        var playerText = CompatibilityText(
            $"{players} motorista(s) remoto(s)",
            $"{players} remote driver(s)",
            $"{players} conductor(es) remoto(s)",
            $"{players} entfernte Fahrer",
            $"{players} conducteur(s) distant(s)");
        var statusParts = new List<string>();
        if (blocking > 0)
        {
            statusParts.Add(CompatibilityText(
                $"{blocking} bloqueio(s)",
                $"{blocking} blocking issue(s)",
                $"{blocking} bloqueo(s)",
                $"{blocking} Blockierung(en)",
                $"{blocking} blocage(s)"));
        }
        if (warnings > 0)
        {
            statusParts.Add(CompatibilityText(
                $"{warnings} atenção",
                $"{warnings} warning(s)",
                $"{warnings} alerta(s)",
                $"{warnings} Warnung(en)",
                $"{warnings} avertissement(s)"));
        }
        if (partial > 0)
        {
            statusParts.Add(CompatibilityText(
                $"{partial} parcial(is)",
                $"{partial} partial",
                $"{partial} parcial(es)",
                $"{partial} teilweise",
                $"{partial} partiel(s)"));
        }

        var status = statusParts.Count == 0
            ? CompatibilityText("sem divergências detectadas", "no detected differences", "sin diferencias detectadas", "keine Unterschiede erkannt", "aucune différence détectée")
            : string.Join(" • ", statusParts);
        var areas = affectedAreas.Count == 0
            ? string.Empty
            : $" • {CompatibilityText("Áreas", "Areas", "Áreas", "Bereiche", "Zones")}: {string.Join(", ", affectedAreas)}";
        return $"{playerText} • {status}{areas}";
    }

    private static bool IsUnknownCompatibilityIssue(string code) =>
        code.Contains("unknown", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(code, "manifest-missing", StringComparison.OrdinalIgnoreCase);

    private static string CompatibilityArea(string code)
    {
        if (code.StartsWith("map", StringComparison.OrdinalIgnoreCase))
        {
            return CompatibilityText("mapa", "map", "mapa", "Karte", "carte");
        }
        if (code.StartsWith("vehicle", StringComparison.OrdinalIgnoreCase))
        {
            return CompatibilityText("ônibus", "vehicle", "autobús", "Bus", "bus");
        }
        if (code.StartsWith("hof", StringComparison.OrdinalIgnoreCase))
        {
            return "HOF";
        }
        if (code.StartsWith("plugin", StringComparison.OrdinalIgnoreCase))
        {
            return CompatibilityText("protocolo", "protocol", "protocolo", "Protokoll", "protocole");
        }
        if (code.StartsWith("omsi", StringComparison.OrdinalIgnoreCase))
        {
            return "OMSI";
        }
        if (string.Equals(code, "manifest-missing", StringComparison.OrdinalIgnoreCase))
        {
            return CompatibilityText("manifesto", "manifest", "manifiesto", "Manifest", "manifeste");
        }
        return string.Empty;
    }

    private void CompatibilitySummaryWindowClosed(object? sender, EventArgs e)
    {
        if (_compatibilitySummaryTimer is not null)
        {
            _compatibilitySummaryTimer.Stop();
            _compatibilitySummaryTimer.Tick -= CompatibilitySummaryTimer_Tick;
            _compatibilitySummaryTimer = null;
        }
        Closed -= CompatibilitySummaryWindowClosed;
    }

    private static SolidColorBrush CompatibilityBrush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));

    private static string CompatibilityText(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    private sealed record PlayerCompatibility(
        PlayerPresence Player,
        RoomCompatibilityReport Report);
}
