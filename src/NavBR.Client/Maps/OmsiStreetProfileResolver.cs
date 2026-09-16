using System.IO;
using System.Text.Json;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Maps;

/// <summary>
/// Resolves a friendly street name from an optional NavBR street profile.
/// OMSI's spline geometry does not provide a reliable, standard friendly street
/// name, so this matcher keeps naming data separate from the simulator assets.
/// </summary>
internal static class OmsiStreetProfileResolver
{
    private const double DefaultMaxDistanceMeters = 35d;
    private const int MaxStreets = 20_000;
    private const int MaxPointsPerStreet = 2_048;

    private static readonly object Sync = new();
    private static readonly Dictionary<string, CachedProfile> Cache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string? Resolve(OmsiMapInfo? map, VehicleTelemetry telemetry)
    {
        if (map is null ||
            telemetry.GridX is not int gridX ||
            telemetry.GridY is not int gridY ||
            telemetry.TileX is not double tileX ||
            telemetry.TileY is not double tileY ||
            !double.IsFinite(tileX) ||
            !double.IsFinite(tileY))
        {
            return null;
        }

        var profile = GetProfile(map);
        if (profile is null || profile.Streets.Count == 0)
        {
            return null;
        }

        var layout = OmsiMapLayoutReader.TryRead(map.GlobalConfigPath);
        if (layout?.TileSize is not double tileSize || !double.IsFinite(tileSize) || tileSize <= 0d)
        {
            return null;
        }

        var x = gridX * tileSize + tileX;
        var y = gridY * tileSize + tileY;
        var maxDistance = profile.MaxDistanceMeters is double configured &&
                          double.IsFinite(configured) &&
                          configured is >= 2d and <= 200d
            ? configured
            : DefaultMaxDistanceMeters;
        var bestDistanceSquared = maxDistance * maxDistance;
        string? bestName = null;

        foreach (var street in profile.Streets)
        {
            if (string.IsNullOrWhiteSpace(street.Name) || street.Points.Count < 2)
            {
                continue;
            }

            var previous = ToWorld(street.Points[0], tileSize);
            for (var index = 1; index < street.Points.Count; index++)
            {
                var current = ToWorld(street.Points[index], tileSize);
                var distanceSquared = DistanceToSegmentSquared(
                    x,
                    y,
                    previous.X,
                    previous.Y,
                    current.X,
                    current.Y);

                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestName = street.Name.Trim();
                }

                previous = current;
            }
        }

        return bestName;
    }

    private static StreetProfile? GetProfile(OmsiMapInfo map)
    {
        lock (Sync)
        {
            if (Cache.TryGetValue(map.DirectoryPath, out var cached))
            {
                return cached.Profile;
            }

            var path = ResolveProfilePath(map);
            var profile = path is null ? null : TryLoad(path);
            Cache[map.DirectoryPath] = new CachedProfile(path, profile);
            return profile;
        }
    }

    private static string? ResolveProfilePath(OmsiMapInfo map)
    {
        var localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OMSI NavBR Multiplayer",
            "StreetProfiles");
        var localProfile = Path.Combine(localRoot, $"{SanitizeFileName(map.FolderName)}.json");
        if (File.Exists(localProfile))
        {
            return localProfile;
        }

        var mapProfile = Path.Combine(map.DirectoryPath, "NavBR.streets.json");
        return File.Exists(mapProfile) ? mapProfile : null;
    }

    private static StreetProfile? TryLoad(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            if (json.Length > 8_000_000)
            {
                return null;
            }

            var document = JsonSerializer.Deserialize<StreetProfileDocument>(json, JsonOptions);
            if (document is null || document.Version != 1 || document.Streets is null)
            {
                return null;
            }

            var streets = document.Streets
                .Take(MaxStreets)
                .Where(static street =>
                    !string.IsNullOrWhiteSpace(street.Name) &&
                    street.Points is { Count: >= 2 })
                .Select(static street => new StreetEntry(
                    street.Name!.Trim(),
                    street.Points!
                        .Take(MaxPointsPerStreet)
                        .Where(static point =>
                            double.IsFinite(point.TileX) &&
                            double.IsFinite(point.TileY))
                        .ToArray()))
                .Where(static street => street.Points.Count >= 2)
                .ToArray();

            return new StreetProfile(document.MaxDistanceMeters, streets);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (double X, double Y) ToWorld(StreetPoint point, double tileSize) =>
        (point.GridX * tileSize + point.TileX, point.GridY * tileSize + point.TileY);

    private static double DistanceToSegmentSquared(
        double px,
        double py,
        double ax,
        double ay,
        double bx,
        double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 0.000001d)
        {
            var pointDx = px - ax;
            var pointDy = py - ay;
            return pointDx * pointDx + pointDy * pointDy;
        }

        var t = ((px - ax) * dx + (py - ay) * dy) / lengthSquared;
        t = Math.Clamp(t, 0d, 1d);
        var closestX = ax + t * dx;
        var closestY = ay + t * dy;
        var distanceX = px - closestX;
        var distanceY = py - closestY;
        return distanceX * distanceX + distanceY * distanceY;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var characters = value
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray();
        return new string(characters).Trim();
    }

    private sealed record CachedProfile(string? Path, StreetProfile? Profile);
    private sealed record StreetProfile(double? MaxDistanceMeters, IReadOnlyList<StreetEntry> Streets);
    private sealed record StreetEntry(string Name, IReadOnlyList<StreetPoint> Points);

    private sealed class StreetProfileDocument
    {
        public int Version { get; set; }
        public double? MaxDistanceMeters { get; set; }
        public List<StreetDocument>? Streets { get; set; }
    }

    private sealed class StreetDocument
    {
        public string? Name { get; set; }
        public List<StreetPoint>? Points { get; set; }
    }

    private sealed class StreetPoint
    {
        public int GridX { get; set; }
        public int GridY { get; set; }
        public double TileX { get; set; }
        public double TileY { get; set; }
    }
}
