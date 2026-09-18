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
    private GhostReplayDocument? _webGhostSelectedDocument;
    private GhostReplayMetadata? _webGhostSelectedMetadata;
    private GhostReplayAnalytics? _webGhostSelectedAnalytics;
    private IReadOnlyList<WebGhostLibraryItem> _webGhostLibrary = Array.Empty<WebGhostLibraryItem>();
    private int _webGhostInvalidLibraryCount;
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
            libraryInvalidCount = _webGhostInvalidLibraryCount,
            library = _webGhostLibrary
                .Select(item => new
                {
                    fileName = item.FileName,
                    name = item.Metadata.Name,
                    recordedAtUtc = item.Metadata.RecordedAtUtc,
                    mapName = item.Metadata.MapName,
                    vehicleName = item.Metadata.VehicleName,
                    durationSeconds = item.Analytics.DurationSeconds,
                    estimatedDistanceKm = item.Analytics.EstimatedDistanceKm,
                    averageSpeedKph = item.Analytics.AverageSpeedKph,
                    maximumSpeedKph = item.Analytics.MaximumSpeedKph,
                    frameCount = item.Metadata.FrameCount,
                    selected = string.Equals(
                        item.Path,
                        _webGhostSelectedPath,
                        StringComparison.OrdinalIgnoreCase)
                })
                .ToArray(),
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
                        },
                    routePoints = BuildWebGhostRoutePoints(_webGhostSelectedDocument),
                    line = _webGhostSelectedDocument?.Frames
                        .Select(frame => frame.Telemetry.Line)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                }
        };
    }

    private void StartGhostRecordingFromWeb(string? name)
    {
        if (_webGhostRecorder.IsRecording)
        {
            throw new InvalidOperationException(
                "Já existe uma gravação Ghost em andamento.");
        }

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
            await RefreshGhostLibraryFromWebAsync();
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
            _webGhostSelectedDocument = null;
            _webGhostSelectedMetadata = null;
            _webGhostSelectedAnalytics = null;
            _webGhostStatus = "ghost-load-failed";
            _webGhostError = ex.Message;
            throw;
        }
    }


    private async Task RefreshGhostLibraryFromWebAsync()
    {
        var directory = GhostRecorder.GetGhostDirectory();
        Directory.CreateDirectory(directory);

        var items = new List<WebGhostLibraryItem>();
        var invalid = 0;
        foreach (var path in Directory
                     .EnumerateFiles(directory, $"*{GhostReplayFormat.Extension}")
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                var document = await _webGhostPlayer.LoadAsync(path);
                items.Add(new WebGhostLibraryItem(
                    Path.GetFileName(path),
                    Path.GetFullPath(path),
                    document.Metadata,
                    GhostReplayAnalyticsCalculator.Analyze(document)));
            }
            catch
            {
                invalid++;
            }
        }

        _webGhostLibrary = items;
        _webGhostInvalidLibraryCount = invalid;
    }

    private async Task SelectGhostLibraryItemFromWebAsync(string? fileName)
    {
        if (_webGhostRecorder.IsRecording || _webGhostPlayer.IsPlaying)
        {
            throw new InvalidOperationException(
                "Pare a gravação ou reprodução antes de trocar de Ghost.");
        }

        var path = ResolveGhostLibraryPath(fileName);
        var document = await _webGhostPlayer.LoadAsync(path);
        SetWebGhostSelection(path, document);
        _webGhostStatus = "ghost-loaded";
        _webGhostError = null;
    }

    private async Task ImportGhostReplayFromWebAsync()
    {
        if (_webGhostRecorder.IsRecording || _webGhostPlayer.IsPlaying)
        {
            throw new InvalidOperationException(
                "Pare a gravação ou reprodução antes de importar um Ghost.");
        }

        var dialog = new OpenFileDialog
        {
            Title = "Importar replay NavBR",
            Filter = $"NavBR Ghost (*{GhostReplayFormat.Extension})|*{GhostReplayFormat.Extension}|Todos os arquivos (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var document = await _webGhostPlayer.LoadAsync(dialog.FileName);
        var directory = GhostRecorder.GetGhostDirectory();
        Directory.CreateDirectory(directory);
        var source = Path.GetFullPath(dialog.FileName);
        var destination = CreateUniqueGhostDestination(
            directory,
            Path.GetFileName(source));

        if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(source, destination, overwrite: false);
        }

        SetWebGhostSelection(destination, document);
        await RefreshGhostLibraryFromWebAsync();
        _webGhostStatus = "ghost-imported";
        _webGhostError = null;
    }

    private static string ResolveGhostLibraryPath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(
                Path.GetFileName(fileName),
                fileName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Ghost inválido.");
        }

        var directory = Path.GetFullPath(GhostRecorder.GetGhostDirectory());
        var path = Path.GetFullPath(Path.Combine(directory, fileName));
        if (!string.Equals(
                Path.GetDirectoryName(path),
                directory,
                StringComparison.OrdinalIgnoreCase) ||
            !path.EndsWith(
                GhostReplayFormat.Extension,
                StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(path))
        {
            throw new FileNotFoundException(
                "O Ghost selecionado não existe mais na biblioteca.",
                path);
        }

        return path;
    }

    private static string CreateUniqueGhostDestination(
        string directory,
        string fileName)
    {
        var safeName = string.IsNullOrWhiteSpace(fileName)
            ? $"Replay{GhostReplayFormat.Extension}"
            : fileName;
        if (!safeName.EndsWith(
                GhostReplayFormat.Extension,
                StringComparison.OrdinalIgnoreCase))
        {
            safeName += GhostReplayFormat.Extension;
        }

        var candidate = Path.Combine(directory, safeName);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var stem = Path.GetFileNameWithoutExtension(safeName);
        for (var index = 2; index < 10_000; index++)
        {
            candidate = Path.Combine(
                directory,
                $"{stem} ({index}){GhostReplayFormat.Extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException(
            "Não foi possível gerar um nome único para o replay importado.");
    }

    private async Task PlayGhostFromWebAsync(
        double? playbackSpeed,
        bool loop)
    {
        if (_webGhostPlayer.IsPlaying)
        {
            throw new InvalidOperationException(
                "Já existe uma reprodução Ghost 3D em andamento.");
        }

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
        _webGhostSelectedDocument = document;
        _webGhostSelectedMetadata = document.Metadata;
        _webGhostSelectedAnalytics =
            GhostReplayAnalyticsCalculator.Analyze(document);
    }

    private static IReadOnlyList<WebGhostRoutePoint> BuildWebGhostRoutePoints(
        GhostReplayDocument? document)
    {
        if (document is null)
        {
            return Array.Empty<WebGhostRoutePoint>();
        }

        var frames = document.Frames
            .Where(frame =>
                double.IsFinite(frame.Telemetry.X) &&
                double.IsFinite(frame.Telemetry.Z))
            .ToArray();
        if (frames.Length == 0)
        {
            return Array.Empty<WebGhostRoutePoint>();
        }

        const int maxPoints = 1200;
        var step = Math.Max(1, (int)Math.Ceiling(frames.Length / (double)maxPoints));
        var points = new List<WebGhostRoutePoint>(
            Math.Min(maxPoints + 1, frames.Length));
        for (var index = 0; index < frames.Length; index += step)
        {
            var frame = frames[index];
            points.Add(new WebGhostRoutePoint(
                frame.Telemetry.X,
                frame.Telemetry.Z,
                frame.OffsetMilliseconds));
        }

        var last = frames[^1];
        if (points.Count == 0 ||
            points[^1].OffsetMilliseconds != last.OffsetMilliseconds)
        {
            points.Add(new WebGhostRoutePoint(
                last.Telemetry.X,
                last.Telemetry.Z,
                last.OffsetMilliseconds));
        }

        return points;
    }

    private sealed record WebGhostLibraryItem(
        string FileName,
        string Path,
        GhostReplayMetadata Metadata,
        GhostReplayAnalytics Analytics);

    private sealed record WebGhostRoutePoint(
        double X,
        double Z,
        long OffsetMilliseconds);
}
