using System.Globalization;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Maps;

internal sealed record OmsiRouteRejoinPoint(double X, double Y);

internal sealed record OmsiRouteRejoinPath(
    IReadOnlyList<OmsiRouteRejoinPoint> Points,
    double DistanceMeters,
    OmsiRouteRejoinPoint RejoinPoint);

/// <summary>
/// Builds a short, fail-closed road path from the current bus position back to
/// the established OMSI route. The graph is derived only from real [spline] /
/// [spline_h] placements in nearby .map tiles.
/// </summary>
internal sealed class OmsiRouteRejoinPathfinder
{
    private const double SampleSpacingMeters = 12d;
    private const int MaxSamplesPerSpline = 128;
    private const double EndpointJoinMeters = 8d;
    private const double EndpointJoinVerticalMeters = 3d;
    private const double MaxSnapToRoadMeters = 65d;
    private const double MaxRejoinSearchMeters = 2200d;
    private const double RejoinAheadMeters = 90d;
    private const int MaxTileSearchMargin = 3;
    private const int MaxTilesPerSearch = 100;
    private const int MaxOutputPoints = 260;

    private string? _mapKey;
    private string? _lastSearchKey;
    private OmsiRouteRejoinPath? _lastResult;
    private Dictionary<(int X, int Y), string> _tileCatalog = new();
    private readonly Dictionary<string, CachedTile> _tileCache =
        new(StringComparer.OrdinalIgnoreCase);

    public OmsiRouteRejoinPath? TryFind(
        OmsiMapInfo map,
        OmsiMapLayout layout,
        VehicleTelemetry telemetry,
        IReadOnlyList<OmsiRouteTracePoint> routeTrace)
    {
        if (layout.TileSize is not double tileSize ||
            tileSize <= 0d ||
            routeTrace.Count < 2 ||
            telemetry.GridX is not int vehicleGridX ||
            telemetry.GridY is not int vehicleGridY ||
            telemetry.TileX is not double vehicleTileX ||
            telemetry.TileY is not double vehicleTileY)
        {
            return null;
        }

        EnsureMap(map);
        if (_tileCatalog.Count == 0)
        {
            return null;
        }

        var vehicle = new RoadPoint3(
            vehicleGridX * tileSize + vehicleTileX,
            vehicleGridY * tileSize + vehicleTileY,
            0d);

        // Rejoin slightly ahead of the closest route position instead of
        // steering back to a point the bus has effectively already passed.
        // The .ttr trace is ordered, so this keeps the recovery path aligned
        // with the active route direction without inventing road geometry.
        var targetTrace = FindRejoinTarget(
            vehicle,
            routeTrace,
            tileSize);
        if (targetTrace is null)
        {
            return null;
        }

        var target = new RoadPoint3(
            targetTrace.GridX * tileSize + targetTrace.TileX,
            targetTrace.GridY * tileSize + targetTrace.TileY,
            0d);
        var directDistance = Distance2D(vehicle, target);
        if (!double.IsFinite(directDistance) ||
            directDistance < 1d ||
            directDistance > MaxRejoinSearchMeters)
        {
            return null;
        }

        var searchKey = string.Create(
            CultureInfo.InvariantCulture,
            $"{_mapKey}|{Math.Round(vehicle.X / 15d)}|{Math.Round(vehicle.Y / 15d)}|" +
            $"{targetTrace.GridX}|{targetTrace.GridY}|{Math.Round(targetTrace.TileX / 15d)}|{Math.Round(targetTrace.TileY / 15d)}");
        if (string.Equals(searchKey, _lastSearchKey, StringComparison.Ordinal))
        {
            return _lastResult;
        }

        _lastSearchKey = searchKey;
        _lastResult = null;

        // A valid road return can legitimately leave the immediately adjacent
        // tile rectangle (one-way layouts, terminals, bridges and large urban
        // blocks). Expand the real .map search progressively instead of failing
        // after the first narrow graph.
        for (var margin = 1; margin <= MaxTileSearchMargin; margin++)
        {
            var minGridX = Math.Min(vehicleGridX, targetTrace.GridX) - margin;
            var maxGridX = Math.Max(vehicleGridX, targetTrace.GridX) + margin;
            var minGridY = Math.Min(vehicleGridY, targetTrace.GridY) - margin;
            var maxGridY = Math.Max(vehicleGridY, targetTrace.GridY) + margin;

            var selectedTiles = _tileCatalog
                .Where(pair =>
                    pair.Key.X >= minGridX &&
                    pair.Key.X <= maxGridX &&
                    pair.Key.Y >= minGridY &&
                    pair.Key.Y <= maxGridY)
                .Take(MaxTilesPerSearch + 1)
                .ToArray();
            if (selectedTiles.Length == 0)
            {
                continue;
            }

            // Do not turn recovery into a full-map path search. If an expanded
            // window exceeds the guard, a smaller window has already been
            // attempted and failed closed.
            if (selectedTiles.Length > MaxTilesPerSearch)
            {
                break;
            }

            var segments = new List<RoadSegment>();
            foreach (var tile in selectedTiles)
            {
                foreach (var segment in ReadTile(
                             tile.Key.X,
                             tile.Key.Y,
                             tile.Value,
                             tileSize))
                {
                    segments.Add(segment);
                }
            }

            if (segments.Count == 0)
            {
                continue;
            }

            var graph = BuildGraph(segments);
            if (graph.Nodes.Count < 2)
            {
                continue;
            }

            var startNode = FindNearestNode(graph.Nodes, vehicle);
            var targetNode = FindNearestNode(graph.Nodes, target);
            if (startNode is null ||
                targetNode is null ||
                Distance2D(startNode.Point, vehicle) > MaxSnapToRoadMeters ||
                Distance2D(targetNode.Point, target) > MaxSnapToRoadMeters)
            {
                continue;
            }

            var nodePath = FindShortestPath(graph, startNode.Id, targetNode.Id);
            if (nodePath.Count == 0)
            {
                continue;
            }

            var output = new List<OmsiRouteRejoinPoint>(nodePath.Count + 2)
            {
                new(vehicle.X, vehicle.Y)
            };
            foreach (var nodeId in nodePath)
            {
                var point = graph.Nodes[nodeId].Point;
                AppendIfDistinct(
                    output,
                    new OmsiRouteRejoinPoint(point.X, point.Y));
            }
            AppendIfDistinct(
                output,
                new OmsiRouteRejoinPoint(target.X, target.Y));

            output = Decimate(output, MaxOutputPoints);
            if (output.Count < 2)
            {
                continue;
            }

            var distance = 0d;
            for (var index = 1; index < output.Count; index++)
            {
                var dx = output[index].X - output[index - 1].X;
                var dy = output[index].Y - output[index - 1].Y;
                distance += Math.Sqrt(dx * dx + dy * dy);
            }

            _lastResult = new OmsiRouteRejoinPath(
                output,
                distance,
                new OmsiRouteRejoinPoint(target.X, target.Y));
            return _lastResult;
        }

        return null;
    }

    private void EnsureMap(OmsiMapInfo map)
    {
        var key = $"{map.DirectoryPath}|{map.GlobalConfigPath}|{map.CompatibilityId}";
        if (string.Equals(key, _mapKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _mapKey = key;
        _lastSearchKey = null;
        _lastResult = null;
        _tileCache.Clear();
        _tileCatalog = ReadTileCatalog(
            map.DirectoryPath,
            map.GlobalConfigPath);
    }

    private IReadOnlyList<RoadSegment> ReadTile(
        int gridX,
        int gridY,
        string tilePath,
        double tileSize)
    {
        DateTime lastWriteUtc;
        try
        {
            lastWriteUtc = File.GetLastWriteTimeUtc(tilePath);
        }
        catch
        {
            return Array.Empty<RoadSegment>();
        }

        if (_tileCache.TryGetValue(tilePath, out var cached) &&
            cached.LastWriteUtc == lastWriteUtc)
        {
            return cached.Segments;
        }

        var segments = ParseTile(
            gridX,
            gridY,
            tilePath,
            tileSize);
        _tileCache[tilePath] = new CachedTile(lastWriteUtc, segments);
        return segments;
    }

    private static IReadOnlyList<RoadSegment> ParseTile(
        int gridX,
        int gridY,
        string tilePath,
        double tileSize)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(tilePath);
        }
        catch
        {
            return Array.Empty<RoadSegment>();
        }

        var result = new List<RoadSegment>();
        for (var index = 0; index < lines.Length - 11; index++)
        {
            var token = lines[index].Trim();
            if (!string.Equals(token, "[spline]", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(token, "[spline_h]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryParseDouble(lines[index + 6], out var x) ||
                !TryParseDouble(lines[index + 7], out var z) ||
                !TryParseDouble(lines[index + 8], out var y) ||
                !TryParseDouble(lines[index + 9], out var rotation) ||
                !TryParseDouble(lines[index + 10], out var length) ||
                !TryParseDouble(lines[index + 11], out var radius) ||
                length <= 0d ||
                length > 10_000d)
            {
                continue;
            }

            var sampleCount = Math.Clamp(
                (int)Math.Ceiling(length / SampleSpacingMeters),
                2,
                MaxSamplesPerSpline);
            var points = new List<RoadPoint3>(sampleCount + 1);
            var radians = rotation * Math.PI / 180d;
            var sin = Math.Sin(radians);
            var cos = Math.Cos(radians);

            for (var sample = 0; sample <= sampleCount; sample++)
            {
                var distance = length * sample / sampleCount;
                double localX;
                double localY;
                if (Math.Abs(radius) > 0.001d)
                {
                    var angle = distance / radius;
                    localX = radius - radius * Math.Cos(angle);
                    localY = radius * Math.Sin(angle);
                }
                else
                {
                    localX = 0d;
                    localY = distance;
                }

                var tileX = x + localX * cos + localY * sin;
                var tileY = y - localX * sin + localY * cos;
                points.Add(new RoadPoint3(
                    gridX * tileSize + tileX,
                    gridY * tileSize + tileY,
                    z));
            }

            if (points.Count >= 2)
            {
                result.Add(new RoadSegment(points));
            }
        }

        var mapDirectory = Path.GetDirectoryName(tilePath);
        if (!string.IsNullOrWhiteSpace(mapDirectory))
        {
            foreach (var sceneryPath in OmsiRouteSceneryPathGeometryReader.ReadAllRoadPaths(
                         mapDirectory,
                         tilePath))
            {
                var points = sceneryPath
                    .Select(point => new RoadPoint3(
                        gridX * tileSize + point.TileX,
                        gridY * tileSize + point.TileY,
                        point.Z))
                    .ToArray();
                if (points.Length >= 2)
                {
                    result.Add(new RoadSegment(points));
                }
            }
        }

        return result;
    }

    private static RoadGraph BuildGraph(IReadOnlyList<RoadSegment> segments)
    {
        var nodes = new List<RoadNode>();
        var edges = new Dictionary<int, List<RoadEdge>>();
        var endpoints = new List<int>();

        foreach (var segment in segments)
        {
            var segmentNodeIds = new int[segment.Points.Count];
            for (var index = 0; index < segment.Points.Count; index++)
            {
                var id = nodes.Count;
                nodes.Add(new RoadNode(id, segment.Points[index]));
                edges[id] = new List<RoadEdge>();
                segmentNodeIds[index] = id;
            }

            endpoints.Add(segmentNodeIds[0]);
            endpoints.Add(segmentNodeIds[^1]);

            for (var index = 1; index < segmentNodeIds.Length; index++)
            {
                var left = segmentNodeIds[index - 1];
                var right = segmentNodeIds[index];
                var weight = Distance2D(nodes[left].Point, nodes[right].Point);
                AddUndirectedEdge(edges, left, right, weight);
            }
        }

        var cellSize = EndpointJoinMeters;
        var buckets = new Dictionary<(int X, int Y), List<int>>();
        foreach (var endpointId in endpoints)
        {
            var point = nodes[endpointId].Point;
            var key = (
                (int)Math.Floor(point.X / cellSize),
                (int)Math.Floor(point.Y / cellSize));
            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = new List<int>();
                buckets[key] = bucket;
            }
            bucket.Add(endpointId);
        }

        foreach (var endpointId in endpoints)
        {
            var point = nodes[endpointId].Point;
            var keyX = (int)Math.Floor(point.X / cellSize);
            var keyY = (int)Math.Floor(point.Y / cellSize);
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (!buckets.TryGetValue((keyX + dx, keyY + dy), out var candidates))
                    {
                        continue;
                    }

                    foreach (var candidateId in candidates)
                    {
                        if (candidateId <= endpointId)
                        {
                            continue;
                        }

                        var candidate = nodes[candidateId].Point;
                        if (Math.Abs(candidate.Z - point.Z) > EndpointJoinVerticalMeters)
                        {
                            continue;
                        }

                        var distance = Distance2D(point, candidate);
                        if (distance <= EndpointJoinMeters)
                        {
                            AddUndirectedEdge(edges, endpointId, candidateId, distance);
                        }
                    }
                }
            }
        }

        return new RoadGraph(nodes, edges);
    }

    private static IReadOnlyList<int> FindShortestPath(
        RoadGraph graph,
        int startId,
        int goalId)
    {
        if (startId == goalId)
        {
            return [startId];
        }

        var distance = new double[graph.Nodes.Count];
        Array.Fill(distance, double.PositiveInfinity);
        var previous = new int[graph.Nodes.Count];
        Array.Fill(previous, -1);
        var open = new PriorityQueue<int, double>();
        distance[startId] = 0d;
        open.Enqueue(startId, 0d);

        while (open.TryDequeue(out var current, out var queuedPriority))
        {
            var currentDistance = distance[current];
            var heuristic = Distance2D(
                graph.Nodes[current].Point,
                graph.Nodes[goalId].Point);
            if (queuedPriority > currentDistance + heuristic + 0.001d)
            {
                continue;
            }

            if (current == goalId)
            {
                break;
            }

            foreach (var edge in graph.Edges[current])
            {
                var candidate = currentDistance + edge.Weight;
                if (candidate >= distance[edge.To])
                {
                    continue;
                }

                distance[edge.To] = candidate;
                previous[edge.To] = current;
                var priority = candidate + Distance2D(
                    graph.Nodes[edge.To].Point,
                    graph.Nodes[goalId].Point);
                open.Enqueue(edge.To, priority);
            }
        }

        if (!double.IsFinite(distance[goalId]))
        {
            return Array.Empty<int>();
        }

        var path = new List<int>();
        for (var current = goalId; current >= 0; current = previous[current])
        {
            path.Add(current);
            if (current == startId)
            {
                break;
            }
        }

        if (path.Count == 0 || path[^1] != startId)
        {
            return Array.Empty<int>();
        }

        path.Reverse();
        return path;
    }

    private static RoadNode? FindNearestNode(
        IReadOnlyList<RoadNode> nodes,
        RoadPoint3 point)
    {
        RoadNode? best = null;
        var bestDistanceSquared = double.PositiveInfinity;
        foreach (var node in nodes)
        {
            var dx = node.Point.X - point.X;
            var dy = node.Point.Y - point.Y;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                best = node;
            }
        }

        return best;
    }

    private static OmsiRouteTracePoint? FindRejoinTarget(
        RoadPoint3 vehicle,
        IReadOnlyList<OmsiRouteTracePoint> routeTrace,
        double tileSize)
    {
        if (routeTrace.Count == 0)
        {
            return null;
        }

        var nearestIndex = -1;
        var bestDistanceSquared = double.PositiveInfinity;
        for (var index = 0; index < routeTrace.Count; index++)
        {
            var point = routeTrace[index];
            var worldX = point.GridX * tileSize + point.TileX;
            var worldY = point.GridY * tileSize + point.TileY;
            var dx = worldX - vehicle.X;
            var dy = worldY - vehicle.Y;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                nearestIndex = index;
            }
        }

        if (nearestIndex < 0)
        {
            return null;
        }

        var travelled = 0d;
        var previous = routeTrace[nearestIndex];
        for (var index = nearestIndex + 1; index < routeTrace.Count; index++)
        {
            var current = routeTrace[index];
            var previousWorld = new RoadPoint3(
                previous.GridX * tileSize + previous.TileX,
                previous.GridY * tileSize + previous.TileY,
                0d);
            var currentWorld = new RoadPoint3(
                current.GridX * tileSize + current.TileX,
                current.GridY * tileSize + current.TileY,
                0d);
            travelled += Distance2D(previousWorld, currentWorld);
            if (travelled >= RejoinAheadMeters)
            {
                return current;
            }

            previous = current;
        }

        // Near the end of the trip there may be less than the normal look-ahead
        // remaining. Rejoin the last trustworthy .ttr point rather than
        // suppressing recovery altogether.
        return routeTrace[^1];
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

            try
            {
                var path = Path.IsPathRooted(relative)
                    ? relative
                    : Path.GetFullPath(Path.Combine(mapDirectory, relative));
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

    private static List<OmsiRouteRejoinPoint> Decimate(
        List<OmsiRouteRejoinPoint> points,
        int maxPoints)
    {
        if (points.Count <= maxPoints)
        {
            return points;
        }

        var result = new List<OmsiRouteRejoinPoint>(maxPoints);
        var step = (points.Count - 1d) / (maxPoints - 1d);
        for (var index = 0; index < maxPoints; index++)
        {
            var sourceIndex = (int)Math.Round(index * step);
            result.Add(points[Math.Min(sourceIndex, points.Count - 1)]);
        }
        return result;
    }

    private static void AppendIfDistinct(
        List<OmsiRouteRejoinPoint> points,
        OmsiRouteRejoinPoint point)
    {
        if (points.Count == 0)
        {
            points.Add(point);
            return;
        }

        var previous = points[^1];
        var dx = point.X - previous.X;
        var dy = point.Y - previous.Y;
        if (dx * dx + dy * dy > 0.25d)
        {
            points.Add(point);
        }
    }

    private static void AddUndirectedEdge(
        Dictionary<int, List<RoadEdge>> edges,
        int left,
        int right,
        double weight)
    {
        if (!double.IsFinite(weight) || weight <= 0d)
        {
            return;
        }

        edges[left].Add(new RoadEdge(right, weight));
        edges[right].Add(new RoadEdge(left, weight));
    }

    private static double Distance2D(RoadPoint3 left, RoadPoint3 right)
    {
        var dx = right.X - left.X;
        var dy = right.Y - left.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool TryParseDouble(string value, out double result) =>
        double.TryParse(
            value.Trim().Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result) &&
        double.IsFinite(result);

    private sealed record CachedTile(
        DateTime LastWriteUtc,
        IReadOnlyList<RoadSegment> Segments);

    private sealed record RoadSegment(
        IReadOnlyList<RoadPoint3> Points);

    private sealed record RoadGraph(
        IReadOnlyList<RoadNode> Nodes,
        IReadOnlyDictionary<int, List<RoadEdge>> Edges);

    private sealed record RoadNode(
        int Id,
        RoadPoint3 Point);

    private readonly record struct RoadEdge(
        int To,
        double Weight);

    private readonly record struct RoadPoint3(
        double X,
        double Y,
        double Z);
}
