using System.Text;

namespace NavBR.Client.Multiplayer;

internal sealed record OpenOmsiVarTableManifest(
    uint Hash,
    ushort[] FloatIds,
    ushort[] StringIds,
    string[] FloatNames,
    string[] StringNames,
    string[] AllFloatNames,
    string[] AllStringNames);

internal static class OpenOmsiVarTableManifestBuilder
{
    private const uint FnvOffset = 0x811C9DC5u;
    private const uint FnvPrime = 0x01000193u;
    private const int PaxDoors = 16;

    internal static OpenOmsiVarTableManifest? TryBuild(
        string omsiRoot,
        string vehiclePath)
    {
        if (string.IsNullOrWhiteSpace(omsiRoot) ||
            string.IsNullOrWhiteSpace(vehiclePath))
        {
            return null;
        }

        string root;
        string bus;
        try
        {
            root = Path.GetFullPath(omsiRoot);
            var relative = vehiclePath
                .Trim()
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
            bus = Path.GetFullPath(Path.Combine(root, relative));
        }
        catch
        {
            return null;
        }

        if (!bus.StartsWith(
                root.TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(bus) ||
            !string.Equals(
                Path.GetExtension(bus),
                ".bus",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                Path.GetExtension(bus),
                ".ovh",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var scriptLists = ReadVehicleScriptLists(bus);
        if (scriptLists is null)
        {
            return null;
        }

        var vars = new List<string>();
        var varIndex = new Dictionary<string, ushort>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var name in BuiltinVars(root))
        {
            Declare(name, vars, varIndex);
        }

        var scriptVars = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var path in scriptLists.Value.VarLists)
        {
            foreach (var name in ReadNameList(path))
            {
                scriptVars.Add(name);
                Declare(name, vars, varIndex);
            }
        }

        var strings = new List<string>();
        var stringIndex = new Dictionary<string, ushort>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var name in BuiltinStringVars(root))
        {
            Declare(name, strings, stringIndex);
        }

        foreach (var path in scriptLists.Value.StringVarLists)
        {
            foreach (var name in ReadNameList(path))
            {
                Declare(name, strings, stringIndex);
            }
        }

        var hash = FnvOffset;
        var floatIds = new List<ushort>();
        var floatNames = new List<string>();
        for (var i = 0; i < vars.Count && i < ushort.MaxValue; i++)
        {
            var name = vars[i];
            if (!scriptVars.Contains(name) || EngineFed(name))
            {
                continue;
            }

            Eat(ref hash, name);
            floatIds.Add((ushort)i);
            floatNames.Add(name);
        }

        var stringIds = new List<ushort>();
        var stringNames = new List<string>();
        for (var i = 0; i < strings.Count && i < ushort.MaxValue; i++)
        {
            Eat(ref hash, strings[i]);
            stringIds.Add((ushort)i);
            stringNames.Add(strings[i]);
        }

        return new OpenOmsiVarTableManifest(
            hash,
            floatIds.ToArray(),
            stringIds.ToArray(),
            floatNames.ToArray(),
            stringNames.ToArray(),
            vars.ToArray(),
            strings.ToArray());
    }

    internal static bool EngineFed(string name)
    {
        var n = name.Trim().ToLowerInvariant();
        var prefixes = new[]
        {
            "wheel_",
            "axle_",
            "velocity",
            "ai_",
            "envir_",
            "dirt",
            "precip",
            "rain_",
            "streetcond",
            "door_",
            "pax_",
            "refresh_"
        };

        return prefixes.Any(n.StartsWith) ||
               n is "n_wheel" or "wetness" or "time" or "timegap";
    }

    private static (string[] VarLists, string[] StringVarLists)?
        ReadVehicleScriptLists(string busPath)
    {
        string[] lines;
        try
        {
            lines = ReadLegacyLines(busPath);
        }
        catch
        {
            return null;
        }

        var baseDir = Path.GetDirectoryName(busPath)
            ?? string.Empty;
        var varLists = new List<string>();
        var stringVarLists = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var keyword = NormalizeKeyword(lines[i]);
            if (keyword is not "varnamelist" and not "stringvarnamelist")
            {
                continue;
            }

            var cursor = i + 1;
            if (!TryNextValue(lines, ref cursor, out var countText) ||
                !int.TryParse(countText, out var count) ||
                count is < 0 or > 1024)
            {
                continue;
            }

            for (var k = 0; k < count; k++)
            {
                if (!TryNextValue(lines, ref cursor, out var relative))
                {
                    break;
                }

                try
                {
                    var path = Path.GetFullPath(
                        Path.Combine(
                            baseDir,
                            relative.Replace(
                                '\\',
                                Path.DirectorySeparatorChar)));
                    if (keyword == "varnamelist")
                    {
                        varLists.Add(path);
                    }
                    else
                    {
                        stringVarLists.Add(path);
                    }
                }
                catch
                {
                }
            }

            i = Math.Max(i, cursor - 1);
        }

        return (varLists.ToArray(), stringVarLists.ToArray());
    }

    private static IReadOnlyList<string> ReadNameList(string path)
    {
        try
        {
            return ReadLegacyLines(path)
                .Select(line => line.Trim())
                .Where(line =>
                    !string.IsNullOrWhiteSpace(line) &&
                    !line.StartsWith("'", StringComparison.Ordinal) &&
                    !line.StartsWith(";", StringComparison.Ordinal))
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string[] ReadLegacyLines(string path)
    {
        var bytes = File.ReadAllBytes(path);
        string text;
        try
        {
            text = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true)
                .GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // OMSI 2 content is frequently stored as legacy Western/ANSI text.
            // Variable identifiers are overwhelmingly ASCII, while Latin-1
            // preserves common accented path characters without adding a
            // platform code-page dependency to the client.
            text = Encoding.Latin1.GetString(bytes);
        }

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        if (lines.Length > 0)
        {
            lines[0] = lines[0].TrimStart('\uFEFF');
        }

        return lines;
    }

    private static IEnumerable<string> BuiltinVars(string root)
    {
        var builtins = ReadNameList(
            Path.Combine(
                root,
                "program",
                "varlist_roadvehicle.txt"))
            .ToList();

        for (var axle = 0; axle < 8; axle++)
        {
            foreach (var side in new[] { "L", "R" })
            {
                foreach (var prefix in new[]
                         {
                             "Wheel_Rotation_",
                             "Wheel_RotationSpeed_",
                             "Axle_Steering_",
                             "Axle_Suspension_",
                             "Axle_Springfactor_",
                             "Axle_Brakeforce_",
                             "Axle_SurfaceID_"
                         })
                {
                    builtins.Add($"{prefix}{axle}_{side}");
                }
            }

            builtins.Add($"PAX_Entry{axle}_Open");
            builtins.Add($"PAX_Entry{axle}_Req");
            builtins.Add($"PAX_Exit{axle}_Open");
            builtins.Add($"PAX_Exit{axle}_Req");
        }

        for (var i = 0; i < 6; i++)
        {
            builtins.Add($"Debug_{i}");
        }

        for (var i = 0; i < 4; i++)
        {
            foreach (var name in new[] { "alpha", "beta", "gamma" })
            {
                builtins.Add($"articulation_{i}_{name}");
            }
        }

        for (var door = 8; door < PaxDoors; door++)
        {
            builtins.Add($"PAX_Entry{door}_Open");
            builtins.Add($"PAX_Entry{door}_Req");
            builtins.Add($"PAX_Exit{door}_Open");
            builtins.Add($"PAX_Exit{door}_Req");
        }

        for (var door = 0; door < PaxDoors; door++)
        {
            builtins.Add($"PAX_Entry{door}_Busy");
            builtins.Add($"PAX_Exit{door}_Busy");
        }

        return builtins;
    }

    private static IEnumerable<string> BuiltinStringVars(string root)
    {
        var path = Path.Combine(
            root,
            "program",
            "stringvarlist_roadvehicle.txt");
        var list = ReadNameList(path);
        return list.Count > 0
            ? list
            : new[]
            {
                "ident",
                "number",
                "act_route",
                "act_busstop",
                "SetLineTo",
                "yard",
                "file_schedule"
            };
    }

    private static void Declare(
        string name,
        List<string> names,
        Dictionary<string, ushort> index)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) ||
            index.ContainsKey(trimmed) ||
            names.Count >= ushort.MaxValue)
        {
            return;
        }

        var id = (ushort)names.Count;
        names.Add(trimmed);
        index[trimmed] = id;
    }

    private static void Eat(ref uint hash, string name)
    {
        foreach (var b in Encoding.UTF8
                     .GetBytes(name.ToLowerInvariant())
                     .Append((byte)0))
        {
            hash = unchecked((hash ^ b) * FnvPrime);
        }
    }

    private static string NormalizeKeyword(string raw)
    {
        var value = raw.Trim();
        if (value.StartsWith("[", StringComparison.Ordinal) &&
            value.EndsWith("]", StringComparison.Ordinal) &&
            value.Length > 2)
        {
            value = value[1..^1];
        }

        return value.Trim().ToLowerInvariant();
    }

    private static bool TryNextValue(
        IReadOnlyList<string> lines,
        ref int cursor,
        out string value)
    {
        while (cursor < lines.Count)
        {
            var candidate = lines[cursor++].Trim();
            if (string.IsNullOrWhiteSpace(candidate) ||
                candidate.StartsWith("'", StringComparison.Ordinal) ||
                candidate.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            value = candidate;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
