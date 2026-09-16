using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NavBR.Client.Maps;

public sealed record OmsiRoadmapVectorBuildResult(
    string OutputPath,
    string? BackupPath,
    int TileFilesRead,
    int SplinesDrawn,
    int PixelWidth,
    int PixelHeight,
    TimeSpan Elapsed);

public sealed class OmsiRoadmapVectorGeneratorService
{
    private const double SampleSpacingMeters = 7d;
    private const int MaxSamplesPerSpline = 128;
    private const int PreferredPixelsPerTile = 220;
    private const int MaxBitmapDimension = 6144;

    private sealed record SplinePlacement(
        double X,
        double Y,
        double RotationDegrees,
        double Length,
        double Radius);

    public Task<OmsiRoadmapVectorBuildResult> BuildAsync(
        OmsiMapInfo map,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Build(map, progress, cancellationToken), cancellationToken);
    }

    private static OmsiRoadmapVectorBuildResult Build(
        OmsiMapInfo map,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var layout = OmsiMapLayoutReader.TryRead(map.GlobalConfigPath)
            ?? throw new InvalidOperationException("Não foi possível ler a grade do global.cfg.");

        var tileCatalog = ReadTileCatalog(map.DirectoryPath, map.GlobalConfigPath);
        if (tileCatalog.Count == 0)
        {
            throw new InvalidOperationException("Nenhum tile .map foi encontrado no global.cfg.");
        }

        var columns = Math.Max(1, layout.MaxGridX - layout.MinGridX + 1);
        var rows = Math.Max(1, layout.MaxGridY - layout.MinGridY + 1);
        var rawWidth = Math.Max(256, columns * PreferredPixelsPerTile);
        var rawHeight = Math.Max(256, rows * PreferredPixelsPerTile);
        var scale = Math.Min(1d, MaxBitmapDimension / (double)Math.Max(rawWidth, rawHeight));
        var width = Math.Max(256, (int)Math.Round(rawWidth * scale));
        var height = Math.Max(256, (int)Math.Round(rawHeight * scale));

        var visual = new DrawingVisual();
        var splineCount = 0;
        var processedTiles = 0;

        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(5, 12, 18)),
                null,
                new Rect(0, 0, width, height));

            var shadowPen = new Pen(new SolidColorBrush(Color.FromRgb(24, 31, 37)), 4.2d);
            var roadPen = new Pen(new SolidColorBrush(Color.FromRgb(120, 134, 145)), 1.65d);
            shadowPen.Freeze();
            roadPen.Freeze();

            foreach (var tile in tileCatalog)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processedTiles++;

                foreach (var spline in ReadSplines(tile.Value))
                {
                    var points = SampleSpline(tile.Key.X, tile.Key.Y, spline);
                    if (points.Count < 2)
                    {
                        continue;
                    }

                    var geometry = new StreamGeometry();
                    using (var geometryContext = geometry.Open())
                    {
                        var startedFigure = false;
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
                                    out var pixelX,
                                    out var pixelY))
                            {
                                continue;
                            }

                            var p = new System.Windows.Point(pixelX, pixelY);
                            if (!startedFigure)
                            {
                                geometryContext.BeginFigure(p, isFilled: false, isClosed: false);
                                startedFigure = true;
                            }
                            else
                            {
                                geometryContext.LineTo(p, isStroked: true, isSmoothJoin: true);
                            }
                        }
                    }

                    geometry.Freeze();
                    if (geometry.Bounds.IsEmpty)
                    {
                        continue;
                    }

                    context.DrawGeometry(null, shadowPen, geometry);
                    context.DrawGeometry(null, roadPen, geometry);
                    splineCount++;
                }

                if (processedTiles % 4 == 0)
                {
                    progress?.Report(processedTiles / (double)tileCatalog.Count * 0.92d);
                }
            }
        }

        if (splineCount == 0)
        {
            throw new InvalidOperationException(
                "Nenhuma spline direta pôde ser lida dos tiles. Este mapa pode depender principalmente de objetos/crossings; o suporte vetorial será ampliado para esses casos.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var bitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        progress?.Report(0.96d);

        var outputDirectory = Path.Combine(map.DirectoryPath, "texture", "map");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "whole.roadmap.bmp");
        var temporaryPath = outputPath + ".vector.navbr.tmp";
        var metadataPath = Path.Combine(outputDirectory, "whole.roadmap.navbr.txt");
        string? backupPath = null;

        try
        {
            using (var stream = File.Create(temporaryPath))
            {
                var encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
            }

            if (File.Exists(outputPath))
            {
                backupPath = Path.Combine(
                    outputDirectory,
                    $"whole.roadmap.backup-{DateTime.Now:yyyyMMdd-HHmmss}.bmp");
                File.Copy(outputPath, backupPath, overwrite: false);
            }

            File.Move(temporaryPath, outputPath, overwrite: true);
            File.WriteAllText(
                metadataPath,
                $"NavBR Roadmap Studio - vector roadmap{Environment.NewLine}" +
                $"Generated: {DateTimeOffset.Now:O}{Environment.NewLine}" +
                $"Map: {map.DisplayName}{Environment.NewLine}" +
                $"Tiles: {tileCatalog.Count}{Environment.NewLine}" +
                $"Direct splines: {splineCount}{Environment.NewLine}" +
                $"Size: {width}x{height}{Environment.NewLine}" +
                "Source: global.cfg + tile .map [spline]/[spline_h]. No OMSI Editor required.\n");
        }
        catch
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
            throw;
        }

        progress?.Report(1d);
        return new OmsiRoadmapVectorBuildResult(
            outputPath,
            backupPath,
            tileCatalog.Count,
            splineCount,
            width,
            height,
            DateTimeOffset.UtcNow - started);
    }

    private static Dictionary<(int X, int Y), string> ReadTileCatalog(
        string mapDirectory,
        string globalConfigPath)
    {
        var result = new Dictionary<(int X, int Y), string>();
        var lines = File.ReadAllLines(globalConfigPath);

        for (var index = 0; index < lines.Length - 3; index++)
        {
            if (!string.Equals(lines[index].Trim(), "[map]", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(lines[index + 1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !int.TryParse(lines[index + 2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
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

            var path = Path.IsPathRooted(relative)
                ? relative
                : Path.GetFullPath(Path.Combine(mapDirectory, relative));
            if (File.Exists(path))
            {
                result.TryAdd((x, y), path);
            }
        }

        return result;
    }

    private static IEnumerable<SplinePlacement> ReadSplines(string tilePath)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(tilePath);
        }
        catch
        {
            yield break;
        }

        for (var index = 0; index < lines.Length - 11; index++)
        {
            var token = lines[index].Trim();
            if (!string.Equals(token, "[spline]", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(token, "[spline_h]", StringComparison.OrdinalIgnoreCase))
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

            yield return new SplinePlacement(x, y, rotation, length, radius);
        }
    }

    private static List<OmsiRouteTracePoint> SampleSpline(
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
        var sinRotation = Math.Sin(rotation);
        var cosRotation = Math.Cos(rotation);

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

            var tileX = spline.X + localX * cosRotation + localY * sinRotation;
            var tileY = spline.Y - localX * sinRotation + localY * cosRotation;
            if (double.IsFinite(tileX) && double.IsFinite(tileY))
            {
                points.Add(new OmsiRouteTracePoint(gridX, gridY, tileX, tileY));
            }
        }

        return points;
    }

    private static bool TryParseDouble(string value, out double result) =>
        double.TryParse(
            value.Trim().Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result) &&
        double.IsFinite(result);
}
