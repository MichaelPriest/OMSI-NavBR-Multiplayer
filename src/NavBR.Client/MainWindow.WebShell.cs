using System.Text.Json;
using System.Windows;
using NavBR.Client.Localization;
using NavBR.Client.Multiplayer;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client;

public partial class MainWindow
{
    private WebShellWindow? _webShellWindow;
    private bool _webShellPrimaryMode;
    private long _webNavigationRequestId;
    private string? _webRequestedScreen;
    private IReadOnlyList<PublicRoomSummary> _webPublicRooms = Array.Empty<PublicRoomSummary>();
    private string? _webPublicRoomDirectoryError;
    private string? _webPublicRoomDirectoryServerUrl;

    internal void OpenPrimaryWebShell() =>
        OpenWebShell(primary: true);

    internal bool IsPrimaryInterfaceVisibleForShell() =>
        _webShellWindow?.IsVisible == true || IsVisible;

    internal void ShowPrimaryInterfaceForShell() =>
        OpenWebShell(primary: true);

    internal void NavigatePrimaryWebShell(string screen)
    {
        if (string.IsNullOrWhiteSpace(screen))
        {
            return;
        }

        _webRequestedScreen = screen.Trim();
        _webNavigationRequestId++;
        OpenWebShell(primary: true);
    }

    internal void HidePrimaryInterfaceForShell()
    {
        _webShellWindow?.Hide();
        ShowInTaskbar = false;
        Hide();
    }

    private Window? GetPrimaryWebDialogOwner() =>
        _webShellWindow is { IsVisible: true } ? _webShellWindow : null;

    private void OpenWebShell(bool primary)
    {
        if (primary)
        {
            _webShellPrimaryMode = true;
        }

        if (_webShellWindow is { IsLoaded: true } existing)
        {
            existing.Show();
            if (primary && existing.IsReady)
            {
                ActivateWebShellAsPrimary(existing);
            }
            existing.Activate();
            return;
        }

        var window = new WebShellWindow(
            BuildWebShellState,
            () =>
            {
                LaunchOmsiForShell();
                _ = RefreshOmsiStatusAsync();
            },
            HandleWebShellCommandAsync,
            GetActiveWebMapResourceDirectory);

        if (!primary)
        {
            window.Owner = this;
        }

        _webShellWindow = window;
        window.ShellReady += (_, _) =>
        {
            if (_webShellPrimaryMode && ReferenceEquals(_webShellWindow, window))
            {
                ActivateWebShellAsPrimary(window);
            }
        };
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_webShellWindow, window))
            {
                _webShellWindow = null;
            }

            if (_webShellPrimaryMode &&
                Application.Current?.Dispatcher.HasShutdownStarted != true)
            {
                // The WPF MainWindow is now a native service host only.
                // Closing the React shell leaves NavBR in the tray instead of
                // exposing the retired WPF user interface.
                ShowInTaskbar = false;
                Hide();
            }
        };
        window.Show();
    }

    private void ActivateWebShellAsPrimary(WebShellWindow window)
    {
        ShowInTaskbar = false;
        Hide();
        window.ShowInTaskbar = true;
        window.Show();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
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
            cultureName = LocalizationService.CurrentCulture.Name,
            supportedLanguages = LocalizationService.SupportedLanguages
                .Select(language => new
                {
                    cultureName = language.CultureName,
                    displayName = language.DisplayName
                })
                .ToArray(),
            navigationRequest = string.IsNullOrWhiteSpace(_webRequestedScreen)
                ? null
                : new
                {
                    id = _webNavigationRequestId,
                    screen = _webRequestedScreen
                },
            shell = new
            {
                topmost = _webShellWindow?.Topmost == true
            },
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
            navigation3D = BuildWebNavigation3DState(),
            operations = BuildWebOperationsState(),
            system = BuildWebSystemState(),
            roadmapStudio = BuildWebRoadmapState(),
            ghost = BuildWebGhostState(),
            hardware = BuildWebHardwareState(),
            network = BuildWebNetworkState(),
            companyNetwork = BuildWebCompanyNetworkState(),
            roleplay = BuildWebRoleplayState(),
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
            voiceInputDeviceNumber = settings.VoiceInputDeviceNumber,
            voiceOutputDeviceNumber = settings.VoiceOutputDeviceNumber,
            voiceInputDevices = Array.Empty<object>(),
            voiceOutputDevices = Array.Empty<object>(),
            voiceMixers = Array.Empty<object>(),
            voicePushToTalkActive = false,
            voiceQuality = new
            {
                activeStreams = 0,
                receivedPackets = 0L,
                playedPackets = 0L,
                fecRecoveredPackets = 0L,
                estimatedLostPackets = 0L,
                latePackets = 0L,
                duplicatePackets = 0L,
                averageJitterMilliseconds = 0d,
                targetBufferMilliseconds = 40,
                estimatedLossPercent = 0d
            },
            chatHotkey = settings.ChatHotkey,
            voiceHotkey = settings.VoiceHotkey,
            hotkeyOptions = NavBR.Client.Overlay.NavBRHotkeyCatalog.Options
                .Select(option => option.Name)
                .ToArray(),
            relayEnabled = settings.EnableApplicationRelay,
            relayServerUrl = settings.RelayServerUrl,
            physicalVehiclesEnabled = settings.ExperimentalPhysicalVehiclesEnabled,
            physicalVehiclesAvailable = false,
            networkQuality = new
            {
                level = "Unknown",
                roundTripMs = null as double?,
                jitterMs = null as double?,
                lossPercent = 0d,
                samples = 0,
                updatedAtUtc = DateTimeOffset.UtcNow
            },
            sessionAuthority = new
            {
                roomOwnerPlayerId = null as string,
                roomOwnerDisplayName = null as string,
                trafficAuthorityPlayerId = null as string,
                trafficAuthorityDisplayName = null as string,
                isRoomOwner = false,
                isTrafficAuthority = false
            },
            transportMode = "none",
            roomCompatibility = new
            {
                level = "none",
                remoteCount = 0,
                blocking = 0,
                warnings = 0,
                partial = 0,
                affectedAreas = Array.Empty<string>()
            },
            sessionOperationalState = null as object,
            roleplayEnabled = settings.ExperimentalRoleplayCharacterEnabled,
            localRoleplayActive = false,
            selectedRoleplayCharacter = null as string,
            playerCount = 0,
            players = Array.Empty<object>(),
            sessionPoints = Array.Empty<object>(),
            chat = Array.Empty<object>()
        };
    }

    private async Task HandleWebShellCommandAsync(string command, JsonElement? payload)
    {
        switch (command)
        {
            case "setLanguage":
            {
                var cultureName = GetWebPayloadString(payload, "cultureName");
                if (!string.IsNullOrWhiteSpace(cultureName))
                {
                    LocalizationService.SetCulture(cultureName);
                }
                break;
            }

            case "refreshOmsiDetection":
                await RefreshOmsiStatusAsync();
                break;

            case "setShellTopmost":
                if (_webShellWindow is not null)
                {
                    _webShellWindow.Topmost = GetWebPayloadBool(payload, "enabled");
                }
                break;

            case "ensureMultiplayerController":
                OpenMultiplayerCentralForShell(showWindow: false);
                break;

            case "openRoleplay":
                NavigatePrimaryWebShell("roleplay");
                break;

            case "setRoleplayEnabled":
                await SetRoleplayEnabledFromWebAsync(
                    GetWebPayloadBool(payload, "enabled"));
                break;

            case "selectRoleplayCharacter":
                SelectRoleplayCharacterFromWeb(
                    GetWebPayloadString(payload, "characterId"));
                break;

            case "startRoleplay":
                await StartRoleplayFromWebAsync();
                break;

            case "stopRoleplay":
                await StopRoleplayFromWebAsync();
                break;

            case "openNavigation3D":
                NavigatePrimaryWebShell("navigation");
                break;

            case "toggleHudLayout":
                ToggleHudLayoutForShell();
                break;

            case "saveHudSettings":
                SaveHudSettingsFromWeb(payload);
                break;

            case "resetHudSettings":
                ResetHudSettingsFromWeb();
                break;

            case "analyzeRoadmap":
                AnalyzeRoadmapFromWeb(
                    GetWebPayloadString(payload, "folderName"));
                break;

            case "buildRoadmapTiles":
                await BuildRoadmapTilesFromWebAsync(
                    GetWebPayloadString(payload, "folderName"));
                break;

            case "buildRoadmapVector":
                await BuildRoadmapVectorFromWebAsync(
                    GetWebPayloadString(payload, "folderName"));
                break;

            case "openRoadmapFolder":
                OpenRoadmapFolderFromWeb(
                    GetWebPayloadString(payload, "folderName"));
                break;

            case "startGhostRecording":
                StartGhostRecordingFromWeb(
                    GetWebPayloadString(payload, "name"));
                break;

            case "stopGhostRecording":
                await StopGhostRecordingFromWebAsync();
                break;

            case "cancelGhostRecording":
                CancelGhostRecordingFromWeb();
                break;

            case "selectGhostFile":
                await SelectGhostFileFromWebAsync();
                break;

            case "refreshGhostLibrary":
                await RefreshGhostLibraryFromWebAsync();
                break;

            case "selectGhostLibraryItem":
                await SelectGhostLibraryItemFromWebAsync(
                    GetWebPayloadString(payload, "fileName"));
                break;

            case "importGhostReplay":
                await ImportGhostReplayFromWebAsync();
                break;

            case "playGhost":
                await PlayGhostFromWebAsync(
                    GetWebPayloadDouble(payload, "playbackSpeed"),
                    GetWebPayloadBool(payload, "loop"));
                break;

            case "stopGhostPlayback":
                StopGhostPlaybackFromWeb();
                break;

            case "openGhostFolder":
                OpenGhostFolderFromWeb();
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
                    if (GetWebPayloadBool(payload, "useRelay"))
                    {
                        await _multiplayerWindow.StartRelayRoomFromWebAsync(
                            GetWebPayloadString(payload, "roomId"),
                            GetWebPayloadString(payload, "displayName"),
                            GetWebPayloadBool(payload, "isPrivate"),
                            GetWebPayloadString(payload, "roomPassword"),
                            GetWebPayloadString(payload, "relayServerUrl"));
                    }
                    else
                    {
                        await _multiplayerWindow.StartLocalHostFromWebAsync(
                            GetWebPayloadString(payload, "roomId"),
                            GetWebPayloadString(payload, "displayName"),
                            GetWebPayloadBool(payload, "isPrivate"),
                            GetWebPayloadString(payload, "roomPassword"));
                    }
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

            case "configureVoiceDevices":
                OpenMultiplayerCentralForShell(showWindow: false);
                _multiplayerWindow?.ConfigureVoiceDevicesFromWeb(
                    GetWebPayloadInt(payload, "inputDeviceNumber"),
                    GetWebPayloadInt(payload, "outputDeviceNumber"));
                break;

            case "configureRemoteVoice":
                OpenMultiplayerCentralForShell(showWindow: false);
                _multiplayerWindow?.ConfigureRemoteVoiceFromWeb(
                    GetWebPayloadString(payload, "playerId"),
                    GetWebPayloadBool(payload, "muted"),
                    GetWebPayloadDouble(payload, "gain"));
                break;

            case "configureMultiplayerHotkeys":
                OpenMultiplayerCentralForShell(showWindow: false);
                _multiplayerWindow?.ConfigureHotkeysFromWeb(
                    GetWebPayloadString(payload, "chatHotkey"),
                    GetWebPayloadString(payload, "voiceHotkey"));
                break;

            case "configureRelay":
                OpenMultiplayerCentralForShell(showWindow: false);
                _multiplayerWindow?.ConfigureRelayFromWeb(
                    GetWebPayloadBool(payload, "enabled"),
                    GetWebPayloadString(payload, "relayServerUrl"));
                break;

            case "setPhysicalVehiclesEnabled":
                OpenMultiplayerCentralForShell(showWindow: false);
                if (_multiplayerWindow is not null)
                {
                    await _multiplayerWindow.ConfigurePhysicalVehiclesFromWebAsync(
                        GetWebPayloadBool(payload, "enabled"));
                }
                break;

            case "submitOperationalReport":
                OpenMultiplayerCentralForShell(showWindow: false);
                if (_multiplayerWindow is not null)
                {
                    await _multiplayerWindow.SubmitOperationalReportFromWebAsync(
                        string.Equals(
                            GetWebPayloadString(payload, "kind"),
                            "incident",
                            StringComparison.OrdinalIgnoreCase));
                }
                break;

            case "resolveMyOperationalReports":
                OpenMultiplayerCentralForShell(showWindow: false);
                if (_multiplayerWindow is not null)
                {
                    await _multiplayerWindow.ResolveOwnOperationalReportsFromWebAsync();
                }
                break;

            case "acknowledgeOperationalReport":
                await HandleWebOperationalReportAsync(
                    GetWebPayloadString(payload, "reportId") ?? string.Empty,
                    resolve: false);
                break;

            case "resolveOperationalReport":
                await HandleWebOperationalReportAsync(
                    GetWebPayloadString(payload, "reportId") ?? string.Empty,
                    resolve: true);
                break;

            case "saveCompany":
                SaveWebCompany(
                    GetWebPayloadString(payload, "name"),
                    GetWebPayloadString(payload, "shortName"),
                    GetWebPayloadString(payload, "baseMap"));
                break;

            case "registerCurrentVehicle":
                RegisterCurrentVehicleFromWeb(
                    GetWebPayloadString(payload, "fleetNumber"),
                    GetWebPayloadString(payload, "livery"));
                break;

            case "removeFleetVehicle":
                RemoveFleetVehicleFromWeb(GetWebPayloadString(payload, "vehicleId"));
                break;

            case "saveDriverProfile":
                SaveWebDriverProfile(
                    GetWebPayloadString(payload, "displayName"),
                    GetWebPayloadString(payload, "companyName"));
                break;

            case "exportDriverProfile":
                ExportDriverProfileFromWeb();
                break;

            case "selectDriverProfileImport":
                SelectDriverProfileImportFromWeb();
                break;

            case "applyDriverProfileImport":
                ApplyDriverProfileImportFromWeb();
                break;

            case "cancelDriverProfileImport":
                CancelDriverProfileImportFromWeb();
                break;

            case "discoverOmsiProfiles":
                DiscoverOmsiProfilesFromWeb(GetWebPayloadString(payload, "path"));
                break;

            case "selectOmsiFolder":
                SelectOmsiFolderFromWeb();
                break;

            case "openOmsiProfileFolder":
                OpenOmsiProfileFolderFromWeb(
                    GetWebPayloadString(payload, "profileId"));
                break;

            case "launchOmsiProfile":
                LaunchOmsiProfileFromWeb(GetWebPayloadString(payload, "profileId"));
                break;

            case "setPreferredOmsiProfile":
                SetPreferredOmsiProfileFromWeb(GetWebPayloadString(payload, "profileId"));
                break;

            case "updateOmsiProfile":
                UpdateOmsiProfileFromWeb(
                    GetWebPayloadString(payload, "profileId"),
                    GetWebPayloadString(payload, "name"),
                    GetWebPayloadString(payload, "launchArguments"));
                break;

            case "removeOmsiProfile":
                RemoveOmsiProfileFromWeb(GetWebPayloadString(payload, "profileId"));
                break;

            case "setDiagnosticsEnabled":
                SetDiagnosticsEnabledFromWeb(GetWebPayloadBool(payload, "enabled"));
                break;

            case "flushDiagnostics":
                await FlushDiagnosticsFromWebAsync();
                break;

            case "purgeDiagnostics":
                PurgeDiagnosticsFromWeb();
                break;

            case "openFeedback":
                OpenFeedbackFromWeb(GetWebPayloadString(payload, "kind"));
                break;

            case "exportSessionHealth":
                ExportSessionHealthFromWeb();
                break;

            case "refreshNetworkDiagnostics":
                await RefreshWebNetworkDiagnosticsAsync();
                break;

            case "applyFirewallRule":
                await ApplyWebFirewallRuleAsync();
                break;

            case "setAutomaticUpnp":
                await SetWebAutomaticUpnpAsync(GetWebPayloadBool(payload, "enabled"));
                break;

            case "runExternalPortProbe":
                await RunWebExternalPortProbeAsync();
                break;

            case "refreshCompanyNetwork":
                await RefreshWebCompanyNetworkAsync();
                break;

            case "startCompanyNode":
                await StartWebCompanyNodeAsync();
                break;

            case "stopCompanyNode":
                await StopWebCompanyNodeAsync();
                break;

            case "createCompanyInvite":
                CreateWebCompanyInvite(GetWebPayloadString(payload, "role"));
                break;

            case "joinCompany":
                await JoinWebCompanyAsync(
                    GetWebPayloadString(payload, "nodeUrl"),
                    GetWebPayloadString(payload, "inviteCode"));
                break;

            case "changeCompanyMemberRole":
                await ChangeWebCompanyMemberRoleAsync(
                    GetWebPayloadString(payload, "playerId"),
                    GetWebPayloadString(payload, "role"));
                break;

            case "removeCompanyMember":
                await RemoveWebCompanyMemberAsync(
                    GetWebPayloadString(payload, "playerId"));
                break;

            case "connectHardware":
                ConnectHardwareFromWeb(
                    GetWebPayloadString(payload, "portName"),
                    GetWebPayloadInt(payload, "baudRate") ?? 115200,
                    GetWebPayloadBool(payload, "autoReconnect"));
                break;

            case "disconnectHardware":
                DisconnectHardwareFromWeb();
                break;

            case "saveHardwareSelection":
                SaveHardwareSelectionFromWeb(
                    GetWebPayloadString(payload, "portName"),
                    GetWebPayloadInt(payload, "baudRate") ?? 115200,
                    GetWebPayloadBool(payload, "autoReconnect"));
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

    private static int? GetWebPayloadInt(JsonElement? payload, string propertyName)
    {
        if (payload is not JsonElement element ||
            element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var number))
        {
            return null;
        }

        return number;
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
