using System.Diagnostics;
using System.Windows.Threading;
using Microsoft.Win32;
using NavBR.Client.Ghost;

namespace NavBR.Client;

public partial class MainWindow
{
    private readonly GhostRecorder _webGhostRecorder = new();
    private readonly GhostReplayPlayer _webGhostPlayer = new();
    private DispatcherTimer? _webGhostRecordTimer;
    private string? _webGhostSelectedPath;
    private GhostReplayMetadata? _webGhostSelectedMetadata;
    private GhostReplayAnalytics? _webGhostSelectedAnalytics;
    private string? _webGhostStatus;
    private string? _webGhostError;

    private object BuildWebGhostState()
    {
        return new
        {
            recording = _webGhostRecorder.IsRecording,
            frameCount = _webGhostRecorder.FrameCount,
            playing = _webGhostPlayer.IsPlaying,
            selectedPath = _webGhostSelectedPath,
            status = _webGhostStatus,
            error = _webGhostError,
            ghostDirectory = GhostRecorder.GetGhostDirectory(),
            selected = _webGhostSelectedMetadata is null
                ? null
                : new
                {
                    name = _webGhostSelectedMetadata.Name,
                    recordedAtUtc = _webGhostSelectedMetadata.RecordedAtUtc,
                    mapName = _webGhostSelectedMetadata.MapName,
                    mapCompatibilityId = _webGhostSelectedMetadata.MapCompatibilityId,
                    vehicleName = _webGhostSelectedMetadata.VehicleName,
                    vehiclePath = _webGhostSelectedMetadata.VehiclePath,
                    vehicleCompatibilityId = _webGhostSelectedMetadata.VehicleCompatibilityId,
                    hofName = _webGhostSelectedMetadata.HofName,
                    hofCompatibilityId = _webGhostSelectedMetadata.HofCompatibilityId,
                    durationSeconds = _webGhostSelectedMetadata.DurationSeconds,
                    frameCount = _webGhostSelectedMetadata.FrameCount,
                    analytics = _webGhostSelectedAnalytics is null
                        ? null
                        : new
                        {
                            durationSeconds = _webGhostSelectedAnalytics.DurationSeconds,
                            estimatedDistanceKm = _webGhostSelectedAnalytics.EstimatedDistanceKm,
                            averageSpeedKph = _webGhostSelectedAnalytics.AverageSpeedKph,
                            maximumSpeedKph = _webGhostSelectedAnalytics.MaximumSpeedKph,
                            validSpeedSamples = _webGhostSelectedAnalytics.ValidSpeedSamples
                        }
                }
        };
    }

    private void StartGhostRecordingFromWeb(string? name)
    {
        var telemetry = GetCurrentTelemetryForAlpha11();
        if (telemetry is null || !telemetry.IsInGame)
        {
            throw new InvalidOperationException(
                "Carregue um mapa e um ônibus no OMSI antes de gravar.");
        }

        _webGhostPlayer.Stop();
        _webGhostError = null;
        _webGhostStatus = "recording";
        _webGhostRecorder.Start(name);
        EnsureWebGhostRecordTimer();
        _webGhostRecordTimer!.Start();
        _webGhostRecorder.Record(telemetry);
    }

    private async Task StopGhostRecordingFromWebAsync()
    {
        if (!_webGhostRecorder.IsRecording)
        {
            return;
        }

        _webGhostRecordTimer?.Stop();
        try
        {
            var path = await _webGhostRecorder.StopAndSaveAsync();
            var document = await _webGhostPlayer.LoadAsync(path);
            SetWebGhostSelection(path, document);
            _webGhostStatus = "recording-saved";
            _webGhostError = null;
        }
        catch (Exception ex)
        {
            _webGhostRecorder.Cancel();
            _webGhostStatus = "recording-save-failed";
            _webGhostError = ex.Message;
            throw;
        }
    }

    private void CancelGhostRecordingFromWeb()
    {
        _webGhostRecordTimer?.Stop();
        _webGhostRecorder.Cancel();
        _webGhostStatus = "recording-cancelled";
        _webGhostError = null;
    }

    private async Task SelectGhostFileFromWebAsync()
    {
        if (_webGhostRecorder.IsRecording || _webGhostPlayer.IsPlaying)
        {
            throw new InvalidOperationException(
                "Pare a gravação ou reprodução antes de abrir outro Ghost.");
        }

        var directory = GhostRecorder.GetGhostDirectory();
        var dialog = new OpenFileDialog
        {
            Title = "Escolha um Ghost NavBR",
            Filter = $"NavBR Ghost (*{GhostReplayFormat.Extension})|*{GhostReplayFormat.Extension}|Todos os arquivos (*.*)|*.*",
            InitialDirectory = Directory.Exists(directory) ? directory : null
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var document = await _webGhostPlayer.LoadAsync(dialog.FileName);
            SetWebGhostSelection(dialog.FileName, document);
            _webGhostStatus = "ghost-loaded";
            _webGhostError = null;
        }
        catch (Exception ex)
        {
            _webGhostSelectedPath = null;
            _webGhostSelectedMetadata = null;
            _webGhostSelectedAnalytics = null;
            _webGhostStatus = "ghost-load-failed";
            _webGhostError = ex.Message;
            throw;
        }
    }

    private async Task PlayGhostFromWebAsync(
        double? playbackSpeed,
        bool loop)
    {
        if (string.IsNullOrWhiteSpace(_webGhostSelectedPath))
        {
            throw new InvalidOperationException(
                "Abra um arquivo Ghost antes de reproduzir.");
        }

        if (_webGhostRecorder.IsRecording)
        {
            throw new InvalidOperationException(
                "Pare a gravação antes de reproduzir um Ghost.");
        }

        var speed = Math.Clamp(
            playbackSpeed is double value && double.IsFinite(value) ? value : 1d,
            0.1d,
            4d);

        _webGhostStatus = "playback-starting";
        _webGhostError = null;
        try
        {
            await _webGhostPlayer.PlayAsync(
                _webGhostSelectedPath,
                speed,
                loop);
            _webGhostStatus = "playback-completed";
        }
        catch (OperationCanceledException)
        {
            _webGhostStatus = "playback-stopped";
        }
        catch (Exception ex)
        {
            _webGhostStatus = "playback-failed";
            _webGhostError = ex.Message;
            throw;
        }
    }

    private void StopGhostPlaybackFromWeb()
    {
        _webGhostPlayer.Stop();
        _webGhostStatus = "playback-stopping";
        _webGhostError = null;
    }

    private static void OpenGhostFolderFromWeb()
    {
        var directory = GhostRecorder.GetGhostDirectory();
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{directory}\"",
            UseShellExecute = true
        });
    }

    private void EnsureWebGhostRecordTimer()
    {
        if (_webGhostRecordTimer is not null)
        {
            return;
        }

        _webGhostRecordTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100d)
        };
        _webGhostRecordTimer.Tick += (_, _) =>
        {
            if (!_webGhostRecorder.IsRecording)
            {
                _webGhostRecordTimer.Stop();
                return;
            }

            _webGhostRecorder.Record(GetCurrentTelemetryForAlpha11());
        };
    }

    private void SetWebGhostSelection(
        string path,
        GhostReplayDocument document)
    {
        _webGhostSelectedPath = Path.GetFullPath(path);
        _webGhostSelectedMetadata = document.Metadata;
        _webGhostSelectedAnalytics =
            GhostReplayAnalyticsCalculator.Analyze(document);
    }
}
