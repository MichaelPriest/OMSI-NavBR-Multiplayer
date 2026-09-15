using System.IO;
using System.Security.Cryptography;
using System.Text;

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
                var (configName, friendlyName) = TryReadMapNames(globalCfg);
                var displayName = configName ?? friendlyName ?? folderName;
                var roadmap = FindRoadmap(directory);
                var tileFiles = Directory
                    .EnumerateFiles(directory, "tile_*.map", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var compatibilityId = TryBuildCompatibilityId(globalCfg, tileFiles);

                maps.Add(new OmsiMapInfo(
                    folderName,
                    displayName,
                    directory,
                    globalCfg,
                    roadmap,
                    tileFiles.Length,
                    compatibilityId,
                    configName,
                    friendlyName));
            }
            catch (IOException)
            {
                // One broken or locked map must not prevent the other maps from loading.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        // A lista exibida pelo cliente usa esta mesma ordem. Mapas com um
        // roadmap compatível aparecem primeiro para o jogador identificar
        // imediatamente quais já estão prontos para o fundo visual do GPS.
        return maps
            .OrderByDescending(map => !string.IsNullOrWhiteSpace(map.RoadmapPath))
            .ThenBy(map => map.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static (string? ConfigName, string? FriendlyName) TryReadMapNames(string globalCfg)
    {
        try
        {
            var lines = File.ReadAllLines(globalCfg);
            string? configName = null;
            string? friendlyName = null;

            for (var i = 0; i < lines.Length - 1; i++)
            {
                var header = lines[i].Trim();
                var value = lines[i + 1].Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (string.Equals(header, "[name]", StringComparison.OrdinalIgnoreCase))
                {
                    configName = value;
                }
                else if (string.Equals(header, "[friendlyname]", StringComparison.OrdinalIgnoreCase))
                {
                    friendlyName = value;
                }
            }

            return (configName, friendlyName);
        }
        catch (IOException)
        {
            // The catalog is best-effort; an unreadable map must not break NavBR startup.
        }
        catch (UnauthorizedAccessException)
        {
        }

        return (null, null);
    }

    private static string? TryBuildCompatibilityId(string globalCfg, IReadOnlyList<string> tileFiles)
    {
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(File.ReadAllBytes(globalCfg));

            foreach (var tileFile in tileFiles)
            {
                var name = Path.GetFileName(tileFile).ToUpperInvariant();
                var length = new FileInfo(tileFile).Length;
                hash.AppendData(Encoding.UTF8.GetBytes($"\n{name}:{length}"));
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
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

    private static string? FindRoadmap(string directory)
    {
        var textureMapDirectory = Path.Combine(directory, "texture", "map");
        var exactCandidates = new[]
        {
            Path.Combine(textureMapDirectory, "whole.roadmap.bmp"),
            Path.Combine(textureMapDirectory, "roadmap.bmp"),
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

        foreach (var searchDirectory in new[] { textureMapDirectory, directory })
        {
            if (!Directory.Exists(searchDirectory))
            {
                continue;
            }

            try
            {
                var fallback = Directory
                    .EnumerateFiles(searchDirectory, "*roadmap*.bmp", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (fallback is not null)
                {
                    return fallback;
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
}
