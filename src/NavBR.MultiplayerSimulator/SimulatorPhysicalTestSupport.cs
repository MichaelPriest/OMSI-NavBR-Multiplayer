using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Win32;

internal sealed record SimulatorHofRoute(
    string Line,
    string Route,
    string Description,
    string? DestinationCode);

internal sealed record SimulatorPhysicalSetup(
    SimulatorOptions Options,
    string Message);

internal static class SimulatorPhysicalTestSupport
{
    private static readonly string[] StandardBusCandidates =
    [
        @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
        @"Vehicles\MAN_NL_NG\MAN_EN92.bus"
    ];

    public static SimulatorPhysicalSetup Resolve(SimulatorOptions options)
    {
        var root = DiscoverOmsiRoot(options.OmsiRoot);
        if (string.IsNullOrWhiteSpace(root))
        {
            return new SimulatorPhysicalSetup(
                options,
                "Physical test bus: OMSI root was not discovered; keeping the room/explicit vehicle identity.");
        }

        var resolved = options with { OmsiRoot = root };
        string? selectedRelativePath = null;
        string? selectedCompatibilityId = null;

        // Explicit and room-inherited identities both describe a real local
        // vehicle and must win over the stock test bus. Otherwise the simulator
        // can join with one fingerprint while the host expects another and the
        // physical coordinator correctly refuses to spawn it.
        if (TryResolveExistingVehicle(
                root,
                options.VehiclePath,
                out var inheritedRelativePath,
                out var inheritedCompatibilityId))
        {
            selectedRelativePath = inheritedRelativePath;
            selectedCompatibilityId = inheritedCompatibilityId;
        }
        else
        {
            foreach (var candidate in StandardBusCandidates)
            {
                if (!TryResolveExistingVehicle(
                        root,
                        candidate,
                        out var relativePath,
                        out var compatibilityId))
                {
                    continue;
                }

                selectedRelativePath = relativePath;
                selectedCompatibilityId = compatibilityId;
                break;
            }
        }

        if (selectedRelativePath is null ||
            selectedCompatibilityId is null)
        {
            return new SimulatorPhysicalSetup(
                resolved,
                $"Physical test bus: no usable rigid vehicle definition was found under '{root}'.");
        }

        var routes = ResolveHofRoutes(
            root,
            selectedRelativePath,
            options.MapName);

        resolved = resolved with
        {
            VehiclePath = selectedRelativePath,
            VehicleCompatibilityId = selectedCompatibilityId,
            HofRoutes = routes
        };

        var routeSummary = routes.Length == 0
            ? "no HOF routes resolved (route is optional for spawn)"
            : $"{routes.Length} HOF route(s) available for bot variation";

        return new SimulatorPhysicalSetup(
            resolved,
            $"Physical test bus: {selectedRelativePath} • {routeSummary}.");
    }

    private static bool TryResolveExistingVehicle(
        string root,
        string? relativePath,
        out string normalizedRelativePath,
        out string compatibilityId)
    {
        normalizedRelativePath = string.Empty;
        compatibilityId = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        try
        {
            var candidate = relativePath.Trim()
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
            if (!candidate.StartsWith(
                    $"Vehicles{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase) ||
                candidate.Contains("..", StringComparison.Ordinal) ||
                !(candidate.EndsWith(".bus", StringComparison.OrdinalIgnoreCase) ||
                  candidate.EndsWith(".ovh", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var normalizedRoot = Path.GetFullPath(root);
            var rootPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
                ? normalizedRoot
                : normalizedRoot + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(
                Path.Combine(normalizedRoot, candidate));
            if (!fullPath.StartsWith(
                    rootPrefix,
                    StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(fullPath))
            {
                return false;
            }

            using var stream = File.Open(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var hash = Convert.ToHexString(
                    SHA256.HashData(stream))
                .ToLowerInvariant();

            normalizedRelativePath = Path.GetRelativePath(
                    normalizedRoot,
                    fullPath)
                .Replace('/', '\\');
            compatibilityId = $"sha256:{hash}";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static SimulatorHofRoute[] ResolveHofRoutes(
        string root,
        string relativeVehiclePath,
        string? mapName)
    {
        try
        {
            var fullVehiclePath = Path.Combine(
                root,
                relativeVehiclePath.Replace('\\', Path.DirectorySeparatorChar));
            var vehicleDirectory = Path.GetDirectoryName(fullVehiclePath);
            if (string.IsNullOrWhiteSpace(vehicleDirectory) ||
                !Directory.Exists(vehicleDirectory))
            {
                return [];
            }

            var hofFiles = Directory
                .EnumerateFiles(
                    vehicleDirectory,
                    "*.hof",
                    SearchOption.TopDirectoryOnly)
                .OrderByDescending(path =>
                    !string.IsNullOrWhiteSpace(mapName) &&
                    Path.GetFileNameWithoutExtension(path)
                        .Contains(
                            mapName,
                            StringComparison.OrdinalIgnoreCase))
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var hofFile in hofFiles)
            {
                var routes = ParseHofRoutes(hofFile);
                if (routes.Length > 0)
                {
                    return routes;
                }
            }
        }
        catch
        {
        }

        return [];
    }

    private static SimulatorHofRoute[] ParseHofRoutes(string hofFile)
    {
        try
        {
            var lines = File.ReadAllLines(
                hofFile,
                System.Text.Encoding.Latin1);
            var routes = new List<SimulatorHofRoute>();
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

                var tripCode = Regex.Replace(values[0], @"\s+", string.Empty);
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
                    new SimulatorHofRoute(
                        line,
                        routeCode,
                        values[1].Trim(),
                        string.IsNullOrWhiteSpace(values[2])
                            ? null
                            : values[2].Trim()));

                if (routes.Count >= 64)
                {
                    break;
                }
            }

            return routes
                .DistinctBy(route => $"{route.Line}|{route.Route}")
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string? DiscoverOmsiRoot(string? explicitRoot)
    {
        foreach (var candidate in EnumerateRootCandidates(explicitRoot))
        {
            try
            {
                var root = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(candidate));
                if (File.Exists(Path.Combine(root, "Omsi.exe")) &&
                    Directory.Exists(Path.Combine(root, "Vehicles")))
                {
                    return root;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateRootCandidates(
        string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            yield return explicitRoot;
        }

        foreach (var name in new[]
                 {
                     "NAVBR_OMSI_PATH",
                     "OMSI2_PATH"
                 })
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }

        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        // The simulator is normally executed beside a live OMSI validation
        // session. Prefer the executable that is actually running so custom
        // Steam libraries (for example G:\\Games\\...) do not depend on a
        // registry entry being present or current.
        Process[] omsiProcesses = [];
        try
        {
            omsiProcesses = Process.GetProcessesByName("Omsi");
            foreach (var process in omsiProcesses)
            {
                string? executable = null;
                try
                {
                    executable = process.MainModule?.FileName;
                }
                catch
                {
                }

                if (!string.IsNullOrWhiteSpace(executable) &&
                    Path.GetDirectoryName(executable) is { } runningRoot)
                {
                    yield return runningRoot;
                }
            }
        }
        finally
        {
            foreach (var process in omsiProcesses)
            {
                process.Dispose();
            }
        }

        foreach (var registryCandidate in ReadRegistryCandidates())
        {
            yield return registryCandidate;
        }

        var programFilesX86 =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(
                programFilesX86,
                "Steam",
                "steamapps",
                "common",
                "OMSI 2");
        }
    }

    private static IEnumerable<string> ReadRegistryCandidates()
    {
        var results = new List<string>();
        try
        {
            foreach (var hive in new[]
                     {
                         RegistryHive.LocalMachine,
                         RegistryHive.CurrentUser
                     })
            {
                foreach (var view in new[]
                         {
                             RegistryView.Registry32,
                             RegistryView.Registry64
                         })
                {
                    using var baseKey =
                        RegistryKey.OpenBaseKey(hive, view);
                    using var key = baseKey.OpenSubKey(
                        @"SOFTWARE\aerosoft\OMSI 2");
                    if (key?.GetValue("Product_Path") is string path &&
                        !string.IsNullOrWhiteSpace(path))
                    {
                        results.Add(path);
                    }
                }
            }
        }
        catch
        {
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Valve\Steam");
            if (key?.GetValue("SteamPath") is string steamPath &&
                !string.IsNullOrWhiteSpace(steamPath))
            {
                results.Add(
                    Path.Combine(
                        steamPath.Replace('/', Path.DirectorySeparatorChar),
                        "steamapps",
                        "common",
                        "OMSI 2"));
            }
        }
        catch
        {
        }

        return results;
    }
}
