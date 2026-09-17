using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private readonly Dictionary<string, DateTimeOffset> _playerCardTelemetryReceivedAt =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _playerCardVoiceActivityAt =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _playerCardsInstalled;
    private StackPanel? _playerCardsPanel;
    private DispatcherTimer? _playerCardsTimer;

    [ModuleInitializer]
    internal static void InitializePlayerCardsBootstrap()
    {
        EventManager.RegisterClassHandler(
            typeof(MultiplayerWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(PlayerCardsWindowLoaded));
    }

    private static void PlayerCardsWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MultiplayerWindow window)
        {
            return;
        }

        _ = window.Dispatcher.BeginInvoke(
            new Action(window.InstallPlayerCards),
            DispatcherPriority.ContextIdle);
    }

    private void InstallPlayerCards()
    {
        if (_playerCardsInstalled || PlayersListBox.Parent is not DockPanel host)
        {
            return;
        }

        _playerCardsInstalled = true;
        PlayersListBox.Visibility = Visibility.Collapsed;

        _playerCardsPanel = new StackPanel
        {
            Margin = new Thickness(0d, 0d, 2d, 0d)
        };

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0d),
            Content = _playerCardsPanel
        };
        host.Children.Add(scroll);

        RemoteTelemetryReceived += PlayerCards_RemoteTelemetryReceived;
        RemotePlayerLeft += PlayerCards_RemotePlayerLeft;
        RemotePlayersReset += PlayerCards_RemotePlayersReset;
        RemoteSpeakerActive += PlayerCards_RemoteSpeakerActive;

        _playerCardsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500d)
        };
        _playerCardsTimer.Tick += PlayerCardsTimer_Tick;
        _playerCardsTimer.Start();

        Closed += PlayerCards_WindowClosed;
        RenderPlayerCards();
    }

    private void PlayerCards_RemoteTelemetryReceived(PlayerTelemetryFrame frame)
    {
        _playerCardTelemetryReceivedAt[frame.Player.PlayerId] = DateTimeOffset.UtcNow;
        RenderPlayerCards();
    }

    private void PlayerCards_RemotePlayerLeft(string playerId)
    {
        _playerCardTelemetryReceivedAt.Remove(playerId);
        _playerCardVoiceActivityAt.Remove(playerId);
        RenderPlayerCards();
    }

    private void PlayerCards_RemotePlayersReset()
    {
        _playerCardTelemetryReceivedAt.Clear();
        _playerCardVoiceActivityAt.Clear();
        RenderPlayerCards();
    }

    private void PlayerCards_RemoteSpeakerActive(string playerId, string displayName)
    {
        _playerCardVoiceActivityAt[playerId] = DateTimeOffset.UtcNow;
        RenderPlayerCards();
    }

    private void PlayerCardsTimer_Tick(object? sender, EventArgs e) => RenderPlayerCards();

    private void PlayerCards_WindowClosed(object? sender, EventArgs e)
    {
        if (_playerCardsTimer is not null)
        {
            _playerCardsTimer.Stop();
            _playerCardsTimer.Tick -= PlayerCardsTimer_Tick;
            _playerCardsTimer = null;
        }

        RemoteTelemetryReceived -= PlayerCards_RemoteTelemetryReceived;
        RemotePlayerLeft -= PlayerCards_RemotePlayerLeft;
        RemotePlayersReset -= PlayerCards_RemotePlayersReset;
        RemoteSpeakerActive -= PlayerCards_RemoteSpeakerActive;
        Closed -= PlayerCards_WindowClosed;
    }

    private void RenderPlayerCards()
    {
        var panel = _playerCardsPanel;
        if (panel is null)
        {
            return;
        }

        panel.Children.Clear();

        if (_client.IsConnected)
        {
            panel.Children.Add(BuildSessionNetworkQualityCard(SessionNetworkQualityFeed.Snapshot()));
        }

        var localTelemetry = _telemetrySource();
        var localMap = localTelemetry?.MapName;
        var localCompatibilityId = _activeMapSource()?.CompatibilityId;
        var now = DateTimeOffset.UtcNow;

        var players = _players.Values
            .OrderBy(player => string.Equals(player.PlayerId, _settings.PlayerId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(player => player.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        if (players.Length == 0)
        {
            panel.Children.Add(BuildPlayerCardsEmptyState());
            return;
        }

        foreach (var player in players)
        {
            var isLocal = string.Equals(player.PlayerId, _settings.PlayerId, StringComparison.OrdinalIgnoreCase);
            VehicleTelemetry? telemetry;
            DateTimeOffset? receivedAt;

            if (isLocal)
            {
                telemetry = localTelemetry;
                receivedAt = telemetry is null ? null : now;
            }
            else
            {
                _remoteTelemetry.TryGetValue(player.PlayerId, out telemetry);
                receivedAt = _playerCardTelemetryReceivedAt.TryGetValue(player.PlayerId, out var seenAt)
                    ? seenAt
                    : null;
            }

            var voiceActive = isLocal
                ? _voiceChat.IsPushToTalkActive
                : _playerCardVoiceActivityAt.TryGetValue(player.PlayerId, out var voiceAt) &&
                  now - voiceAt <= TimeSpan.FromSeconds(1.35d);

            var distance = GetDistanceText(
                localTelemetry,
                telemetry,
                localMap,
                player.MapName,
                localCompatibilityId,
                player.MapCompatibilityId);

            panel.Children.Add(BuildPlayerCard(
                player,
                telemetry,
                receivedAt,
                distance,
                isLocal,
                voiceActive,
                now));
        }
    }

    private FrameworkElement BuildSessionNetworkQualityCard(SessionNetworkQualitySnapshot quality)
    {
        var (accent, label) = quality.Level switch
        {
            SessionNetworkQualityLevel.Good => (PlayerCardBrush(82, 215, 145), PlayerCardText("Rede boa", "Good network", "Red buena", "Gute Verbindung", "Réseau bon")),
            SessionNetworkQualityLevel.Degraded => (PlayerCardBrush(237, 184, 75), PlayerCardText("Rede em atenção", "Network degraded", "Red degradada", "Verbindung eingeschränkt", "Réseau dégradé")),
            SessionNetworkQualityLevel.Poor => (PlayerCardBrush(232, 91, 91), PlayerCardText("Rede ruim", "Poor network", "Red deficiente", "Schlechte Verbindung", "Réseau faible")),
            _ => (PlayerCardBrush(104, 151, 184), PlayerCardText("Aferindo rede", "Measuring network", "Midiéndo la red", "Verbindung wird gemessen", "Mesure du réseau"))
        };

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = PlayerCardText("QUALIDADE DA SESSÃO", "SESSION QUALITY", "CALIDAD DE SESIÓN", "SITZUNGSQUALITÄT", "QUALITÉ DE SESSION"),
            Foreground = PlayerCardBrush(123, 153, 174),
            FontSize = 8.8d,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        });

        var state = new Border
        {
            Grid.ColumnProperty = 1,
            Background = new SolidColorBrush(Color.FromArgb(35, accent.Color.R, accent.Color.G, accent.Color.B)),
            BorderBrush = accent,
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(8d),
            Padding = new Thickness(7d, 3d, 7d, 3d),
            Child = new TextBlock
            {
                Text = label,
                Foreground = accent,
                FontSize = 8.5d,
                FontWeight = FontWeights.Bold
            }
        };
        Grid.SetColumn(state, 1);
        header.Children.Add(state);

        var metrics = new WrapPanel { Margin = new Thickness(0d, 9d, 0d, 0d) };
        metrics.Children.Add(PlayerMetricChip(
            "RTT",
            quality.RoundTripMs.HasValue ? $"{quality.RoundTripMs.Value:F0} ms" : "—"));
        metrics.Children.Add(PlayerMetricChip(
            PlayerCardText("JITTER", "JITTER", "JITTER", "JITTER", "JITTER"),
            quality.JitterMs.HasValue ? $"{quality.JitterMs.Value:F0} ms" : "—"));
        metrics.Children.Add(PlayerMetricChip(
            PlayerCardText("PERDA", "LOSS", "PÉRDIDA", "VERLUST", "PERTE"),
            quality.Samples >= 2 ? $"{quality.LossPercent:F0}%" : "—"));

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(metrics);

        return new Border
        {
            Background = PlayerCardBrush(7, 23, 34),
            BorderBrush = PlayerCardBrush(28, 57, 77),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(11d),
            Padding = new Thickness(10d),
            Margin = new Thickness(0d, 0d, 0d, 9d),
            Child = stack
        };
    }

    private FrameworkElement BuildPlayerCard(
        PlayerPresence player,
        VehicleTelemetry? telemetry,
        DateTimeOffset? receivedAt,
        string distance,
        bool isLocal,
        bool voiceActive,
        DateTimeOffset now)
    {
        var age = receivedAt.HasValue
            ? Math.Max(0d, (now - receivedAt.Value).TotalSeconds)
            : double.PositiveInfinity;
        var stale = !isLocal && telemetry is not null && age > 3d;
        var stateAccent = voiceActive
            ? PlayerCardBrush(88, 218, 151)
            : stale
                ? PlayerCardBrush(239, 181, 71)
                : telemetry is not null
                    ? PlayerCardBrush(83, 166, 225)
                    : PlayerCardBrush(111, 132, 147);

        var stateText = isLocal
            ? PlayerCardText("VOCÊ", "YOU", "TÚ", "SIE", "VOUS")
            : voiceActive
                ? PlayerCardText("FALANDO", "SPEAKING", "HABLANDO", "SPRICHT", "PARLE")
                : telemetry is null
                    ? PlayerCardText("AGUARDANDO", "WAITING", "ESPERANDO", "WARTET", "EN ATTENTE")
                    : stale
                        ? PlayerCardText("ATRASADA", "STALE", "ATRASADA", "VERZÖGERT", "RETARDÉE")
                        : PlayerCardText("AO VIVO", "LIVE", "EN VIVO", "LIVE", "EN DIRECT");

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var avatar = new Border
        {
            Width = 34d,
            Height = 34d,
            CornerRadius = new CornerRadius(17d),
            Background = isLocal ? PlayerCardBrush(17, 86, 69) : PlayerCardBrush(17, 55, 79),
            BorderBrush = isLocal ? PlayerCardBrush(70, 191, 136) : PlayerCardBrush(55, 133, 187),
            BorderThickness = new Thickness(1d),
            Child = new TextBlock
            {
                Text = PlayerInitial(player.DisplayName),
                Foreground = Brushes.White,
                FontSize = 13d,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(avatar, 0);
        header.Children.Add(avatar);

        var identity = new StackPanel { Margin = new Thickness(9d, 0d, 8d, 0d) };
        identity.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(player.DisplayName) ? player.PlayerId : player.DisplayName,
            Foreground = Brushes.White,
            FontSize = 12.5d,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        identity.Children.Add(new TextBlock
        {
            Text = BuildPlayerServiceLine(telemetry, player),
            Foreground = PlayerCardBrush(129, 153, 170),
            FontSize = 9.6d,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0d, 3d, 0d, 0d)
        });
        Grid.SetColumn(identity, 1);
        header.Children.Add(identity);

        var state = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(35, stateAccent.Color.R, stateAccent.Color.G, stateAccent.Color.B)),
            BorderBrush = stateAccent,
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(8d),
            Padding = new Thickness(7d, 3d, 7d, 3d),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = stateText,
                Foreground = stateAccent,
                FontSize = 8d,
                FontWeight = FontWeights.Bold
            }
        };
        Grid.SetColumn(state, 2);
        header.Children.Add(state);

        var metrics = new WrapPanel { Margin = new Thickness(0d, 10d, 0d, 0d) };
        metrics.Children.Add(PlayerMetricChip(
            PlayerCardText("VEL", "SPD", "VEL", "GES", "VIT"),
            telemetry is null ? "—" : $"{telemetry.SpeedKph:F0} km/h"));
        metrics.Children.Add(PlayerMetricChip(
            PlayerCardText("DIST", "DIST", "DIST", "DIST", "DIST"),
            distance));
        metrics.Children.Add(PlayerMetricChip(
            PlayerCardText("TELEMETRIA", "TELEMETRY", "TELEMETRÍA", "TELEMETRIE", "TÉLÉMÉTRIE"),
            BuildTelemetryAgeText(isLocal, telemetry, age)));

        if (telemetry?.DelaySeconds is int delaySeconds)
        {
            metrics.Children.Add(PlayerMetricChip(
                PlayerCardText("HORÁRIO", "SCHEDULE", "HORARIO", "FAHRPLAN", "HORAIRE"),
                BuildDelayText(delaySeconds)));
        }

        var details = new StackPanel { Margin = new Thickness(0d, 9d, 0d, 0d) };
        var destination = FirstNonEmpty(telemetry?.DestinationName, telemetry?.NextStopName);
        if (!string.IsNullOrWhiteSpace(destination))
        {
            details.Children.Add(new TextBlock
            {
                Text = $"→ {destination}",
                Foreground = PlayerCardBrush(198, 216, 228),
                FontSize = 10d,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        var vehicle = telemetry?.VehicleName;
        if (!string.IsNullOrWhiteSpace(vehicle))
        {
            details.Children.Add(new TextBlock
            {
                Text = vehicle.Trim(),
                Foreground = PlayerCardBrush(111, 139, 158),
                FontSize = 9.4d,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0d, 3d, 0d, 0d)
            });
        }

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(metrics);
        body.Children.Add(details);

        return new Border
        {
            Background = PlayerCardBrush(8, 22, 32),
            BorderBrush = voiceActive ? stateAccent : PlayerCardBrush(27, 49, 64),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(11d),
            Padding = new Thickness(10d),
            Margin = new Thickness(0d, 0d, 0d, 8d),
            Child = body
        };
    }

    private static FrameworkElement BuildPlayerCardsEmptyState()
    {
        var body = new StackPanel
        {
            Margin = new Thickness(8d, 24d, 8d, 12d)
        };
        body.Children.Add(new TextBlock
        {
            Text = "◎",
            Foreground = PlayerCardBrush(69, 132, 174),
            FontSize = 28d,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        body.Children.Add(new TextBlock
        {
            Text = PlayerCardText(
                "Nenhum motorista na sala agora",
                "No drivers in the room right now",
                "No hay conductores en la sala ahora",
                "Derzeit keine Fahrer im Raum",
                "Aucun conducteur dans la salle actuellement"),
            Foreground = PlayerCardBrush(154, 177, 192),
            FontSize = 10.5d,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 5d, 0d, 0d)
        });
        return body;
    }

    private static Border PlayerMetricChip(string label, string value)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = PlayerCardBrush(90, 119, 139),
            FontSize = 7.5d,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            Foreground = PlayerCardBrush(211, 225, 234),
            FontSize = 9.2d,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 118d,
            Margin = new Thickness(0d, 2d, 0d, 0d)
        });

        return new Border
        {
            Background = PlayerCardBrush(5, 17, 25),
            BorderBrush = PlayerCardBrush(23, 44, 58),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(7d),
            Padding = new Thickness(7d, 5d, 7d, 5d),
            Margin = new Thickness(0d, 0d, 5d, 5d),
            Child = stack
        };
    }

    private static string BuildPlayerServiceLine(VehicleTelemetry? telemetry, PlayerPresence player)
    {
        var line = string.IsNullOrWhiteSpace(telemetry?.Line) ? null : telemetry.Line.Trim();
        var route = string.IsNullOrWhiteSpace(telemetry?.Route) ? null : telemetry.Route.Trim();
        var map = string.IsNullOrWhiteSpace(player.MapName) ? null : player.MapName.Trim();

        var service = FirstNonEmpty(
            line is not null && route is not null ? $"{line} • {route}" : null,
            line,
            route);

        return (service, map) switch
        {
            (not null, not null) => $"{service} • {map}",
            (not null, null) => service,
            (null, not null) => map,
            _ => PlayerCardText("Sem serviço informado", "No service reported", "Sin servicio informado", "Kein Dienst gemeldet", "Aucun service indiqué")
        };
    }

    private static string BuildTelemetryAgeText(bool isLocal, VehicleTelemetry? telemetry, double ageSeconds)
    {
        if (telemetry is null)
        {
            return PlayerCardText("aguardando", "waiting", "esperando", "wartet", "en attente");
        }

        if (isLocal || ageSeconds < 1d)
        {
            return PlayerCardText("agora", "now", "ahora", "jetzt", "maintenant");
        }

        if (!double.IsFinite(ageSeconds))
        {
            return "—";
        }

        var seconds = Math.Max(1, (int)Math.Round(ageSeconds));
        return PlayerCardText(
            $"há {seconds}s",
            $"{seconds}s ago",
            $"hace {seconds}s",
            $"vor {seconds}s",
            $"il y a {seconds}s");
    }

    private static string BuildDelayText(int delaySeconds)
    {
        if (Math.Abs(delaySeconds) < 30)
        {
            return PlayerCardText("no horário", "on time", "a tiempo", "pünktlich", "à l'heure");
        }

        var magnitude = TimeSpan.FromSeconds(Math.Abs(delaySeconds));
        var value = magnitude.TotalMinutes >= 1d
            ? $"{(int)magnitude.TotalMinutes}:{magnitude.Seconds:00}"
            : $"0:{magnitude.Seconds:00}";

        return delaySeconds > 0
            ? PlayerCardText($"+{value}", $"+{value}", $"+{value}", $"+{value}", $"+{value}")
            : $"-{value}";
    }

    private static string PlayerInitial(string? displayName)
    {
        var value = (displayName ?? string.Empty).Trim();
        return value.Length == 0 ? "?" : value[..1].ToUpperInvariant();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string PlayerCardText(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    private static SolidColorBrush PlayerCardBrush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
