using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NavBR.Client.Diagnostics;

namespace NavBR.Client.Maps;

/// <summary>
/// Generates a lightweight read-only roadmap preview in NavBR's WebView cache
/// when a community map does not provide whole.roadmap.bmp. Source geometry is
/// limited to real OMSI tile splines and scenery-object road paths.
/// </summary>
internal static class OmsiWebRoadmapFallbackCache
{
    private const double SampleSpacingMeters = 9d;
    private const int MaxSamplesPerSpline = 96;
    private const int PreferredPixelsPerTile = 180;
    private const int MaxBitmapDimension = 4096;
    private const int MaxTiles = 1600;
    private const string CacheVersion = "v2-real-road-geometry";

    private static readonly ConcurrentDictionary<string, Task> GenerationTasks =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, DateTimeOffset> RetryAfter =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan FailedGenerationRetryDelay =
        TimeSpan.FromSeconds(30);

    private sealed record SplinePlacement(
        double X,
        double Y,
        double RotationDegrees,
        double Length,
        double Radius);

    public static string? TryGetOrRequestUrl(OmsiMapInfo map)
    {
        try
        {
            var key = BuildCacheKey(map);
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var relativePath = Path.Combine(
                "generated-roadmaps",
                $"{key}.png");
            var outputPath = Path.Combine(
                WebRoadmapCache.CacheRoot,
                relativePath);

            if (File.Exists(outputPath) &&
                new FileInfo(outputPath).Length > 0)
            {
                return BuildCacheUrl(relativePath);
            }

            if (RetryAfter.TryGetValue(key, out var retryAfter) &&
                retryAfter > DateTimeOffset.UtcNow)
            {
                return null;
            }

            RequestGeneration(key, map, outputPath);
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static void RequestGeneration(
        string key,
        OmsiMapInfo map,
        string outputPath)
    {
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!GenerationTasks.TryAdd(key, completion.Task))
        {
            return;
        }

        NavBRAppLog.Info(
            "roadmap-fallback-generation-start",
            $"map={LogValue(map.FolderName)} key={ShortKey(key)}");

        var thread = new Thread(() =>
        {
            try
            {
                var generated = Generate(map, outputPath, out var detail);
                if (generated)
                {
                    RetryAfter.TryRemove(key, out _);
                }
                else
                {
                    RetryAfter[key] =
                        DateTimeOffset.UtcNow + FailedGenerationRetryDelay;
                }

                NavBRAppLog.Info(
                    generated
                        ? "roadmap-fallback-generation-success"
                        : "roadmap-fallback-generation-failed",
                    $"map={LogValue(map.FolderName)} key={ShortKey(key)} {detail}");
                completion.TrySetResult(generated);
            }
            catch (Exception ex)
            {
                RetryAfter[key] =
                    DateTimeOffset.UtcNow + FailedGenerationRetryDelay;
                NavBRAppLog.Info(
                    "roadmap-fallback-generation-error",
                    $"map={LogValue(map.FolderName)} key={ShortKey(key)} " +
                    $"type={ex.GetType().Name} message={LogValue(ex.Message)}");
                completion.TrySetResult(false);
            }
            finally
            {
                GenerationTasks.TryRemove(key, out _);
            }
        })
        {
            IsBackground = true,
            Name = "NavBR roadmap renderer"
        };

        try
        {
            // DrawingVisual/RenderTargetBitmap are WPF dispatcher objects.
            // Keep generation away from the UI thread, but use a dedicated
            // STA apartment so community maps without whole.roadmap.bmp can
            // actually render instead of failing silently on a pool MTA.
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        catch (Exception ex)
        {
            GenerationTasks.TryRemove(key, out _);
            RetryAfter[key] =
                DateTimeOffset.UtcNow + FailedGenerationRetryDelay;
            NavBRAppLog.Info(
                "roadmap-fallback-thread-start-failed",
                $"map={LogValue(map.FolderName)} key={ShortKey(key)} " +
                $"type={ex.GetType().Name} message={LogValue(ex.Message)}");
            completion.TrySetResult(false);
        }
    }

    private static string ShortKey(string key) =>
        key.Length <= 12 ? key : key[..12];

    private static string LogValue(string? value)
    {
        var normalized = (value ?? "-")
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return normalized.Length <= 160
            ? normalized
            : normalized[..160];
    }

    private static string? BuildCacheKey(OmsiMapInfo map)
    {
        try
        {
            var global = new FileInfo(map.GlobalConfigPath);
            var signature = string.Join(
                "|",
                CacheVersion,
                Path.GetFullPath(map.DirectoryPath).ToUpperInvariant(),
                map.CompatibilityId ?? string.Empty,
                map.TileCount,
                global.Exists ? global.Length : 0L,
                global.Exists ? global.LastWriteTimeUtc.Ticks : 0L);

            return Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(signature)))
                .ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static string BuildCacheUrl(string relativePath)
    {
        var escaped = string.Join(
            "/",
            relativePath
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
        return $"https://navbr-cache.local/{escaped}";
    }

    private static bool Generate(
        OmsiMapInfo map,
        string outputPath,
        out string detail)
    {
        detail = "reason=unknown";
        try
        {
            var layout = OmsiMapLayoutReader.TryRead(map.GlobalConfigPath);
            if (layout?.TileSize is not double tileSize ||
                tileSize <= 0d ||
                layout.GridWidth <= 0 ||
                layout.GridHeight <= 0)
            {
                detail = "reason=layout-unavailable";
                return false;
            }

            var tiles = ReadTileCatalog(
                map.DirectoryPath,
                map.GlobalConfigPath);
            if (tiles.Count == 0)
            {
                detail = "reason=no-tiles";
                return false;
            }

            if (tiles.Count > MaxTiles)
            {
                detail = $"reason=too-many-tiles tiles={tiles.Count}";
                return false;
            }

            var rawWidth = Math.Max(
                320,
                layout.GridWidth * PreferredPixelsPerTile);
            var rawHeight = Math.Max(
                320,
                layout.GridHeight * PreferredPixelsPerTile);
            var scale = Math.Min(
                1d,
                MaxBitmapDimension /
                (double)Math.Max(rawWidth, rawHeight));
            var width = Math.Max(320, (int)Math.Round(rawWidth * scale));
            var height = Math.Max(320, (int)Math.Round(rawHeight * scale));

            var visual = new DrawingVisual();
            var geometryCount = 0;
            using (var context = visual.RenderOpen())
            {
                context.DrawRectangle(
                    new SolidColorBrush(Color.FromRgb(5, 12, 18)),
                    null,
                    new System.Windows.Rect(0, 0, width, height));

                var shadowPen = new Pen(
                    new SolidColorBrush(Color.FromRgb(24, 31, 37)),
                    4.6d);
                var roadPen = new Pen(
                    new SolidColorBrush(Color.FromRgb(126, 143, 154)),
                    1.75d);
                shadowPen.Freeze();
                roadPen.Freeze();

                foreach (var tile in tiles)
                {
                    foreach (var spline in ReadSplines(tile.Value))
                    {
                        var points = SampleSpline(
                            tile.Key.X,
                            tile.Key.Y,
                            spline);
                        if (DrawRoad(
                                context,
                                shadowPen,
                                roadPen,
                                layout,
                                width,
                                height,
                                points))
                        {
                            geometryCount++;
                        }
                    }

                    foreach (var sceneryPath in
                             OmsiRouteSceneryPathGeometryReader.ReadAllRoadPaths(
                                 map.DirectoryPath,
                                 tile.Value))
                    {
                        var points = sceneryPath
                            .Select(point => new OmsiRouteTracePoint(
                                tile.Key.X,
                                tile.Key.Y,
                                point.TileX,
                                point.TileY))
                            .ToArray();

                        if (DrawRoad(
                                context,
                                shadowPen,
                                roadPen,
                                layout,
                                width,
                                height,
                                points))
                        {
                            geometryCount++;
                        }
                    }
                }
            }

            if (geometryCount == 0)
            {
                detail = $"reason=no-road-geometry tiles={tiles.Count}";
                return false;
            }

            var bitmap = new RenderTargetBitmap(
                width,
                height,
                96d,
                96d,
                PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();

            var directory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                detail = "reason=cache-directory-unavailable";
                return false;
            }

            Directory.CreateDirectory(directory);
            var temporaryPath = outputPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = File.Create(temporaryPath))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(stream);
                }

                File.Move(temporaryPath, outputPath, overwrite: true);
                detail =
                    $"reason=ok tiles={tiles.Count} geometry={geometryCount} " +
                    $"size={width}x{height}";
                return true;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            // Navigation keeps its route/vehicle overlays even if a community
            // map contains malformed geometry. Never turn fallback rendering
            // into a startup failure.
            detail =
                $"reason=exception type={ex.GetType().Name} " +
                $"message={LogValue(ex.Message)}";
            return false;
        }
    }

    private static bool DrawRoad(
        DrawingContext context,
        Pen shadowPen,
        Pen roadPen,
        OmsiMapLayout layout,
        int width,
        int height,
        IReadOnlyList<OmsiRouteTracePoint> points)
    {
        if (points.Count < 2)
        {
            return false;
        }

        var geometry = new StreamGeometry();
        using (var geometryContext = geometry.Open())
        {
            var started = false;
            foreach (var point in points)
            {
                if (!RoadmapTransform.TryToPixel(
                        layout,
                        width,
                        height,
                        point.GridX,
                        point.GridY,
                        point.TileX,
                        point.TileY,
                        out var x,
                        out var y))
                {
                    continue;
                }

                var pixel = new System.Windows.Point(x, y);
                if (!started)
                {
                    geometryContext.BeginFigure(
                        pixel,
                        isFilled: false,
                        isClosed: false);
                    started = true;
                }
                else
                {
                    geometryContext.LineTo(
                        pixel,
                        isStroked: true,
                        isSmoothJoin: true);
                }
            }
        }

        geometry.Freeze();
        if (geometry.Bounds.IsEmpty)
        {
            return false;
        }

        context.DrawGeometry(null, shadowPen, geometry);
        context.DrawGeometry(null, roadPen, geometry);
        return true;
    }

    private static IReadOnlyList<SplinePlacement> ReadSplines(string tilePath)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(tilePath);
        }
        catch
        {
            return Array.Empty<SplinePlacement>();
        }

        var result = new List<SplinePlacement>();
        for (var index = 0; index < lines.Length - 11; index++)
        {
            var token = lines[index].Trim();
            if (!string.Equals(
                    token,
                    "[spline]",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    token,
                    "[spline_h]",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryParseDouble(lines[index + 6], out var x) ||
                !TryParseDouble(lines[index + 8], out var y) ||
                !TryParseDouble(lines[index + 9], out var rotation) ||
                !TryParseDouble(lines[index + 10], out var length) ||
                !TryParseDouble(lines[index + 11], out var radius) ||
                length <= 0d ||
                length > 20_000d)
            {
                continue;
            }

            result.Add(new SplinePlacement(
                x,
                y,
                rotation,
                length,
                radius));
        }

        return result;
    }

    private static IReadOnlyList<OmsiRouteTracePoint> SampleSpline(
        int gridX,
        int gridY,
        SplinePlacement spline)
    {
        var sampleCount = Math.Clamp(
            (int)Math.Ceiling(spline.Length / SampleSpacingMeters),
            2,
            MaxSamplesPerSpline);
        var points = new List<OmsiRouteTracePoint>(sampleCount + 1);
        var rotation = spline.RotationDegrees * Math.PI / 180d;
        var sin = Math.Sin(rotation);
        var cos = Math.Cos(rotation);

        for (var index = 0; index <= sampleCount; index++)
        {
            var distance = spline.Length * index / sampleCount;
            double localX;
            double localY;
            if (Math.Abs(spline.Radius) > 0.001d)
            {
                var angle = distance / spline.Radius;
                localX = spline.Radius - spline.Radius * Math.Cos(angle);
                localY = spline.Radius * Math.Sin(angle);
            }
            else
            {
                localX = 0d;
                localY = distance;
            }

            var tileX =
                spline.X + localX * cos + localY * sin;
            var tileY =
                spline.Y - localX * sin + localY * cos;
            if (double.IsFinite(tileX) && double.IsFinite(tileY))
            {
                points.Add(new OmsiRouteTracePoint(
                    gridX,
                    gridY,
                    tileX,
                    tileY));
            }
        }

        return points;
    }

    private static Dictionary<(int X, int Y), string> ReadTileCatalog(
        string mapDirectory,
        string globalConfigPath)
    {
        var result = new Dictionary<(int X, int Y), string>();
        string[] lines;
        try
        {
            lines = File.ReadAllLines(globalConfigPath);
        }
        catch
        {
            return result;
        }

        for (var index = 0; index < lines.Length - 3; index++)
        {
            if (!string.Equals(
                    lines[index].Trim(),
                    "[map]",
                    StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(
                    lines[index + 1].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var x) ||
                !int.TryParse(
                    lines[index + 2].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var y))
            {
                continue;
            }

            var relative = lines[index + 3].Trim()
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            try
            {
                var path = Path.IsPathRooted(relative)
                    ? relative
                    : Path.GetFullPath(Path.Combine(
                        mapDirectory,
                        relative));
                if (File.Exists(path))
                {
                    result.TryAdd((x, y), path);
                }
            }
            catch
            {
            }
        }

        return result;
    }

    private static bool TryParseDouble(
        string value,
        out double result) =>
        double.TryParse(
            value.Trim().Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result) &&
        double.IsFinite(result);
}
