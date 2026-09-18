using System.Text.Json;
using System.Windows;
using NavBR.Client.Multiplayer;

namespace NavBR.Client;

public partial class MainWindow
{
    private WebShellWindow? _webShellWindow;

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
            multiplayer = BuildWebMultiplayerState()
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
            inviteAddresses = Array.Empty<string>(),
            latencyMs = null as double?,
            voiceEnabled = false,
            voiceChannel = settings.VoiceChannel,
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

            case "toggleHudLayout":
                ToggleHudLayoutForShell();
                break;

            case "openHudEditor":
                OpenHudEditorForShell();
                break;

            case "sendChat":
                if (_multiplayerWindow is null ||
                    payload is not JsonElement chatPayload ||
                    !chatPayload.TryGetProperty("text", out var textElement))
                {
                    return;
                }

                var text = textElement.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    await _multiplayerWindow.SendChatFromWebAsync(text);
                }
                break;
        }
    }
}
