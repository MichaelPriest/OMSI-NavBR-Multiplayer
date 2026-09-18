using System.Text.Json;
using System.Windows;
using NavBR.Client.Multiplayer;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client;

public partial class MainWindow
{
    private WebShellWindow? _webShellWindow;
    private IReadOnlyList<PublicRoomSummary> _webPublicRooms = Array.Empty<PublicRoomSummary>();
    private string? _webPublicRoomDirectoryError;
    private string? _webPublicRoomDirectoryServerUrl;

    private void WebShellButton_Click(object sender, RoutedEventArgs e)
    {
        if (_webShellWindow is { IsLoaded: true })
        {
            _webShellWindow.Activate();
            return;
        }

        _webShellWindow = new WebShellWindow(
            BuildWebShellState,
            () =>
            {
                LaunchOmsiForShell();
                _ = RefreshOmsiStatusAsync();
            },
            HandleWebShellCommandAsync)
        {
            Owner = this
        };
        _webShellWindow.Closed += (_, _) => _webShellWindow = null;
        _webShellWindow.Show();
    }

    private object BuildWebShellState()
    {
        var telemetry = _lastTelemetry;
        var omsi = _currentOmsi;
        var localManifest = OmsiCompatibilityManifestFactory.Create(
            telemetry,
            GetActiveMapForMultiplayer(),
            omsi?.FileVersion);

        return new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            appVersion = typeof(MainWindow).Assembly.GetName().Version?.ToString(),
            omsi = new
            {
                running = omsi is not null,
                processId = omsi?.ProcessId,
                version = omsi?.FileVersion,
                installDirectory = omsi?.InstallDirectory,
                compatible = omsi?.IsOmsi23004 ?? false
            },
            telemetry = telemetry is null
                ? null
                : new
                {
                    inGame = telemetry.IsInGame,
                    mapName = telemetry.MapName,
                    line = telemetry.Line,
                    route = telemetry.Route,
                    destinationName = telemetry.DestinationName,
                    nextStopName = telemetry.NextStopName,
                    x = telemetry.X,
                    y = telemetry.Y,
                    z = telemetry.Z,
                    headingDegrees = telemetry.HeadingDegrees,
                    speedKph = telemetry.SpeedKph
                },
            navigation = BuildWebNavigationState(),
            multiplayer = BuildWebMultiplayerState(),
            roomDirectory = new
            {
                serverUrl = _webPublicRoomDirectoryServerUrl,
                error = _webPublicRoomDirectoryError,
                rooms = _webPublicRooms
                    .OrderByDescending(room => PublicRoomFavoritesStore.IsFavorite(room.RoomId))
                    .ThenByDescending(room => room.PlayerCount)
                    .ThenBy(room => room.RoomId, StringComparer.CurrentCultureIgnoreCase)
                    .Select(room =>
                    {
                        var compatibility = EvaluateWebRoomCompatibility(localManifest, room);
                        return new
                        {
                            roomId = room.RoomId,
                            playerCount = room.PlayerCount,
                            mapName = room.MapName,
                            mapCompatibilityId = room.MapCompatibilityId,
                            updatedAtUtc = room.UpdatedAtUtc,
                            omsiVersion = room.OmsiVersion,
                            navbrVersion = room.NavBRVersion,
                            vehiclePath = room.VehiclePath,
                            vehicleCompatibilityId = room.VehicleCompatibilityId,
                            hofName = room.HofName,
                            hofCompatibilityId = room.HofCompatibilityId,
                            pluginProtocolVersion = room.PluginProtocolVersion,
                            favorite = PublicRoomFavoritesStore.IsFavorite(room.RoomId),
                            compatibility = compatibility.Level,
                            compatibilityIssues = compatibility.Issues,
                            directJoinAllowed = compatibility.DirectJoinAllowed
                        };
                    })
                    .ToArray()
            }
        };
    }

    private object BuildWebMultiplayerState()
    {
        if (_multiplayerWindow is not null)
        {
            return _multiplayerWindow.BuildWebBridgeState();
        }

        var settings = MultiplayerSettingsStore.Load();
        return new
        {
            available = false,
            connected = false,
            connectionState = "Disconnected",
            serverUrl = settings.ServerUrl,
            roomId = settings.RoomId,
            displayName = settings.DisplayName,
            hostRunning = false,
            hostPort = null as int?,
            roomIsPrivate = false,
            inviteAddresses = Array.Empty<string>(),
            latencyMs = null as double?,
            voiceEnabled = false,
            voiceChannel = settings.VoiceChannel,
            voiceProximityMeters = settings.VoiceProximityMeters,
            voiceDeafened = settings.VoiceDeafened,
            roleplayEnabled = settings.ExperimentalRoleplayCharacterEnabled,
            localRoleplayActive = false,
            selectedRoleplayCharacter = null as string,
            playerCount = 0,
            players = Array.Empty<object>(),
            chat = Array.Empty<object>()
        };
    }

    private async Task HandleWebShellCommandAsync(string command, JsonElement? payload)
    {
        switch (command)
        {
            case "openMultiplayerCentral":
                OpenMultiplayerCentralForShell();
                break;

            case "openRoleplay":
                OpenMultiplayerRoleplayTabForShell();
                break;

            case "openNavigation3D":
                OpenNavigation3D();
                break;

            case "toggleHudLayout":
                ToggleHudLayoutForShell();
                break;

            case "openHudEditor":
                OpenHudEditorForShell();
                break;

            case "connectRoom":
                OpenMultiplayerCentralForShell(showWindow: false);
                if (_multiplayerWindow is not null)
                {
                    await _multiplayerWindow.ConnectFromWebAsync(
                        GetWebPayloadString(payload, "serverUrl"),
                        GetWebPayloadString(payload, "roomId"),
                        GetWebPayloadString(payload, "displayName"),
                        GetWebPayloadString(payload, "roomPassword"));
                }
                break;

            case "createLocalRoom":
                OpenMultiplayerCentralForShell(showWindow: false);
                if (_multiplayerWindow is not null)
                {
                    await _multiplayerWindow.StartLocalHostFromWebAsync(
                        GetWebPayloadString(payload, "roomId"),
                        GetWebPayloadString(payload, "displayName"),
                        GetWebPayloadBool(payload, "isPrivate"),
                        GetWebPayloadString(payload, "roomPassword"));
                }
                break;

            case "disconnectRoom":
                if (_multiplayerWindow is not null)
                {
                    await _multiplayerWindow.DisconnectFromWebAsync();
                }
                break;

            case "stopLocalHost":
                if (_multiplayerWindow is not null)
                {
                    await _multiplayerWindow.StopLocalHostFromWebAsync();
                }
                break;

            case "refreshPublicRooms":
            {
                var serverUrl = GetWebPayloadString(payload, "serverUrl")
                    ?? MultiplayerSettingsStore.Load().ServerUrl;
                _webPublicRoomDirectoryServerUrl = serverUrl;
                _webPublicRoomDirectoryError = null;

                try
                {
                    using var directory = new PublicRoomDirectoryClient();
                    _webPublicRooms = await directory.GetRoomsAsync(serverUrl);
                }
                catch (Exception ex)
                {
                    _webPublicRooms = Array.Empty<PublicRoomSummary>();
                    _webPublicRoomDirectoryError = ex.Message;
                }
                break;
            }

            case "toggleRoomFavorite":
            {
                var roomId = GetWebPayloadString(payload, "roomId");
                if (!string.IsNullOrWhiteSpace(roomId))
                {
                    PublicRoomFavoritesStore.Toggle(roomId);
                }
                break;
            }

            case "setVoiceEnabled":
                OpenMultiplayerCentralForShell(showWindow: false);
                _multiplayerWindow?.SetVoiceEnabledFromWeb(GetWebPayloadBool(payload, "enabled"));
                break;

            case "configureVoice":
                OpenMultiplayerCentralForShell(showWindow: false);
                _multiplayerWindow?.ConfigureVoiceFromWeb(
                    GetWebPayloadString(payload, "channel"),
                    GetWebPayloadDouble(payload, "proximityMeters"),
                    GetWebPayloadBool(payload, "deafened"));
                break;

            case "sendChat":
                if (_multiplayerWindow is null)
                {
                    return;
                }

                var text = GetWebPayloadString(payload, "text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    await _multiplayerWindow.SendChatFromWebAsync(text);
                }
                break;
        }
    }

    private static WebRoomCompatibility EvaluateWebRoomCompatibility(
        OmsiCompatibilityManifest local,
        PublicRoomSummary room)
    {
        if (string.IsNullOrWhiteSpace(room.MapName))
        {
            return new WebRoomCompatibility(
                "blocked",
                false,
                ["O host ainda não informou o mapa obrigatório."]);
        }

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
        var issues = report.Issues
            .Where(issue => issue.Severity != CompatibilityIssueSeverity.Info)
            .Select(issue => issue.Message)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        if (report.HasBlockingIssues)
        {
            return new WebRoomCompatibility("blocked", false, issues);
        }

        if (report.Issues.Any(issue => issue.Severity == CompatibilityIssueSeverity.Warning))
        {
            return new WebRoomCompatibility("warning", true, issues);
        }

        return new WebRoomCompatibility("compatible", true, issues);
    }

    private static double? GetWebPayloadDouble(JsonElement? payload, string propertyName)
    {
        if (payload is not JsonElement element ||
            element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out var number) ||
            !double.IsFinite(number))
        {
            return null;
        }

        return number;
    }

    private static bool GetWebPayloadBool(JsonElement? payload, string propertyName)
    {
        if (payload is not JsonElement element ||
            element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value) ||
            (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False))
        {
            return false;
        }

        return value.GetBoolean();
    }

    private sealed record WebRoomCompatibility(
        string Level,
        bool DirectJoinAllowed,
        IReadOnlyList<string> Issues);

    private static string? GetWebPayloadString(JsonElement? payload, string propertyName)
    {
        if (payload is not JsonElement element ||
            element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }
}
