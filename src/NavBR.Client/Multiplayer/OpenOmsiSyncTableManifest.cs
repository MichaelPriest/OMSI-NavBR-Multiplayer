using System.Globalization;
using System.Text;

namespace NavBR.Client.Multiplayer;

internal sealed record OpenOmsiSyncTableManifest(
    uint Hash,
    ushort[] LampIds,
    ushort[] SwitchIds,
    ushort[] ValueIds,
    ushort[] DoorIds,
    string[] LampNames,
    string[] SwitchNames,
    string[] ValueNames);

/// <summary>
/// Builds the visual LAN SyncTable used by openOMSI from installed OMSI vehicle files.
/// The ordering, filtering and hash format mirror crates/omsi-app/src/lan.rs.
/// </summary>
internal static class OpenOmsiSyncTableManifestBuilder
{
    private const uint FnvOffset = 0x811C9DC5u;
    private const uint FnvPrime = 0x01000193u;
    private static readonly string[] Doorish =
        ["door", "tuer", "tür", "ramp", "kryshka", "dver"];

    internal static OpenOmsiSyncTableManifest? TryBuild(
        string omsiRoot,
        string vehiclePath,
        OpenOmsiVarTableManifest variables)
    {
        if (!TryResolveVehicle(
                omsiRoot,
                vehiclePath,
                out var root,
                out var leadBus))
        {
            return null;
        }

        var varIds = variables.AllFloatNames
            .Select((name, index) => (name, index))
            .Where(item => item.index <= ushort.MaxValue)
            .GroupBy(item => item.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (ushort)group.First().index,
                StringComparer.OrdinalIgnoreCase);

        if (varIds.Count == 0)
        {
            return null;
        }

        var vehicleFiles = ReadVehicleChain(leadBus);
        if (vehicleFiles.Count == 0)
        {
            return null;
        }

        var paintVars = ReadPaintVariables(vehicleFiles);
        var lampCandidates = new List<string>();
        var switchCandidates = new List<string>();
        var visibleValueCandidates = new List<string>();

        foreach (var vehicle in vehicleFiles)
        {
            if (vehicle.ModelPath is not null)
            {
                ReadModelVariables(
                    vehicle.ModelPath,
                    lampCandidates,
                    switchCandidates,
                    visibleValueCandidates);
            }
        }

        var lamps = Collect(
            lampCandidates,
            varIds,
            name =>
                OpenOmsiVarTableManifestBuilder.EngineFed(name) ||
                paintVars.Contains(name),
            OpenOmsiLanProtocol.MaxLamps);

        var lampIds = lamps.Select(item => item.Id).ToHashSet();
        var switches = Collect(
            switchCandidates,
            varIds,
            name =>
                OpenOmsiVarTableManifestBuilder.EngineFed(name) ||
                paintVars.Contains(name) ||
                varIds.TryGetValue(name, out var id) &&
                lampIds.Contains(id),
            OpenOmsiLanProtocol.MaxSwitches);

        var taken = lamps
            .Concat(switches)
            .Select(item => item.Id)
            .ToHashSet();

        var values = Collect(
            visibleValueCandidates,
            varIds,
            name =>
                EngineFedVisual(name) ||
                !varIds.TryGetValue(name, out var id) ||
                taken.Contains(id),
            OpenOmsiLanProtocol.MaxValues)
            .ToList();

        // Sound variables are appended after visible moving parts, just as openOMSI
        // does, so a capped table gives visual animations priority.
        foreach (var vehicle in vehicleFiles)
        {
            if (values.Count >= OpenOmsiLanProtocol.MaxValues)
            {
                break;
            }

            var names = ReadOutsideSoundVariables(vehicle);
            var already = values.Select(item => item.Id).ToHashSet();
            var more = Collect(
                names,
                varIds,
                name =>
                    EngineFedVisual(name) ||
                    !varIds.TryGetValue(name, out var id) ||
                    taken.Contains(id) ||
                    already.Contains(id),
                OpenOmsiLanProtocol.MaxValues - values.Count);
            values.AddRange(more);
        }

        var doors = new List<ushort>();
        for (var i = 0; i < OpenOmsiLanProtocol.MaxDoors; i++)
        {
            if (!varIds.TryGetValue($"door_{i}", out var id))
            {
                break;
            }

            doors.Add(id);
        }

        var key = new StringBuilder();
        AppendKey(key, "lamps", lamps);
        AppendKey(key, "switches", switches);
        AppendKey(key, "values", values);
        key.Append("doors:")
            .Append(doors.Count.ToString(CultureInfo.InvariantCulture));

        var hash = Fnv1A(Encoding.UTF8.GetBytes(key.ToString()));
        if (hash == 0)
        {
            hash = 1;
        }

        return new OpenOmsiSyncTableManifest(
            hash,
            lamps.Select(item => item.Id).ToArray(),
            switches.Select(item => item.Id).ToArray(),
            values.Select(item => item.Id).ToArray(),
            doors.ToArray(),
            lamps.Select(item => item.Name).ToArray(),
            switches.Select(item => item.Name).ToArray(),
            values.Select(item => item.Name).ToArray());
    }

    private sealed record VehicleFiles(
        string BusPath,
        string? ModelPath,
        string? SoundPath,
        string? SoundAiPath);

    private sealed record SyncEntry(string Name, ushort Id);

    private static bool TryResolveVehicle(
        string omsiRoot,
        string vehiclePath,
        out string root,
        out string bus)
    {
        root = string.Empty;
        bus = string.Empty;
        try
        {
            root = Path.GetFullPath(omsiRoot);
            bus = Path.GetFullPath(
                Path.Combine(
                    root,
                    vehiclePath
                        .Trim()
                        .Replace('/', Path.DirectorySeparatorChar)
                        .TrimStart(Path.DirectorySeparatorChar)));
            return bus.StartsWith(
                       root.TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase) &&
                   File.Exists(bus);
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<VehicleFiles> ReadVehicleChain(string leadBus)
    {
        var result = new List<VehicleFiles>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = leadBus;

        for (var part = 0;
             part <= OpenOmsiLanProtocol.MaxRearSections &&
             !string.IsNullOrWhiteSpace(current) &&
             seen.Add(current);
             part++)
        {
            var parsed = ReadVehicleFiles(current);
            if (parsed is null)
            {
                break;
            }

            result.Add(parsed.Value.Files);
            current = parsed.Value.RearBusPath;
        }

        return result;
    }

    private static (VehicleFiles Files, string? RearBusPath)?
        ReadVehicleFiles(string busPath)
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

        var directory = Path.GetDirectoryName(busPath) ?? string.Empty;
        string? model = null;
        string? sound = null;
        string? soundAi = null;
        string? rear = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var keyword = NormalizeKeyword(lines[i]);
            var cursor = i + 1;
            switch (keyword)
            {
                case "model":
                    if (TryNextValue(lines, ref cursor, out var modelRel))
                    {
                        model = ResolveRelative(directory, modelRel);
                    }
                    break;
                case "sound":
                    if (TryNextValue(lines, ref cursor, out var soundRel))
                    {
                        sound = ResolveRelative(directory, soundRel);
                    }
                    break;
                case "sound_ai":
                    if (TryNextValue(lines, ref cursor, out var soundAiRel))
                    {
                        soundAi = ResolveRelative(directory, soundAiRel);
                    }
                    break;
                case "couple_back":
                    if (TryNextValue(lines, ref cursor, out var rearRel))
                    {
                        rear = ResolveRelative(directory, rearRel);
                    }
                    break;
            }
        }

        return (
            new VehicleFiles(busPath, model, sound, soundAi),
            rear is not null && File.Exists(rear) ? rear : null);
    }

    private static HashSet<string> ReadPaintVariables(
        IReadOnlyList<VehicleFiles> vehicles)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var vehicle in vehicles)
        {
            var baseDir = Path.GetDirectoryName(vehicle.BusPath);
            if (string.IsNullOrWhiteSpace(baseDir) ||
                !Directory.Exists(baseDir))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory
                    .EnumerateFiles(baseDir, "*.cti", SearchOption.AllDirectories)
                    .Take(512)
                    .ToArray();
            }
            catch
            {
                files = Array.Empty<string>();
            }

            foreach (var file in files)
            {
                string[] lines;
                try
                {
                    lines = ReadLegacyLines(file);
                }
                catch
                {
                    continue;
                }

                for (var i = 0; i < lines.Length; i++)
                {
                    if (NormalizeKeyword(lines[i]) != "setvar")
                    {
                        continue;
                    }

                    var cursor = i + 1;
                    if (TryNextValue(lines, ref cursor, out var name))
                    {
                        result.Add(name.Trim());
                    }
                }
            }
        }

        return result;
    }

    private sealed class MeshScan
    {
        internal string File = string.Empty;
        internal string? Ident;
        internal string? MouseEvent;
        internal readonly List<string> Animations = [];
    }

    private static void ReadModelVariables(
        string modelPath,
        ICollection<string> lamps,
        ICollection<string> switches,
        ICollection<string> values)
    {
        if (!File.Exists(modelPath))
        {
            return;
        }

        string[] lines;
        try
        {
            lines = ReadLegacyLines(modelPath);
        }
        catch
        {
            return;
        }

        MeshScan? mesh = null;
        void FlushMesh()
        {
            if (mesh is null)
            {
                return;
            }

            var file = mesh.File.ToLowerInvariant();
            var wiper =
                (file.Contains("wisch", StringComparison.Ordinal) ||
                 file.Contains("wiper", StringComparison.Ordinal)) &&
                string.IsNullOrWhiteSpace(mesh.MouseEvent);
            var door =
                DoorLike(file) ||
                DoorLike(mesh.Ident) ||
                DoorLike(mesh.MouseEvent) ||
                mesh.Animations.Any(DoorLike);

            if (wiper || door)
            {
                foreach (var variable in mesh.Animations)
                {
                    values.Add(variable);
                }
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var keyword = NormalizeKeyword(lines[i]);
            var cursor = i + 1;
            switch (keyword)
            {
                case "mesh":
                    FlushMesh();
                    mesh = new MeshScan();
                    if (TryNextValue(lines, ref cursor, out var meshFile))
                    {
                        mesh.File = meshFile.Trim();
                    }
                    break;
                case "mesh_ident":
                    if (mesh is not null &&
                        TryNextValue(lines, ref cursor, out var ident))
                    {
                        mesh.Ident = ident.Trim();
                    }
                    break;
                case "mouseevent":
                    if (mesh is not null &&
                        TryNextValue(lines, ref cursor, out var mouseEvent))
                    {
                        mesh.MouseEvent = mouseEvent.Trim();
                    }
                    break;
                case "visible":
                    if (TryNextValue(lines, ref cursor, out var visible))
                    {
                        switches.Add(visible.Trim());
                    }
                    break;
                case "anim_rot":
                case "anim_trans":
                    if (mesh is not null &&
                        TryNextValue(lines, ref cursor, out var animation))
                    {
                        mesh.Animations.Add(animation.Trim());
                    }
                    break;
                case "matl_change":
                    if (TryValues(lines, i + 1, 3, out var change))
                    {
                        lamps.Add(change[2]);
                    }
                    break;
                case "matl_lightmap":
                    if (TryValues(lines, i + 1, 2, out var lightmap))
                    {
                        lamps.Add(lightmap[1]);
                    }
                    break;
                case "light_enh":
                    if (TryValues(lines, i + 1, 8, out var lightEnh))
                    {
                        lamps.Add(lightEnh[7]);
                    }
                    break;
                case "light_enh_2":
                    if (TryValues(lines, i + 1, 18, out var lightEnh2))
                    {
                        lamps.Add(lightEnh2[17]);
                    }
                    break;
                case "interiorlight":
                    if (TryNextValue(lines, ref cursor, out var interior))
                    {
                        lamps.Add(interior.Trim());
                    }
                    break;
                case "spotlight_2":
                    if (TryValues(lines, i + 1, 13, out var spotlight))
                    {
                        lamps.Add(spotlight[12]);
                    }
                    break;
                case "alphascale":
                    if (TryNextValue(lines, ref cursor, out var alpha) &&
                        alpha.Trim().StartsWith(
                            "rain_window",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        values.Add(alpha.Trim());
                    }
                    break;
                case "texcoordtransx":
                case "texcoordtransy":
                    if (TryNextValue(lines, ref cursor, out var scroll))
                    {
                        values.Add(scroll.Trim());
                    }
                    break;
            }
        }

        FlushMesh();
    }

    private static IEnumerable<string> ReadOutsideSoundVariables(
        VehicleFiles vehicle)
    {
        var output = new List<string>();
        if (vehicle.SoundAiPath is not null)
        {
            output.AddRange(
                ReadSoundVariables(
                    vehicle.SoundAiPath,
                    outsideOnly: false));
            if (vehicle.SoundPath is not null)
            {
                output.AddRange(
                    ReadSoundVariables(
                        vehicle.SoundPath,
                        outsideOnly: true));
            }
        }
        else if (vehicle.SoundPath is not null)
        {
            output.AddRange(
                ReadSoundVariables(
                    vehicle.SoundPath,
                    outsideOnly: false));
        }

        return output;
    }

    private sealed class SoundEntryScan
    {
        internal readonly List<string> Variables = [];
        internal readonly List<string> OutsideHints = [];
    }

    private static IEnumerable<string> ReadSoundVariables(
        string path,
        bool outsideOnly)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<string>();
        }

        string[] lines;
        try
        {
            lines = ReadLegacyLines(path);
        }
        catch
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        SoundEntryScan? entry = null;

        void Flush()
        {
            if (entry is null)
            {
                return;
            }

            var outside =
                entry.OutsideHints.Any(
                    hint =>
                    {
                        var lower = hint.ToLowerInvariant();
                        return lower.Contains("hupe", StringComparison.Ordinal) ||
                               lower.Contains("horn", StringComparison.Ordinal) ||
                               lower.Contains("blinker", StringComparison.Ordinal) ||
                               lower.Contains("kneel", StringComparison.Ordinal);
                    });

            if (!outsideOnly || outside)
            {
                result.AddRange(entry.Variables);
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var keyword = NormalizeKeyword(lines[i]);
            var cursor = i + 1;
            switch (keyword)
            {
                case "sound":
                    Flush();
                    entry = new SoundEntryScan();
                    break;
                case "loopsound":
                    Flush();
                    entry = new SoundEntryScan();
                    if (TryValues(lines, i + 1, 3, out var loop) &&
                        !string.IsNullOrWhiteSpace(loop[2]))
                    {
                        entry.Variables.Add(loop[2]);
                    }
                    break;
                case "volcurve":
                    if (entry is not null &&
                        TryNextValue(lines, ref cursor, out var volume))
                    {
                        entry.Variables.Add(volume);
                        entry.OutsideHints.Add(volume);
                    }
                    break;
                case "conditionbool":
                case "conditionint":
                case "conditionsingle":
                    if (entry is not null &&
                        TryNextValue(lines, ref cursor, out var condition))
                    {
                        entry.Variables.Add(condition);
                    }
                    break;
                case "trigger":
                case "trigger2":
                case "trigger3":
                    if (entry is not null &&
                        TryNextValue(lines, ref cursor, out var trigger))
                    {
                        entry.OutsideHints.Add(trigger);
                    }
                    break;
            }
        }

        Flush();
        return result;
    }

    private static IReadOnlyList<SyncEntry> Collect(
        IEnumerable<string> names,
        IReadOnlyDictionary<string, ushort> varIds,
        Func<string, bool> skip,
        int cap)
    {
        return names
            .Select(name => name.Trim())
            .Where(name =>
                name.Length > 0 &&
                !float.TryParse(
                    name,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out _))
            .Where(name => varIds.ContainsKey(name))
            .Select(name =>
            {
                var id = varIds[name];
                var canonical = varIds
                    .First(pair => pair.Value == id)
                    .Key;
                return new SyncEntry(canonical, id);
            })
            .Where(entry => !skip(entry.Name))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(Math.Max(0, cap))
            .ToArray();
    }

    private static bool EngineFedVisual(string name) =>
        OpenOmsiVarTableManifestBuilder.EngineFed(name) &&
        !name.Trim().StartsWith(
            "rain_window",
            StringComparison.OrdinalIgnoreCase);

    private static bool DoorLike(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lower = text.ToLowerInvariant();
        return Doorish.Any(
            word => lower.Contains(word, StringComparison.Ordinal));
    }

    private static void AppendKey(
        StringBuilder key,
        string tag,
        IEnumerable<SyncEntry> entries)
    {
        key.Append(tag).Append(':');
        foreach (var entry in entries)
        {
            key.Append(entry.Name.ToLowerInvariant()).Append(',');
        }
        key.Append(';');
    }

    private static uint Fnv1A(ReadOnlySpan<byte> data)
    {
        var hash = FnvOffset;
        foreach (var value in data)
        {
            hash = unchecked((hash ^ value) * FnvPrime);
        }

        return hash;
    }

    private static string? ResolveRelative(
        string directory,
        string raw)
    {
        try
        {
            var relative = raw
                .Trim()
                .Trim('"')
                .Replace('\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(directory, relative));
        }
        catch
        {
            return null;
        }
    }

    private static string[] ReadLegacyLines(string path)
    {
        var bytes = File.ReadAllBytes(path);
        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
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

    private static bool TryValues(
        IReadOnlyList<string> lines,
        int cursor,
        int count,
        out string[] values)
    {
        var result = new List<string>(count);
        while (cursor < lines.Count && result.Count < count)
        {
            var candidate = lines[cursor++].Trim();
            if (candidate.Length == 0 ||
                candidate.StartsWith("'", StringComparison.Ordinal) ||
                candidate.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            if (candidate.StartsWith("[", StringComparison.Ordinal))
            {
                values = Array.Empty<string>();
                return false;
            }

            result.Add(candidate);
        }

        values = result.ToArray();
        return values.Length == count;
    }

    private static bool TryNextValue(
        IReadOnlyList<string> lines,
        ref int cursor,
        out string value)
    {
        while (cursor < lines.Count)
        {
            var candidate = lines[cursor++].Trim();
            if (candidate.Length == 0 ||
                candidate.StartsWith("'", StringComparison.Ordinal) ||
                candidate.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            if (candidate.StartsWith("[", StringComparison.Ordinal))
            {
                break;
            }

            value = candidate;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
