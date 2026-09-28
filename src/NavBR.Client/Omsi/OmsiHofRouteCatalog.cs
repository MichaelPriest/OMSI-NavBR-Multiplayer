using System.Text.RegularExpressions;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Omsi;

internal sealed record NavBrTpTsHofRoute(
    string Line,
    string Route,
    string Description,
    string? DestinationCode,
    string HofFile)
{
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Description)
            ? $"{Line} · rota {Route}"
            : $"{Line} · rota {Route} · {Description}";
}

internal static class OmsiHofRouteCatalog
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, CacheEntry> Cache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ResolutionCacheEntry> ResolutionCache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan ResolutionCacheLifetime =
        TimeSpan.FromSeconds(15);

    private sealed record CacheEntry(
        DateTime LastWriteUtc,
        IReadOnlyList<NavBrTpTsHofRoute> Routes);

    private sealed record ResolutionCacheEntry(
        DateTimeOffset ExpiresAtUtc,
        IReadOnlyList<NavBrTpTsHofRoute> Routes);

    public static IReadOnlyList<NavBrTpTsHofRoute> Resolve(
        VehicleTelemetry? telemetry,
        string? preferredOmsiRoot = null)
    {
        if (telemetry is null ||
            string.IsNullOrWhiteSpace(telemetry.VehiclePath))
        {
            return Array.Empty<NavBrTpTsHofRoute>();
        }

        var root = ResolveOmsiRoot(preferredOmsiRoot);
        if (string.IsNullOrWhiteSpace(root))
        {
            return Array.Empty<NavBrTpTsHofRoute>();
        }

        try
        {
            var normalizedRoot = Path.GetFullPath(root);
            var vehicleRelative = telemetry.VehiclePath!
                .Trim()
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            if (!vehicleRelative.StartsWith(
                    $"Vehicles{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase) ||
                vehicleRelative.Contains("..", StringComparison.Ordinal))
            {
                return Array.Empty<NavBrTpTsHofRoute>();
            }

            var resolutionKey =
                $"{normalizedRoot}|{vehicleRelative}|{telemetry.HofName}|{telemetry.MapName}";
            lock (Sync)
            {
                if (ResolutionCache.TryGetValue(resolutionKey, out var resolved) &&
                    resolved.ExpiresAtUtc > DateTimeOffset.UtcNow)
                {
                    return resolved.Routes;
                }
            }

            var vehiclePath = Path.GetFullPath(
                Path.Combine(normalizedRoot, vehicleRelative));
            var vehicleDirectory = Path.GetDirectoryName(vehiclePath);
            if (string.IsNullOrWhiteSpace(vehicleDirectory) ||
                !Directory.Exists(vehicleDirectory))
            {
                return Array.Empty<NavBrTpTsHofRoute>();
            }

            var hofFiles = Directory
                .EnumerateFiles(
                    vehicleDirectory,
                    "*.hof",
                    SearchOption.TopDirectoryOnly)
                .OrderByDescending(path =>
                    HofMatches(
                        path,
                        telemetry.HofName))
                .ThenByDescending(path =>
                    HofMatches(
                        path,
                        telemetry.MapName))
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var hofFile in hofFiles)
            {
                var routes = ReadCached(hofFile);
                if (routes.Count > 0)
                {
                    lock (Sync)
                    {
                        ResolutionCache[resolutionKey] =
                            new ResolutionCacheEntry(
                                DateTimeOffset.UtcNow + ResolutionCacheLifetime,
                                routes);
                    }

                    return routes;
                }
            }

            lock (Sync)
            {
                ResolutionCache[resolutionKey] =
                    new ResolutionCacheEntry(
                        DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5),
                        Array.Empty<NavBrTpTsHofRoute>());
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (ArgumentException)
        {
        }

        return Array.Empty<NavBrTpTsHofRoute>();
    }

    private static IReadOnlyList<NavBrTpTsHofRoute> ReadCached(
        string hofFile)
    {
        var writeUtc = File.GetLastWriteTimeUtc(hofFile);
        lock (Sync)
        {
            if (Cache.TryGetValue(hofFile, out var cached) &&
                cached.LastWriteUtc == writeUtc)
            {
                return cached.Routes;
            }
        }

        var routes = Parse(hofFile);
        lock (Sync)
        {
            Cache[hofFile] = new CacheEntry(writeUtc, routes);
        }

        return routes;
    }

    private static IReadOnlyList<NavBrTpTsHofRoute> Parse(
        string hofFile)
    {
        try
        {
            var lines = File.ReadAllLines(
                hofFile,
                System.Text.Encoding.Latin1);
            var routes = new List<NavBrTpTsHofRoute>();

            for (var index = 0; index < lines.Length; index++)
            {
                if (!string.Equals(
                        lines[index].Trim(),
                        "[infosystem_trip]",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var values = new List<string>(4);
                for (var cursor = index + 1;
                     cursor < lines.Length && values.Count < 4;
                     cursor++)
                {
                    var value = lines[cursor].Trim();
                    if (value.Length == 0 ||
                        value.StartsWith(";", StringComparison.Ordinal) ||
                        value.StartsWith("//", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (value.StartsWith("[", StringComparison.Ordinal))
                    {
                        break;
                    }

                    values.Add(value);
                }

                if (values.Count < 4)
                {
                    continue;
                }

                var tripCode = Regex.Replace(
                    values[0],
                    @"\s+",
                    string.Empty);
                if (tripCode.Length < 3 ||
                    !tripCode.All(char.IsLetterOrDigit))
                {
                    continue;
                }

                var routeCode = tripCode[^2..];
                var line = values[3].Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    line = tripCode[..^2];
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                routes.Add(
                    new NavBrTpTsHofRoute(
                        line,
                        routeCode,
                        values[1].Trim(),
                        string.IsNullOrWhiteSpace(values[2])
                            ? null
                            : values[2].Trim(),
                        Path.GetFileName(hofFile)));

                if (routes.Count >= 256)
                {
                    break;
                }
            }

            return routes
                .DistinctBy(route => $"{route.Line}|{route.Route}")
                .OrderBy(route => route.Line, StringComparer.OrdinalIgnoreCase)
                .ThenBy(route => route.Route, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<NavBrTpTsHofRoute>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<NavBrTpTsHofRoute>();
        }
    }

    private static bool HofMatches(
        string hofFile,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(hofFile);
        return string.Equals(
                   fileName,
                   Path.GetFileNameWithoutExtension(value.Trim()),
                   StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains(
                   value.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveOmsiRoot(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            var resolved =
                OmsiInstallationLocator.TryResolveInstallDirectory(preferred);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return OmsiInstallationLocator
            .Discover()
            .FirstOrDefault()
            ?.InstallDirectory;
    }
}
