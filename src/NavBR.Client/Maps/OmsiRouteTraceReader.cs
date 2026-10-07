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
/// Reads the active OMSI timetable route from the trip's real lane sequence.
/// Tracks (.ttr) are used when present; ordinary bus trips are reconstructed
/// from station links (StnLinks.cfg). Every entry is then resolved against the
/// actual spline/scenery path geometry in the map. Incomplete geometry fails
/// closed instead of drawing a synthetic tile-centre route.
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
        string? activeLine = null,
        string? activeDestination = null)
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
            var tileCatalog = ReadTileIndexCatalog(map.GlobalConfigPath);
            if (tileCatalog.Count == 0)
            {
                WriteDiagnostics(
                    map,
                    null,
                    activeLine,
                    activeTrackOrTarget,
                    "tile-catalog-empty",
                    0,
                    0);
                return Array.Empty<OmsiRouteTracePoint>();
            }

            var tripPath = FindTripPath(
                map.DirectoryPath,
                activeTrackOrTarget,
                activeLine,
                activeDestination);

            var trackPath = FindTrackPath(
                map.DirectoryPath,
                activeTrackOrTarget);

            if (trackPath is null && tripPath is not null)
            {
                trackPath = FindTrackPath(
                    map.DirectoryPath,
                    ReadTripTrackName(tripPath));
            }

            if (trackPath is null)
            {
                var resolvedTrackName = ResolveTrackNameFromTrip(
                    map.DirectoryPath,
                    activeLine,
                    activeTrackOrTarget);
                trackPath = FindTrackPath(
                    map.DirectoryPath,
                    resolvedTrackName);
            }

            // OMSI often exposes Route as an IBIS/direction code (for example
            // "01") while the .ttp identifies the trip by its terminus. Do not
            // stop after the route-code lookup fails: line + destination is the
            // reliable second key for choosing the correct trip direction.
            if (trackPath is null &&
                !string.IsNullOrWhiteSpace(activeDestination) &&
                !string.Equals(
                    Normalize(activeDestination),
                    Normalize(activeTrackOrTarget ?? string.Empty),
                    StringComparison.Ordinal))
            {
                var resolvedFromDestination = ResolveTrackNameFromTrip(
                    map.DirectoryPath,
                    activeLine,
                    activeDestination);
                trackPath = FindTrackPath(
                    map.DirectoryPath,
                    resolvedFromDestination);
            }

            if (trackPath is null &&
                !string.IsNullOrWhiteSpace(activeDestination))
            {
                trackPath = FindTrackPath(
                    map.DirectoryPath,
                    activeDestination);
            }

            IReadOnlyList<OmsiRouteTrackEntry> entries;
            string sourceMode;
            string? sourcePath;

            if (trackPath is not null)
            {
                entries = ReadTrackEntries(
                    File.ReadAllLines(trackPath),
                    tileCatalog);
                sourceMode = "track";
                sourcePath = trackPath;
            }
            else if (tripPath is not null)
            {
                entries = ReadStationLinkEntries(
                    map.DirectoryPath,
                    tripPath,
                    tileCatalog);
                sourceMode = "station-links";
                sourcePath = tripPath;
            }
            else
            {
                WriteDiagnostics(
                    map,
                    null,
                    activeLine,
                    activeTrackOrTarget,
                    "route-source-not-found",
                    0,
                    0);
                return Array.Empty<OmsiRouteTracePoint>();
            }

            if (entries.Count == 0)
            {
                WriteDiagnostics(
                    map,
                    sourcePath,
                    activeLine,
                    activeTrackOrTarget,
                    sourceMode == "track"
                        ? "track-empty"
                        : "station-links-empty",
                    0,
                    0);
                return Array.Empty<OmsiRouteTracePoint>();
            }

            var splineTrace = OmsiRouteSplineGeometryReader.TryBuild(
                map,
                layout,
                entries);
            if (splineTrace.Count >= 2 &&
                IsDetailedTraceContinuous(splineTrace, tileSize))
            {
                WriteDiagnostics(
                    map,
                    sourcePath,
                    activeLine,
                    activeTrackOrTarget,
                    sourceMode == "track"
                        ? "detailed"
                        : "station-links-detailed",
                    entries.Count,
                    splineTrace.Count);
                return splineTrace;
            }

            WriteDiagnostics(
                map,
                sourcePath,
                activeLine,
                activeTrackOrTarget,
                splineTrace.Count >= 2
                    ? $"{sourceMode}-geometry-gap"
                    : $"{sourceMode}-geometry-unresolved",
                entries.Count,
                splineTrace.Count);
            return Array.Empty<OmsiRouteTracePoint>();
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
            if (!string.Equals(
                    lines[i].Trim(),
                    "[track_entry]",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Native OMSI 2 TTR layout:
            // object/spline id
            // path index inside the .sli/.sco
            // tile index (position of [map] in global.cfg)
            // internal per-tile path number/cache
            // path length
            // reserved (normally 0)
            //
            // Older/custom tools may write GridX/GridY instead of tile index +
            // internal path number. Always prefer the native tile-index form.
            if (i + 5 >= lines.Length ||
                !TryParseInteger(lines[i + 1], out var objectId) ||
                !TryParseInteger(lines[i + 2], out var pathId) ||
                !TryParseInteger(lines[i + 3], out var thirdValue) ||
                !TryParseInteger(lines[i + 4], out var fourthValue))
            {
                continue;
            }

            (int GridX, int GridY) grid;
            var directGridExists = tileCatalog.Values.Any(candidate =>
                candidate.GridX == thirdValue &&
                candidate.GridY == fourthValue);
            if (directGridExists)
            {
                // Native OMSI TTR stores GridX/GridY directly. Prefer this
                // interpretation even when GridX also happens to be a valid
                // ordered [map] index in large maps.
                grid = (thirdValue, fourthValue);
            }
            else if (tileCatalog.TryGetValue(
                         thirdValue,
                         out var indexedGrid))
            {
                grid = indexedGrid;
            }
            else
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
                double.IsFinite(pathLength) && pathLength > 0d
                    ? pathLength
                    : 0d));
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

            // Keep an ordered tile-id catalog as a compatibility fallback.
            // Native OMSI TTR entries carry GridX/GridY directly, but older or
            // custom timetable tooling can still reference ordered [map] slots.
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

    private static string? FindTripPath(
        string mapDirectory,
        string? activeRoute,
        string? activeLine,
        string? activeDestination)
    {
        var routeKey = Normalize(activeRoute ?? string.Empty);
        var lineKey = Normalize(activeLine ?? string.Empty);
        var destinationKey = Normalize(activeDestination ?? string.Empty);

        if (routeKey.Length > 0)
        {
            foreach (var directory in GetTimetableDirectories(mapDirectory))
            {
                foreach (var path in EnumerateTripFiles(directory))
                {
                    if (Normalize(Path.GetFileNameWithoutExtension(path)) == routeKey)
                    {
                        return path;
                    }
                }
            }
        }

        var lineOnlyMatches = new List<string>();
        foreach (var directory in GetTimetableDirectories(mapDirectory))
        {
            foreach (var path in EnumerateTripFiles(directory))
            {
                if (!TryReadTripHeader(
                        path,
                        out var trackName,
                        out var destination,
                        out var line))
                {
                    continue;
                }

                var fileKey = Normalize(Path.GetFileNameWithoutExtension(path));
                var trackKey = Normalize(trackName);
                var tripDestinationKey = Normalize(destination);
                var tripLineKey = Normalize(line);

                if (lineKey.Length > 0 && tripLineKey != lineKey)
                {
                    continue;
                }

                var routeMatches =
                    routeKey.Length > 0 &&
                    (fileKey == routeKey ||
                     trackKey == routeKey ||
                     tripDestinationKey == routeKey);
                var destinationMatches =
                    destinationKey.Length > 0 &&
                    (tripDestinationKey == destinationKey ||
                     fileKey == destinationKey);

                if (routeMatches || destinationMatches)
                {
                    return path;
                }

                if (lineKey.Length > 0)
                {
                    lineOnlyMatches.Add(path);
                }
            }
        }

        var distinctLineOnly = lineOnlyMatches
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
        return distinctLineOnly.Length == 1
            ? distinctLineOnly[0]
            : null;
    }

    private static IEnumerable<string> EnumerateTripFiles(string directory)
    {
        try
        {
            return Directory
                .EnumerateFiles(directory, "*.ttp", SearchOption.TopDirectoryOnly)
                .ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static bool TryReadTripHeader(
        string path,
        out string trackName,
        out string destination,
        out string line)
    {
        trackName = string.Empty;
        destination = string.Empty;
        line = string.Empty;

        var lines = File.ReadAllLines(path);
        for (var index = 0; index + 3 < lines.Length; index++)
        {
            if (!string.Equals(
                    lines[index].Trim(),
                    "[trip]",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            trackName = lines[index + 1].Trim();
            destination = lines[index + 2].Trim();
            line = lines[index + 3].Trim();
            return true;
        }

        return false;
    }

    private static string? ReadTripTrackName(string tripPath)
    {
        return TryReadTripHeader(
            tripPath,
            out var trackName,
            out _,
            out _)
            ? trackName
            : null;
    }

    private static IReadOnlyList<int> ReadTripStationIds(string tripPath)
    {
        var result = new List<int>();
        var lines = File.ReadAllLines(tripPath);

        for (var index = 0; index + 1 < lines.Length; index++)
        {
            if (!string.Equals(
                    lines[index].Trim(),
                    "[station_typ2]",
                    StringComparison.OrdinalIgnoreCase) ||
                !TryParseInteger(lines[index + 1], out var stationId))
            {
                continue;
            }

            if (result.Count == 0 || result[^1] != stationId)
            {
                result.Add(stationId);
            }
        }

        return result;
    }

    private static IReadOnlyList<OmsiRouteTrackEntry> ReadStationLinkEntries(
        string mapDirectory,
        string tripPath,
        IReadOnlyDictionary<int, (int GridX, int GridY)> tileCatalog)
    {
        var stationIds = ReadTripStationIds(tripPath);
        if (stationIds.Count < 2)
        {
            return Array.Empty<OmsiRouteTrackEntry>();
        }

        var directories = new List<string>();
        var baseDirectory = Path.Combine(mapDirectory, "TTData");
        if (Directory.Exists(baseDirectory))
        {
            directories.Add(baseDirectory);
        }

        var tripDirectory = Path.GetDirectoryName(tripPath);
        if (!string.IsNullOrWhiteSpace(tripDirectory) &&
            Directory.Exists(tripDirectory) &&
            !directories.Contains(
                tripDirectory,
                StringComparer.OrdinalIgnoreCase))
        {
            directories.Add(tripDirectory);
        }

        var links = new Dictionary<
            (int FromId, int ToId),
            List<OmsiRouteTrackEntry>>();

        foreach (var directory in directories)
        {
            var path = Path.Combine(directory, "StnLinks.cfg");
            if (!File.Exists(path))
            {
                continue;
            }

            ReadStationLinksFile(path, tileCatalog, links);
        }

        var result = new List<OmsiRouteTrackEntry>();
        for (var index = 1; index < stationIds.Count; index++)
        {
            var key = (
                FromId: stationIds[index - 1],
                ToId: stationIds[index]);
            if (!links.TryGetValue(key, out var linkEntries))
            {
                continue;
            }

            foreach (var entry in linkEntries)
            {
                if (result.Count > 0)
                {
                    var previous = result[^1];
                    if (previous.ObjectId == entry.ObjectId &&
                        previous.PathId == entry.PathId &&
                        previous.GridX == entry.GridX &&
                        previous.GridY == entry.GridY)
                    {
                        continue;
                    }
                }

                result.Add(entry);
            }
        }

        return result;
    }

    private static void ReadStationLinksFile(
        string path,
        IReadOnlyDictionary<int, (int GridX, int GridY)> tileCatalog,
        IDictionary<(int FromId, int ToId), List<OmsiRouteTrackEntry>> links)
    {
        var lines = File.ReadAllLines(path);
        (int FromId, int ToId)? currentLink = null;

        for (var index = 0; index < lines.Length; index++)
        {
            var token = lines[index].Trim();
            if (string.Equals(
                    token,
                    "[StnLink]",
                    StringComparison.OrdinalIgnoreCase))
            {
                currentLink = null;
                if (index + 3 >= lines.Length ||
                    !TryParseInteger(lines[index + 2], out var fromId) ||
                    !TryParseInteger(lines[index + 3], out var toId))
                {
                    continue;
                }

                currentLink = (fromId, toId);
                // Chrono TTData replaces a station link with the same endpoints.
                links[currentLink.Value] = new List<OmsiRouteTrackEntry>();
                continue;
            }

            if (!string.Equals(
                    token,
                    "[StnLink_entry]",
                    StringComparison.OrdinalIgnoreCase) ||
                currentLink is null ||
                index + 4 >= lines.Length ||
                !TryParseInteger(lines[index + 1], out var objectId) ||
                !TryParseInteger(lines[index + 2], out var pathId) ||
                !TryParseInteger(lines[index + 3], out var tileIndex) ||
                !tileCatalog.TryGetValue(tileIndex, out var grid))
            {
                continue;
            }

            var pathLength = 0d;
            double.TryParse(
                lines[index + 4].Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out pathLength);

            links[currentLink.Value].Add(
                new OmsiRouteTrackEntry(
                    objectId,
                    pathId,
                    grid.GridX,
                    grid.GridY,
                    double.IsFinite(pathLength) && pathLength > 0d
                        ? pathLength
                        : 0d));
        }
    }

    private static bool TryParseInteger(string value, out int result)
    {
        var trimmed = value.Trim();
        if (int.TryParse(
                trimmed,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result))
        {
            return true;
        }

        if (double.TryParse(
                trimmed,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var numeric) &&
            double.IsFinite(numeric) &&
            numeric >= int.MinValue &&
            numeric <= int.MaxValue)
        {
            var rounded = Math.Round(numeric);
            if (Math.Abs(numeric - rounded) <= 0.001d)
            {
                result = (int)rounded;
                return true;
            }
        }

        result = 0;
        return false;
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
        var targetOnlyMatches = new List<string>();

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

                        if (targetMatches)
                        {
                            targetOnlyMatches.Add(trackName);
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

        var distinctTargetOnly = targetOnlyMatches
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
        if (distinctTargetOnly.Length == 1)
        {
            return distinctTargetOnly[0];
        }

        var distinctFallback = fallbackMatches
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
        return distinctFallback.Length == 1
            ? distinctFallback[0]
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
