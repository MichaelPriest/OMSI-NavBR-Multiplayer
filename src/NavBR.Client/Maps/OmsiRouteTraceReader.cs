using System.Globalization;
using System.IO;

namespace NavBR.Client.Maps;

public sealed record OmsiRouteTracePoint(int GridX, int GridY, double TileX, double TileY);

public sealed record OmsiRouteTraceDiagnostics(
    string MapFolder,
    string Mode,
    string? TrackName,
    string? ActiveLine,
    string? LookupValue,
    int EntryCount,
    int PointCount);

/// <summary>
/// Reads the active OMSI timetable track (.ttr). Whenever possible NavBR now
/// resolves track entries against the real spline placement stored in the tile
/// .map file; if that detailed geometry is unavailable it keeps the coarse,
/// trustworthy tile-centre fallback instead of inventing a road shape.
/// </summary>
public static class OmsiRouteTraceReader
{
    private const double MaxDetailedGapMeters = 120d;
    private static readonly object DiagnosticLock = new();
    private static string? _lastDiagnosticSignature;
    private static OmsiRouteTraceDiagnostics? _lastDiagnostics;

    public static OmsiRouteTraceDiagnostics? LastDiagnostics
    {
        get
        {
            lock (DiagnosticLock)
            {
                return _lastDiagnostics;
            }
        }
    }

    public static IReadOnlyList<OmsiRouteTracePoint> TryRead(
        OmsiMapInfo map,
        OmsiMapLayout layout,
        string? activeTrackOrTarget,
        string? activeLine = null)
    {
        if (layout.TileSize is not double tileSize)
        {
            WriteDiagnostics(
                map,
                null,
                activeLine,
                activeTrackOrTarget,
                "layout-missing",
                0,
                0);
            return Array.Empty<OmsiRouteTracePoint>();
        }

        try
        {
            var trackPath = FindTrackPath(map.DirectoryPath, activeTrackOrTarget);
            if (trackPath is null)
            {
                var resolvedTrackName = ResolveTrackNameFromTrip(
                    map.DirectoryPath,
                    activeLine,
                    activeTrackOrTarget);
                trackPath = FindTrackPath(map.DirectoryPath, resolvedTrackName);
            }

            if (trackPath is null)
            {
                WriteDiagnostics(
                    map,
                    null,
                    activeLine,
                    activeTrackOrTarget,
                    "track-not-found",
                    0,
                    0);
                return Array.Empty<OmsiRouteTracePoint>();
            }

            var tileCatalog = ReadTileIndexCatalog(map.GlobalConfigPath);
            if (tileCatalog.Count == 0)
            {
                WriteDiagnostics(
                    map,
                    trackPath,
                    activeLine,
                    activeTrackOrTarget,
                    "tile-catalog-empty",
                    0,
                    0);
                return Array.Empty<OmsiRouteTracePoint>();
            }

            var entries = ReadTrackEntries(File.ReadAllLines(trackPath), tileCatalog);
            if (entries.Count == 0)
            {
                WriteDiagnostics(
                    map,
                    trackPath,
                    activeLine,
                    activeTrackOrTarget,
                    "track-empty",
                    0,
                    0);
                return Array.Empty<OmsiRouteTracePoint>();
            }

            var splineTrace = OmsiRouteSplineGeometryReader.TryBuild(map, layout, entries);
            if (splineTrace.Count >= 2 && IsDetailedTraceContinuous(splineTrace, tileSize))
            {
                WriteDiagnostics(
                    map,
                    trackPath,
                    activeLine,
                    activeTrackOrTarget,
                    "detailed",
                    entries.Count,
                    splineTrace.Count);
                return splineTrace;
            }

            var fallback = BuildTileFallback(entries, tileSize);
            WriteDiagnostics(
                map,
                trackPath,
                activeLine,
                activeTrackOrTarget,
                splineTrace.Count >= 2 ? "tile-fallback-gap" : "tile-fallback",
                entries.Count,
                fallback.Count);
            return fallback;
        }
        catch (IOException)
        {
            WriteDiagnostics(
                map,
                null,
                activeLine,
                activeTrackOrTarget,
                "io-error",
                0,
                0);
            return Array.Empty<OmsiRouteTracePoint>();
        }
        catch (UnauthorizedAccessException)
        {
            WriteDiagnostics(
                map,
                null,
                activeLine,
                activeTrackOrTarget,
                "access-denied",
                0,
                0);
            return Array.Empty<OmsiRouteTracePoint>();
        }
    }

    private static bool IsDetailedTraceContinuous(
        IReadOnlyList<OmsiRouteTracePoint> points,
        double tileSize)
    {
        var maxGapSquared = MaxDetailedGapMeters * MaxDetailedGapMeters;
        for (var index = 1; index < points.Count; index++)
        {
            var previous = points[index - 1];
            var current = points[index];
            var previousX = previous.GridX * tileSize + previous.TileX;
            var previousY = previous.GridY * tileSize + previous.TileY;
            var currentX = current.GridX * tileSize + current.TileX;
            var currentY = current.GridY * tileSize + current.TileY;
            var dx = currentX - previousX;
            var dy = currentY - previousY;
            if (dx * dx + dy * dy > maxGapSquared)
            {
                return false;
            }
        }

        return true;
    }

    private static List<OmsiRouteTrackEntry> ReadTrackEntries(
        string[] lines,
        IReadOnlyDictionary<int, (int GridX, int GridY)> tileCatalog)
    {
        var entries = new List<OmsiRouteTrackEntry>();

        for (var i = 0; i < lines.Length; i++)
        {
            if (!string.Equals(lines[i].Trim(), "[track_entry]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // OMSI 2 TTR [track_entry] layout used by the timetable editor:
            // object/spline id
            // path id
            // tile id (Kachel-ID: index in the [map] list of global.cfg)
            // internal/auxiliary value (not needed to locate the tile)
            // approximate path length
            // flags/reserved (normally 0)
            //
            // The third and fourth values are NOT GridX/GridY. This matters on
            // world-coordinate maps, where real grid coordinates can be values
            // such as -14445/8871 while TTR tile ids remain small integers.
            if (i + 5 >= lines.Length ||
                !int.TryParse(lines[i + 1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectId) ||
                !int.TryParse(lines[i + 2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pathId) ||
                !int.TryParse(lines[i + 3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tileId) ||
                !tileCatalog.TryGetValue(tileId, out var grid))
            {
                continue;
            }

            var pathLength = 0d;
            double.TryParse(
                lines[i + 5].Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out pathLength);

            entries.Add(new OmsiRouteTrackEntry(
                objectId,
                pathId,
                grid.GridX,
                grid.GridY,
                double.IsFinite(pathLength) && pathLength > 0d ? pathLength : 0d));
        }

        return entries;
    }

    private static IReadOnlyDictionary<int, (int GridX, int GridY)> ReadTileIndexCatalog(
        string globalConfigPath)
    {
        var result = new Dictionary<int, (int GridX, int GridY)>();
        var lines = File.ReadAllLines(globalConfigPath);
        var tileId = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            if (!string.Equals(lines[i].Trim(), "[map]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // OMSI timetable files reference the ordered [map] entries by
            // Kachel-ID. The entry itself stores the real grid X/Y directly
            // below [map], followed by the tile .map filename.
            if (i + 2 < lines.Length &&
                int.TryParse(lines[i + 1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var gridX) &&
                int.TryParse(lines[i + 2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var gridY))
            {
                result[tileId] = (gridX, gridY);
            }

            tileId++;
        }

        return result;
    }

    private static IReadOnlyList<OmsiRouteTracePoint> BuildTileFallback(
        IReadOnlyList<OmsiRouteTrackEntry> entries,
        double tileSize)
    {
        var points = new List<OmsiRouteTracePoint>();
        (int X, int Y)? previousGrid = null;

        foreach (var entry in entries)
        {
            var grid = (entry.GridX, entry.GridY);
            if (previousGrid == grid)
            {
                continue;
            }

            previousGrid = grid;
            points.Add(new OmsiRouteTracePoint(
                entry.GridX,
                entry.GridY,
                tileSize / 2d,
                tileSize / 2d));
        }

        return points;
    }

    private static string? FindTrackPath(string mapDirectory, string? activeTrackName)
    {
        if (string.IsNullOrWhiteSpace(activeTrackName))
        {
            return null;
        }

        var requested = Path.GetFileNameWithoutExtension(activeTrackName.Trim());
        if (string.IsNullOrWhiteSpace(requested))
        {
            return null;
        }

        var timetableDirectories = GetTimetableDirectories(mapDirectory);
        foreach (var directory in timetableDirectories)
        {
            try
            {
                var exact = Directory
                    .EnumerateFiles(directory, "*.ttr", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(path => string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        requested,
                        StringComparison.OrdinalIgnoreCase));
                if (exact is not null)
                {
                    return exact;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        var normalizedRequested = Normalize(requested);
        foreach (var directory in timetableDirectories)
        {
            try
            {
                var normalized = Directory
                    .EnumerateFiles(directory, "*.ttr", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(path => Normalize(Path.GetFileNameWithoutExtension(path)) == normalizedRequested);
                if (normalized is not null)
                {
                    return normalized;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return null;
    }

    /// <summary>
    /// OMSI trip files (.ttp) link a trip to a track using the [trip] block:
    /// line 1 = track/TTR name, line 2 = destination, line 3 = line number.
    /// Some runtimes expose the destination but not the track name through the
    /// in-memory timetable record. In that case, line + destination can resolve
    /// the real TTR without guessing route geometry.
    /// </summary>
    private static string? ResolveTrackNameFromTrip(
        string mapDirectory,
        string? activeLine,
        string? activeTarget)
    {
        if (string.IsNullOrWhiteSpace(activeLine) && string.IsNullOrWhiteSpace(activeTarget))
        {
            return null;
        }

        var normalizedLine = Normalize(activeLine ?? string.Empty);
        var normalizedTarget = Normalize(activeTarget ?? string.Empty);
        var fallbackMatches = new List<string>();

        foreach (var directory in GetTimetableDirectories(mapDirectory))
        {
            IEnumerable<string> tripFiles;
            try
            {
                tripFiles = Directory.EnumerateFiles(directory, "*.ttp", SearchOption.TopDirectoryOnly).ToArray();
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var tripPath in tripFiles)
            {
                try
                {
                    var lines = File.ReadAllLines(tripPath);
                    for (var i = 0; i < lines.Length - 3; i++)
                    {
                        if (!string.Equals(lines[i].Trim(), "[trip]", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var trackName = lines[i + 1].Trim();
                        var target = lines[i + 2].Trim();
                        var line = lines[i + 3].Trim();
                        if (string.IsNullOrWhiteSpace(trackName))
                        {
                            break;
                        }

                        var lineMatches = string.IsNullOrWhiteSpace(normalizedLine) ||
                                          Normalize(line) == normalizedLine;
                        var targetMatches = string.IsNullOrWhiteSpace(normalizedTarget) ||
                                            Normalize(target) == normalizedTarget ||
                                            Normalize(Path.GetFileNameWithoutExtension(tripPath)) == normalizedTarget;

                        if (lineMatches && targetMatches)
                        {
                            return trackName;
                        }

                        if (lineMatches)
                        {
                            fallbackMatches.Add(trackName);
                        }

                        break;
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        return fallbackMatches
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .Count() == 1
            ? fallbackMatches[0]
            : null;
    }

    private static IReadOnlyList<string> GetTimetableDirectories(string mapDirectory)
    {
        var timetableDirectories = new List<string>();
        var baseTimetable = Path.Combine(mapDirectory, "TTData");
        if (Directory.Exists(baseTimetable))
        {
            timetableDirectories.Add(baseTimetable);
        }

        var chronoDirectory = Path.Combine(mapDirectory, "Chrono");
        if (Directory.Exists(chronoDirectory))
        {
            try
            {
                timetableDirectories.AddRange(
                    Directory.EnumerateDirectories(chronoDirectory, "*", SearchOption.TopDirectoryOnly)
                        .Select(path => Path.Combine(path, "TTData"))
                        .Where(Directory.Exists));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return timetableDirectories;
    }

    private static void WriteDiagnostics(
        OmsiMapInfo map,
        string? trackPath,
        string? activeLine,
        string? activeTrackOrTarget,
        string mode,
        int entryCount,
        int pointCount)
    {
        var trackName = trackPath is null
            ? "-"
            : Path.GetFileName(trackPath);
        var signature = string.Join(
            "|",
            map.FolderName,
            trackName,
            activeLine ?? string.Empty,
            activeTrackOrTarget ?? string.Empty,
            mode,
            entryCount.ToString(CultureInfo.InvariantCulture),
            pointCount.ToString(CultureInfo.InvariantCulture));

        lock (DiagnosticLock)
        {
            _lastDiagnostics = new OmsiRouteTraceDiagnostics(
                map.FolderName,
                mode,
                trackPath is null ? null : Path.GetFileName(trackPath),
                activeLine,
                activeTrackOrTarget,
                entryCount,
                pointCount);

            if (string.Equals(_lastDiagnosticSignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            _lastDiagnosticSignature = signature;
        }

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                return;
            }

            var directory = Path.Combine(localAppData, "OMSI NavBR Multiplayer");
            Directory.CreateDirectory(directory);
            var logPath = Path.Combine(directory, "navbr-route.log");
            var line = string.Join(
                " ",
                $"[{DateTimeOffset.Now:O}]",
                $"map={ToLogValue(map.FolderName)}",
                $"line={ToLogValue(activeLine)}",
                $"route={ToLogValue(activeTrackOrTarget)}",
                $"track={ToLogValue(trackName)}",
                $"mode={mode}",
                $"entries={entryCount.ToString(CultureInfo.InvariantCulture)}",
                $"points={pointCount.ToString(CultureInfo.InvariantCulture)}");

            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string ToLogValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim()
            .Replace(' ', '_');
    }

    private static string Normalize(string value) =>
        new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
