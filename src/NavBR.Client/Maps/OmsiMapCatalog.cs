using System.IO;

namespace NavBR.Client.Maps;

public sealed class OmsiMapCatalog
{
    public IReadOnlyList<OmsiMapInfo> Discover(string omsiInstallDirectory)
    {
        var mapsDirectory = Path.Combine(omsiInstallDirectory, "maps");
        if (!Directory.Exists(mapsDirectory))
        {
            return Array.Empty<OmsiMapInfo>();
        }

        string[] mapDirectories;
        try
        {
            mapDirectories = Directory.GetDirectories(mapsDirectory);
        }
        catch (IOException)
        {
            return Array.Empty<OmsiMapInfo>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<OmsiMapInfo>();
        }

        var maps = new List<OmsiMapInfo>();

        foreach (var directory in mapDirectories)
        {
            try
            {
                var globalCfg = Path.Combine(directory, "global.cfg");
                if (!File.Exists(globalCfg))
                {
                    continue;
                }

                var folderName = Path.GetFileName(directory);
                var displayName = TryReadMapName(globalCfg) ?? folderName;
                var roadmap = FindRoadmap(directory);
                var tileCount = Directory
                    .EnumerateFiles(directory, "tile_*.map", SearchOption.TopDirectoryOnly)
                    .Count();

                maps.Add(new OmsiMapInfo(
                    folderName,
                    displayName,
                    directory,
                    globalCfg,
                    roadmap,
                    tileCount));
            }
            catch (IOException)
            {
                // One broken or locked map must not prevent the other maps from loading.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return maps
            .OrderBy(map => map.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static string? TryReadMapName(string globalCfg)
    {
        try
        {
            var lines = File.ReadAllLines(globalCfg);
            for (var i = 0; i < lines.Length - 1; i++)
            {
                if (!string.Equals(lines[i].Trim(), "[name]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = lines[i + 1].Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        catch (IOException)
        {
            // The catalog is best-effort; an unreadable map must not break NavBR startup.
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }

    private static string? FindRoadmap(string directory)
    {
        var exactCandidates = new[]
        {
            Path.Combine(directory, "whole.roadmap.bmp"),
            Path.Combine(directory, "roadmap.bmp")
        };

        foreach (var candidate in exactCandidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        try
        {
            return Directory
                .EnumerateFiles(directory, "*roadmap*.bmp", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
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
}
