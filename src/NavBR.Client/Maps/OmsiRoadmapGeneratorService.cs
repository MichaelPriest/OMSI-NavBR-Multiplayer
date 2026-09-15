using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NavBR.Client.Maps;

public sealed record OmsiRoadmapAnalysis(
    string MapDirectory,
    string OutputPath,
    int TileImageCount,
    int MinGridX,
    int MinGridY,
    int MaxGridX,
    int MaxGridY,
    int TilePixelWidth,
    int TilePixelHeight,
    int OutputPixelWidth,
    int OutputPixelHeight,
    int MissingTileImages,
    bool ExistingWholeRoadmap,
    long EstimatedBytes)
{
    public bool CanBuild => TileImageCount > 0 && OutputPixelWidth > 0 && OutputPixelHeight > 0;
}

public sealed record OmsiRoadmapBuildResult(
    string OutputPath,
    string? BackupPath,
    int TileImagesUsed,
    int MissingTileImages,
    int PixelWidth,
    int PixelHeight,
    long FileSizeBytes,
    TimeSpan Elapsed);

public sealed class OmsiRoadmapGeneratorService
{
    private static readonly Regex TileRoadmapPattern = new(
        @"^tile_(?<x>-?\d+)_(?<y>-?\d+)(?:\.map)?\.roadmap\.bmp$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private sealed record TileBitmap(
        int GridX,
        int GridY,
        string Path,
        int PixelWidth,
        int PixelHeight);

    public OmsiRoadmapAnalysis Analyze(OmsiMapInfo map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var tiles = DiscoverTileRoadmaps(map.DirectoryPath);
        if (tiles.Count == 0)
        {
            return new OmsiRoadmapAnalysis(
                map.DirectoryPath,
                GetOutputPath(map.DirectoryPath),
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                File.Exists(GetOutputPath(map.DirectoryPath)),
                0);
        }

        var tileWidth = tiles[0].PixelWidth;
        var tileHeight = tiles[0].PixelHeight;
        if (tiles.Any(tile => tile.PixelWidth != tileWidth || tile.PixelHeight != tileHeight))
        {
            throw new InvalidDataException(
                "Os roadmaps por tile têm dimensões diferentes. O NavBR não vai combinar arquivos incompatíveis automaticamente.");
        }

        var minX = tiles.Min(tile => tile.GridX);
        var maxX = tiles.Max(tile => tile.GridX);
        var minY = tiles.Min(tile => tile.GridY);
        var maxY = tiles.Max(tile => tile.GridY);
        var columns = checked(maxX - minX + 1);
        var rows = checked(maxY - minY + 1);
        var outputWidth = checked(columns * tileWidth);
        var outputHeight = checked(rows * tileHeight);
        var expectedTileCount = checked(columns * rows);
        var missing = Math.Max(0, expectedTileCount - tiles.Count);
        var rowStride = Align4(checked(outputWidth * 3L));
        var estimated = checked(54L + rowStride * outputHeight);

        return new OmsiRoadmapAnalysis(
            map.DirectoryPath,
            GetOutputPath(map.DirectoryPath),
            tiles.Count,
            minX,
            minY,
            maxX,
            maxY,
            tileWidth,
            tileHeight,
            outputWidth,
            outputHeight,
            missing,
            File.Exists(GetOutputPath(map.DirectoryPath)),
            estimated);
    }

    public Task<OmsiRoadmapBuildResult> BuildFromTileRoadmapsAsync(
        OmsiMapInfo map,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => BuildFromTileRoadmaps(map, progress, cancellationToken),
            cancellationToken);
    }

    private OmsiRoadmapBuildResult BuildFromTileRoadmaps(
        OmsiMapInfo map,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var analysis = Analyze(map);
        if (!analysis.CanBuild)
        {
            throw new InvalidOperationException(
                "Nenhum roadmap por tile foi encontrado. Use o modo Vetorial quando ele estiver disponível ou gere pelo menos as imagens por tile.");
        }

        if (analysis.OutputPixelWidth > 100_000 || analysis.OutputPixelHeight > 100_000)
        {
            throw new InvalidOperationException(
                $"O roadmap resultaria em {analysis.OutputPixelWidth}x{analysis.OutputPixelHeight} px. " +
                "A geração foi bloqueada para evitar um arquivo impraticável.");
        }

        if (analysis.EstimatedBytes > 2_000_000_000L)
        {
            throw new InvalidOperationException(
                $"O roadmap estimado ultrapassa 2 GB ({FormatBytes(analysis.EstimatedBytes)}). " +
                "Reduza a resolução das imagens por tile ou use o modo Vetorial.");
        }

        var tiles = DiscoverTileRoadmaps(map.DirectoryPath)
            .ToDictionary(tile => (tile.GridX, tile.GridY));
        var outputPath = analysis.OutputPath;
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var temporaryPath = outputPath + ".navbr.tmp";
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }

        string? backupPath = null;
        try
        {
            WriteTopDownBmp(
                temporaryPath,
                analysis,
                tiles,
                progress,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(outputPath))
            {
                backupPath = Path.Combine(
                    Path.GetDirectoryName(outputPath)!,
                    $"whole.roadmap.backup-{DateTime.Now:yyyyMMdd-HHmmss}.bmp");
                File.Copy(outputPath, backupPath, overwrite: false);
            }

            File.Move(temporaryPath, outputPath, overwrite: true);
            var size = new FileInfo(outputPath).Length;
            progress?.Report(1d);

            return new OmsiRoadmapBuildResult(
                outputPath,
                backupPath,
                analysis.TileImageCount,
                analysis.MissingTileImages,
                analysis.OutputPixelWidth,
                analysis.OutputPixelHeight,
                size,
                DateTimeOffset.UtcNow - started);
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
    }

    private static void WriteTopDownBmp(
        string outputPath,
        OmsiRoadmapAnalysis analysis,
        IReadOnlyDictionary<(int X, int Y), TileBitmap> tiles,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var width = analysis.OutputPixelWidth;
        var height = analysis.OutputPixelHeight;
        var tileWidth = analysis.TilePixelWidth;
        var tileHeight = analysis.TilePixelHeight;
        var rawRowBytes = checked(width * 3);
        var outputStride = checked((int)Align4(rawRowBytes));
        var imageSize = checked((long)outputStride * height);
        var fileSize = checked(54L + imageSize);

        using var stream = new FileStream(
            outputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 1024,
            FileOptions.SequentialScan);
        using var writer = new BinaryWriter(stream);

        // BITMAPFILEHEADER
        writer.Write((ushort)0x4D42);
        writer.Write((uint)fileSize);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((uint)54);

        // BITMAPINFOHEADER. Negative height creates a top-down bitmap, which
        // matches WPF pixel coordinates and avoids reversing the final file.
        writer.Write((uint)40);
        writer.Write(width);
        writer.Write(-height);
        writer.Write((ushort)1);
        writer.Write((ushort)24);
        writer.Write((uint)0);
        writer.Write((uint)imageSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write((uint)0);
        writer.Write((uint)0);

        var outputRow = new byte[outputStride];
        var tileRowBuffer = new byte[tileWidth * 3];
        var gridColumns = analysis.MaxGridX - analysis.MinGridX + 1;
        var renderedRows = 0;

        for (var gridY = analysis.MaxGridY; gridY >= analysis.MinGridY; gridY--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowSources = new Dictionary<int, BitmapSource>();
            for (var gridX = analysis.MinGridX; gridX <= analysis.MaxGridX; gridX++)
            {
                if (!tiles.TryGetValue((gridX, gridY), out var tile))
                {
                    continue;
                }

                rowSources[gridX] = LoadAsBgr24(tile.Path);
            }

            for (var tilePixelY = 0; tilePixelY < tileHeight; tilePixelY++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Array.Fill(outputRow, (byte)7);

                for (var column = 0; column < gridColumns; column++)
                {
                    var gridX = analysis.MinGridX + column;
                    if (!rowSources.TryGetValue(gridX, out var bitmap))
                    {
                        continue;
                    }

                    bitmap.CopyPixels(
                        new System.Windows.Int32Rect(0, tilePixelY, tileWidth, 1),
                        tileRowBuffer,
                        tileRowBuffer.Length,
                        0);
                    Buffer.BlockCopy(
                        tileRowBuffer,
                        0,
                        outputRow,
                        column * tileWidth * 3,
                        tileRowBuffer.Length);
                }

                writer.Write(outputRow);
                renderedRows++;
                if (renderedRows % 32 == 0)
                {
                    progress?.Report((double)renderedRows / height);
                }
            }
        }
    }

    private static List<TileBitmap> DiscoverTileRoadmaps(string mapDirectory)
    {
        var roadmapDirectory = Path.Combine(mapDirectory, "texture", "map");
        if (!Directory.Exists(roadmapDirectory))
        {
            return [];
        }

        var result = new List<TileBitmap>();
        foreach (var path in Directory.EnumerateFiles(
                     roadmapDirectory,
                     "*.roadmap.bmp",
                     SearchOption.TopDirectoryOnly))
        {
            var match = TileRoadmapPattern.Match(Path.GetFileName(path));
            if (!match.Success ||
                !int.TryParse(match.Groups["x"].Value, out var gridX) ||
                !int.TryParse(match.Groups["y"].Value, out var gridY))
            {
                continue;
            }

            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var decoder = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0)
                {
                    continue;
                }

                result.Add(new TileBitmap(
                    gridX,
                    gridY,
                    path,
                    frame.PixelWidth,
                    frame.PixelHeight));
            }
            catch
            {
                // One broken tile should not prevent analysis of all others.
            }
        }

        return result;
    }

    private static BitmapSource LoadAsBgr24(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var source = decoder.Frames[0];

        if (source.Format == PixelFormats.Bgr24)
        {
            source.Freeze();
            return source;
        }

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0d);
        converted.Freeze();
        return converted;
    }

    private static string GetOutputPath(string mapDirectory) =>
        Path.Combine(mapDirectory, "texture", "map", "whole.roadmap.bmp");

    private static long Align4(long value) => (value + 3L) & ~3L;

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024L * 1024L) return $"{bytes / 1024d:F1} KB";
        if (bytes < 1024L * 1024L * 1024L) return $"{bytes / (1024d * 1024d):F1} MB";
        return $"{bytes / (1024d * 1024d * 1024d):F2} GB";
    }
}
