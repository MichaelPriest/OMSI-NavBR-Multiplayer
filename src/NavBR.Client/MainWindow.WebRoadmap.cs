using System.Diagnostics;
using NavBR.Client.Maps;

namespace NavBR.Client;

public partial class MainWindow
{
    private readonly OmsiRoadmapGeneratorService _webRoadmapGenerator = new();
    private readonly OmsiRoadmapVectorGeneratorService _webRoadmapVectorGenerator = new();
    private string? _webRoadmapSelectedFolder;
    private OmsiRoadmapAnalysis? _webRoadmapAnalysis;
    private WebRoadmapResult? _webRoadmapResult;
    private string? _webRoadmapStatus;
    private string? _webRoadmapError;
    private double? _webRoadmapProgress;
    private bool _webRoadmapBusy;

    private object BuildWebRoadmapState()
    {
        return new
        {
            maps = _installedMaps
                .OrderBy(map => map.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(map =>
                {
                    var roadmapPath = Path.Combine(
                        map.DirectoryPath,
                        "texture",
                        "map",
                        "whole.roadmap.bmp");
                    var hdRoadmapPath = Path.Combine(
                        map.DirectoryPath,
                        "texture",
                        "map",
                        OmsiRoadmapVectorGeneratorService.HdRoadmapFileName);
                    var roadmapExists = File.Exists(roadmapPath);
                    var hdRoadmapExists = File.Exists(hdRoadmapPath);
                    var roadmapPreviewUrl = roadmapExists
                        ? TryBuildWebMapResourceUrl(map, roadmapPath)
                        : null;
                    var hdRoadmapPreviewUrl = hdRoadmapExists
                        ? TryBuildWebMapResourceUrl(map, hdRoadmapPath)
                        : null;
                    var activeUsesHd =
                        hdRoadmapExists &&
                        string.Equals(
                            map.RoadmapPath,
                            hdRoadmapPath,
                            StringComparison.OrdinalIgnoreCase);
                    return new
                    {
                        folderName = map.FolderName,
                        displayName = map.DisplayName,
                        directoryPath = map.DirectoryPath,
                        tileCount = map.TileCount,
                        compatibilityId = map.CompatibilityId,
                        roadmapPath,
                        roadmapExists,
                        roadmapPreviewUrl,
                        hdRoadmapPath,
                        hdRoadmapExists,
                        hdRoadmapPreviewUrl,
                        activeUsesHd
                    };
                })
                .ToArray(),
            selectedFolder = _webRoadmapSelectedFolder,
            busy = _webRoadmapBusy,
            progress = _webRoadmapProgress,
            status = _webRoadmapStatus,
            error = _webRoadmapError,
            analysis = _webRoadmapAnalysis is null
                ? null
                : new
                {
                    mapDirectory = _webRoadmapAnalysis.MapDirectory,
                    outputPath = _webRoadmapAnalysis.OutputPath,
                    tileImageCount = _webRoadmapAnalysis.TileImageCount,
                    minGridX = _webRoadmapAnalysis.MinGridX,
                    minGridY = _webRoadmapAnalysis.MinGridY,
                    maxGridX = _webRoadmapAnalysis.MaxGridX,
                    maxGridY = _webRoadmapAnalysis.MaxGridY,
                    tilePixelWidth = _webRoadmapAnalysis.TilePixelWidth,
                    tilePixelHeight = _webRoadmapAnalysis.TilePixelHeight,
                    outputPixelWidth = _webRoadmapAnalysis.OutputPixelWidth,
                    outputPixelHeight = _webRoadmapAnalysis.OutputPixelHeight,
                    missingTileImages = _webRoadmapAnalysis.MissingTileImages,
                    existingWholeRoadmap = _webRoadmapAnalysis.ExistingWholeRoadmap,
                    estimatedBytes = _webRoadmapAnalysis.EstimatedBytes,
                    canBuildFromTiles = _webRoadmapAnalysis.CanBuild
                },
            result = _webRoadmapResult is null
                ? null
                : new
                {
                    mode = _webRoadmapResult.Mode,
                    outputPath = _webRoadmapResult.OutputPath,
                    backupPath = _webRoadmapResult.BackupPath,
                    pixelWidth = _webRoadmapResult.PixelWidth,
                    pixelHeight = _webRoadmapResult.PixelHeight,
                    elapsedSeconds = _webRoadmapResult.ElapsedSeconds,
                    fileSizeBytes = _webRoadmapResult.FileSizeBytes,
                    tileImagesUsed = _webRoadmapResult.TileImagesUsed,
                    missingTileImages = _webRoadmapResult.MissingTileImages,
                    tileFilesRead = _webRoadmapResult.TileFilesRead,
                    splinesDrawn = _webRoadmapResult.SplinesDrawn,
                    quality = _webRoadmapResult.Quality
                }
        };
    }

    private void AnalyzeRoadmapFromWeb(string? folderName)
    {
        var map = ResolveWebRoadmapMap(folderName);
        if (_webRoadmapBusy)
        {
            throw new InvalidOperationException(
                "Aguarde a geração atual do roadmap terminar.");
        }

        _webRoadmapSelectedFolder = map.FolderName;
        _webRoadmapError = null;
        _webRoadmapProgress = null;
        _webRoadmapResult = null;

        try
        {
            _webRoadmapAnalysis = _webRoadmapGenerator.Analyze(map);
            _webRoadmapStatus = _webRoadmapAnalysis.CanBuild
                ? "analysis-ready"
                : "analysis-no-tile-images";
        }
        catch (Exception ex)
        {
            _webRoadmapAnalysis = null;
            _webRoadmapStatus = "analysis-failed";
            _webRoadmapError = ex.Message;
            throw;
        }
    }

    private async Task BuildRoadmapTilesFromWebAsync(string? folderName)
    {
        var map = ResolveWebRoadmapMap(folderName);
        BeginWebRoadmapBuild(map, "building-tiles");

        try
        {
            var progress = new Progress<double>(value =>
            {
                _webRoadmapProgress = Math.Clamp(
                    double.IsFinite(value) ? value : 0d,
                    0d,
                    1d);
            });
            var result = await _webRoadmapGenerator.BuildFromTileRoadmapsAsync(
                map,
                progress);

            RefreshGeneratedRoadmapConsumers(map, result.OutputPath);
            _webRoadmapAnalysis = _webRoadmapGenerator.Analyze(map);
            _webRoadmapResult = new WebRoadmapResult(
                "tiles",
                result.OutputPath,
                result.BackupPath,
                result.PixelWidth,
                result.PixelHeight,
                result.Elapsed.TotalSeconds,
                result.FileSizeBytes,
                result.TileImagesUsed,
                result.MissingTileImages,
                null,
                null,
                null);
            _webRoadmapProgress = 1d;
            _webRoadmapStatus = "tiles-built";
        }
        catch (Exception ex)
        {
            _webRoadmapStatus = "tiles-build-failed";
            _webRoadmapError = ex.Message;
            throw;
        }
        finally
        {
            _webRoadmapBusy = false;
        }
    }

    private async Task BuildRoadmapHdFromWebAsync(
        string? folderName,
        string? quality)
    {
        var map = ResolveWebRoadmapMap(folderName);
        var normalizedQuality = string.Equals(
            quality,
            "ultra",
            StringComparison.OrdinalIgnoreCase)
            ? "ultra"
            : "hd";
        BeginWebRoadmapBuild(map, "building-hd");

        try
        {
            var progress = new Progress<double>(value =>
            {
                _webRoadmapProgress = Math.Clamp(
                    double.IsFinite(value) ? value : 0d,
                    0d,
                    1d);
            });
            var result = await _webRoadmapVectorGenerator.BuildHdAsync(
                map,
                normalizedQuality,
                progress);

            _webRoadmapAnalysis = _webRoadmapGenerator.Analyze(map);
            var fileSize = File.Exists(result.OutputPath)
                ? new FileInfo(result.OutputPath).Length
                : null as long?;

            RefreshGeneratedRoadmapConsumers(map, result.OutputPath);

            _webRoadmapResult = new WebRoadmapResult(
                "hd",
                result.OutputPath,
                null,
                result.PixelWidth,
                result.PixelHeight,
                result.Elapsed.TotalSeconds,
                fileSize,
                null,
                null,
                result.TileFilesRead,
                result.SplinesDrawn,
                normalizedQuality);
            _webRoadmapProgress = 1d;
            _webRoadmapStatus = "hd-built";
        }
        catch (Exception ex)
        {
            _webRoadmapStatus = "hd-build-failed";
            _webRoadmapError = ex.Message;
            throw;
        }
        finally
        {
            _webRoadmapBusy = false;
        }
    }

    private async Task BuildRoadmapVectorFromWebAsync(string? folderName)
    {
        var map = ResolveWebRoadmapMap(folderName);
        BeginWebRoadmapBuild(map, "building-vector");

        try
        {
            var progress = new Progress<double>(value =>
            {
                _webRoadmapProgress = Math.Clamp(
                    double.IsFinite(value) ? value : 0d,
                    0d,
                    1d);
            });
            var result = await _webRoadmapVectorGenerator.BuildAsync(
                map,
                progress);

            RefreshGeneratedRoadmapConsumers(map, result.OutputPath);
            _webRoadmapAnalysis = _webRoadmapGenerator.Analyze(map);
            var fileSize = File.Exists(result.OutputPath)
                ? new FileInfo(result.OutputPath).Length
                : null as long?;
            _webRoadmapResult = new WebRoadmapResult(
                "vector",
                result.OutputPath,
                result.BackupPath,
                result.PixelWidth,
                result.PixelHeight,
                result.Elapsed.TotalSeconds,
                fileSize,
                null,
                null,
                result.TileFilesRead,
                result.SplinesDrawn,
                null);
            _webRoadmapProgress = 1d;
            _webRoadmapStatus = "vector-built";
        }
        catch (Exception ex)
        {
            _webRoadmapStatus = "vector-build-failed";
            _webRoadmapError = ex.Message;
            throw;
        }
        finally
        {
            _webRoadmapBusy = false;
        }
    }

    private void RefreshGeneratedRoadmapConsumers(
        OmsiMapInfo map,
        string generatedOutputPath)
    {
        var hdPath = Path.Combine(
            map.DirectoryPath,
            "texture",
            "map",
            OmsiRoadmapVectorGeneratorService.HdRoadmapFileName);
        var preferredPath = File.Exists(hdPath)
            ? hdPath
            : generatedOutputPath;

        _installedMaps = _installedMaps
            .Select(item =>
                string.Equals(
                    item.FolderName,
                    map.FolderName,
                    StringComparison.OrdinalIgnoreCase)
                    ? item with { RoadmapPath = preferredPath }
                    : item)
            .ToArray();

        // Force the 2D/3D WebView navigation payloads to resolve the new
        // bitmap and cached PNG on the very next state update.
        _webNavigationMapKey = null;
        _webNavigationRouteKey = null;

        // The retired WPF GPS is still used as an internal compatibility host
        // in a few paths. Drop its bitmap identity too so it cannot pin the
        // previous roadmap in memory.
        _loadedRoadmapPath = null;
        _loadedRoadmapBitmap = null;
        _loadedRoadmapLayout = null;
        _roadmapZoomInitialized = false;

        // HUD overlay has its own BitmapImage cache. Push the updated map
        // immediately rather than waiting for the 200 ms refresh timer.
        if (_hudOverlay is not null)
        {
            _hudOverlay.UpdateLocalTelemetry(
                _lastTelemetry,
                GetActiveMapForMultiplayer());
        }
    }

    private void OpenRoadmapFolderFromWeb(string? folderName)
    {
        var map = ResolveWebRoadmapMap(folderName);
        var folder = Path.Combine(map.DirectoryPath, "texture", "map");
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    private void BeginWebRoadmapBuild(OmsiMapInfo map, string status)
    {
        if (_webRoadmapBusy)
        {
            throw new InvalidOperationException(
                "Já existe uma geração de roadmap em andamento.");
        }

        _webRoadmapBusy = true;
        _webRoadmapSelectedFolder = map.FolderName;
        _webRoadmapStatus = status;
        _webRoadmapError = null;
        _webRoadmapProgress = 0d;
        _webRoadmapResult = null;
    }

    private OmsiMapInfo ResolveWebRoadmapMap(string? folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            throw new InvalidOperationException("Selecione um mapa do OMSI.");
        }

        var map = _installedMaps.FirstOrDefault(item =>
            string.Equals(
                item.FolderName,
                folderName.Trim(),
                StringComparison.OrdinalIgnoreCase));
        return map ?? throw new InvalidOperationException(
            "O mapa selecionado não está mais disponível no catálogo do OMSI.");
    }

    private sealed record WebRoadmapResult(
        string Mode,
        string OutputPath,
        string? BackupPath,
        int PixelWidth,
        int PixelHeight,
        double ElapsedSeconds,
        long? FileSizeBytes,
        int? TileImagesUsed,
        int? MissingTileImages,
        int? TileFilesRead,
        int? SplinesDrawn,
        string? Quality);
}
