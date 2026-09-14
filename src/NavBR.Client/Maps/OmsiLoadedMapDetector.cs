using System.IO;

namespace NavBR.Client.Maps;

/// <summary>
/// Resolves the currently loaded OMSI map from logfile.txt.
/// OMSI writes entries such as:
/// Information: maps\Map Folder\global.cfg map loaded!
///
/// This is intentionally a fallback for builds/installations where TMap.name
/// cannot be read reliably from process memory.
/// </summary>
public static class OmsiLoadedMapDetector
{
    private static readonly object Sync = new();
    private static string? _cachedInstallDirectory;
    private static DateTime _cachedLastWriteUtc;
    private static string? _cachedMapFolder;

    public static string? TryGetLoadedMapFolder(string installDirectory)
    {
        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            return null;
        }

        var logfilePath = Path.Combine(installDirectory, "logfile.txt");

        try
        {
            var lastWriteUtc = File.GetLastWriteTimeUtc(logfilePath);

            lock (Sync)
            {
                if (string.Equals(
                        _cachedInstallDirectory,
                        installDirectory,
                        StringComparison.OrdinalIgnoreCase) &&
                    _cachedLastWriteUtc == lastWriteUtc)
                {
                    return _cachedMapFolder;
                }
            }

            string? latestFolder = null;
            using (var stream = new FileStream(
                       logfilePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true))
            {
                while (reader.ReadLine() is { } line)
                {
                    var parsed = TryParseMapFolder(line);
                    if (!string.IsNullOrWhiteSpace(parsed))
                    {
                        latestFolder = parsed;
                    }
                }
            }

            lock (Sync)
            {
                _cachedInstallDirectory = installDirectory;
                _cachedLastWriteUtc = lastWriteUtc;
                _cachedMapFolder = latestFolder;
            }

            return latestFolder;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static string? TryParseMapFolder(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var normalized = line.Replace('/', '\\');
        const string mapsPrefix = "maps\\";
        const string loadedSuffix = "\\global.cfg map loaded!";

        var start = normalized.IndexOf(mapsPrefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += mapsPrefix.Length;
        var end = normalized.IndexOf(loadedSuffix, start, StringComparison.OrdinalIgnoreCase);
        if (end <= start)
        {
            return null;
        }

        var relativeFolder = normalized[start..end].Trim().Trim('\\');
        if (relativeFolder.Length == 0)
        {
            return null;
        }

        // Maps normally live directly under OMSI\maps. Keep the last path
        // component as the catalog key, while still tolerating unusual nested
        // folders in logfile entries.
        var separator = relativeFolder.LastIndexOf('\\');
        return separator >= 0
            ? relativeFolder[(separator + 1)..]
            : relativeFolder;
    }
}
