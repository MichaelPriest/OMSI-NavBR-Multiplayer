namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiContentContext(
    string? ContentRoot,
    string? MapDirectory,
    string? TimetableDirectory)
{
    public bool RootAvailable => !string.IsNullOrWhiteSpace(ContentRoot);
    public bool MapAvailable => !string.IsNullOrWhiteSpace(MapDirectory);
    public bool TimetableAvailable => !string.IsNullOrWhiteSpace(TimetableDirectory);
}

internal static class OpenOmsiContentLocator
{
    private const string RootEnvironmentVariable = "NAVBR_OPENOMSI_CONTENT_ROOT";

    public static OpenOmsiContentContext Resolve(
        string? mapName,
        string? mapPath = null)
    {
        var direct = ResolveFromMapPath(mapPath);
        if (direct is not null)
        {
            return direct;
        }

        foreach (var root in EnumerateRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var mapDirectory = ResolveMapDirectory(root, mapName);
            var timetableDirectory = mapDirectory is null
                ? null
                : ResolveChildDirectory(mapDirectory, "TTData");

            return new(
                ContentRoot: root,
                MapDirectory: mapDirectory,
                TimetableDirectory: timetableDirectory);
        }

        return new(null, null, null);
    }

    private static OpenOmsiContentContext? ResolveFromMapPath(string? mapPath)
    {
        if (string.IsNullOrWhiteSpace(mapPath))
        {
            return null;
        }

        foreach (var candidate in ResolveMapPathCandidates(mapPath))
        {
            try
            {
                if (!File.Exists(candidate) ||
                    !string.Equals(
                        Path.GetFileName(candidate),
                        "global.cfg",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var mapDirectory = Path.GetDirectoryName(candidate);
                var mapsDirectory = string.IsNullOrWhiteSpace(mapDirectory)
                    ? null
                    : Directory.GetParent(mapDirectory)?.FullName;
                var root = string.IsNullOrWhiteSpace(mapsDirectory)
                    ? null
                    : Directory.GetParent(mapsDirectory)?.FullName;
                if (string.IsNullOrWhiteSpace(root) ||
                    !string.Equals(
                        Path.GetFileName(mapsDirectory),
                        "maps",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return new(
                    ContentRoot: root,
                    MapDirectory: mapDirectory,
                    TimetableDirectory: ResolveChildDirectory(mapDirectory!, "TTData"));
            }
            catch
            {
            }
        }

        return null;
    }

    private static IEnumerable<string> ResolveMapPathCandidates(string mapPath)
    {
        var trimmed = mapPath.Trim().Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(trimmed))
        {
            yield return Path.GetFullPath(trimmed);
            yield break;
        }

        foreach (var root in EnumerateRoots())
        {
            yield return Path.GetFullPath(Path.Combine(root, trimmed));
        }
    }

    internal static string ConfigFilePath
    {
        get
        {
            var local = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(
                local,
                "NavBR",
                "openomsi-content-root.txt");
        }
    }

    private static IEnumerable<string> EnumerateRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var env = Environment.GetEnvironmentVariable(RootEnvironmentVariable);
        if (TryNormalizeRoot(env, out var envRoot) && seen.Add(envRoot))
        {
            yield return envRoot;
        }

        string? configured = null;
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                configured = File.ReadAllText(ConfigFilePath).Trim();
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        if (TryNormalizeRoot(configured, out var configuredRoot) &&
            seen.Add(configuredRoot))
        {
            yield return configuredRoot;
        }

        var omsiContent = Environment.GetEnvironmentVariable("OMSI_CONTENT");
        if (TryNormalizeRoot(omsiContent, out var omsiRoot) && seen.Add(omsiRoot))
        {
            yield return omsiRoot;
        }

        var processPath = Environment.ProcessPath;
        var processDirectory = string.IsNullOrWhiteSpace(processPath)
            ? null
            : Path.GetDirectoryName(processPath);
        if (TryNormalizeRoot(processDirectory, out var processRoot) &&
            seen.Add(processRoot))
        {
            yield return processRoot;
        }

        var home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            var fallback = Path.Combine(home, ".openomsi", "content");
            if (TryNormalizeRoot(fallback, out var fallbackRoot) &&
                seen.Add(fallbackRoot))
            {
                yield return fallbackRoot;
            }
        }
    }

    private static bool TryNormalizeRoot(
        string? candidate,
        out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        try
        {
            normalized = Path.GetFullPath(candidate.Trim());
            return Directory.Exists(normalized);
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveMapDirectory(
        string root,
        string? mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return null;
        }

        var maps = ResolveChildDirectory(root, "maps");
        if (maps is null)
        {
            return null;
        }

        var normalizedName = mapName
            .Trim()
            .Replace('\\', '/')
            .Trim('/');
        if (normalizedName.StartsWith("maps/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedName = normalizedName[5..];
        }

        if (normalizedName.EndsWith("/global.cfg", StringComparison.OrdinalIgnoreCase))
        {
            normalizedName = normalizedName[..^11].TrimEnd('/');
        }

        var direct = Path.Combine(maps, normalizedName.Replace('/', Path.DirectorySeparatorChar));
        if (Directory.Exists(direct))
        {
            return Path.GetFullPath(direct);
        }

        try
        {
            return Directory.EnumerateDirectories(maps)
                .FirstOrDefault(path =>
                    string.Equals(
                        Path.GetFileName(path),
                        normalizedName,
                        StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveChildDirectory(
        string parent,
        string name)
    {
        var direct = Path.Combine(parent, name);
        if (Directory.Exists(direct))
        {
            return direct;
        }

        try
        {
            return Directory.EnumerateDirectories(parent)
                .FirstOrDefault(path =>
                    string.Equals(
                        Path.GetFileName(path),
                        name,
                        StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }
}
