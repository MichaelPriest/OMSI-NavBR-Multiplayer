using System.Globalization;

namespace NavBR.Shared.OpenOmsi;

/// <summary>
/// Interoperability helpers for the public openOMSI LAN protocol v6.
///
/// The protocol format and limits are compatible with turbo-devv/openOMSI
/// (MIT licensed; see licenses/openOMSI-LICENSE.txt). NavBR keeps this
/// implementation independent so the desktop client does not link Rust code
/// or access openOMSI process memory.
/// </summary>
public static class OpenOmsiLanProtocol
{
    public const byte ProtocolVersion = 6;
    public const byte StateMagic = 0xB3;
    public const int StateHeaderBytes = 6;
    public const int MaxStateBytes = 512;
    public const int MaxDatagramBytes = 1400;
    public const int MaxDoors = 7;
    public const int MaxWheels = 15;
    public const int MaxRearSections = 3;
    public const int MaxLamps = 127;
    public const int MaxSwitches = 31;
    public const int MaxValues = 63;

    public const uint FlagVehicle = 1;
    public const uint FlagEngine = 1 << 1;
    public const uint FlagElectrics = 1 << 2;
    public const uint FlagHorn = 1 << 3;
    public const uint FlagBrake = 1 << 4;
    public const uint FlagReverse = 1 << 5;
    public const uint FlagFog = 1 << 6;
    public const uint FlagKneeling = 1 << 7;
    public const uint FlagWipers = 1 << 8;
    public const uint FlagStopBrake = 1 << 9;

    public static string SessionHex(ulong session) =>
        (session & 0xFFFF_FFFF_FFFFUL).ToString(
            "X12",
            CultureInfo.InvariantCulture);

    public static string EncodeWelcome(
        ushort assignedPlayerId,
        ulong session,
        string hostName,
        OpenOmsiLanWorld world,
        int players)
    {
        if (assignedPlayerId < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(assignedPlayerId));
        }

        return string.Join(
            "|",
            "WELCOME",
            ProtocolVersion.ToString(CultureInfo.InvariantCulture),
            assignedPlayerId.ToString(CultureInfo.InvariantCulture),
            SessionHex(session),
            CleanText(hostName, 32),
            CleanText(world.Map, 260),
            CleanDate(world.Date),
            FormatFinite(world.TimeSeconds, 0d, 86_400d),
            CleanText(world.Weather, 260),
            CleanText(world.Season, 16),
            Math.Clamp(players, 1, 33)
                .ToString(CultureInfo.InvariantCulture));
    }

    public static string EncodeClock(
        OpenOmsiLanWorld world,
        double speed = 1d) =>
        string.Join(
            "|",
            "CLOCK",
            CleanText(world.Map, 260),
            CleanDate(world.Date),
            FormatFinite(world.TimeSeconds, 0d, 86_400d),
            CleanText(world.Weather, 260),
            CleanText(world.Season, 16),
            FormatFinite(speed, 0.01d, 1_000d));

    public static bool TryDecodeClock(
        string text,
        out OpenOmsiLanClock clock)
    {
        clock = default!;
        var parts = text.Split('|');
        if (parts.Length < 7 ||
            !string.Equals(
                parts[0],
                "CLOCK",
                StringComparison.Ordinal) ||
            !TryFinite(parts[3], 0d, 86_400d, out var time) ||
            !TryFinite(parts[6], 0.01d, 1_000d, out var speed))
        {
            return false;
        }

        clock = new OpenOmsiLanClock(
            new OpenOmsiLanWorld(
                CleanText(parts[1], 260),
                CleanDate(parts[2]),
                time,
                CleanText(parts[4], 260),
                CleanText(parts[5], 16)),
            speed);
        return true;
    }

    public static string EncodeNear(
        ushort playerId,
        IEnumerable<OpenOmsiLanFootprint>? footprints = null)
    {
        var body = footprints is null
            ? string.Empty
            : string.Join(
                ";",
                footprints
                    .Take(28)
                    .Select(static f => string.Join(
                        ",",
                        FormatFinite(f.X, -100_000_000d, 100_000_000d),
                        FormatFinite(f.Y, -100_000_000d, 100_000_000d),
                        FormatFinite(f.Z, -100_000d, 100_000d),
                        FormatFinite(NormalizeHeading(f.HeadingDegrees), 0d, 360d),
                        FormatFinite(f.LengthMeters, 0d, 60d),
                        FormatFinite(f.WidthMeters, 0d, 8d))));

        return $"NEAR|{playerId.ToString(CultureInfo.InvariantCulture)}|{body}";
    }

    public static bool TryDecodeNear(
        string text,
        ushort expectedPlayerId,
        out IReadOnlyList<OpenOmsiLanFootprint> footprints)
    {
        footprints = Array.Empty<OpenOmsiLanFootprint>();
        var parts = text.Split('|', 3);
        if (parts.Length < 2 ||
            !string.Equals(parts[0], "NEAR", StringComparison.Ordinal) ||
            !ushort.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var playerId) ||
            playerId != expectedPlayerId)
        {
            return false;
        }

        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2]))
        {
            return true;
        }

        var parsed = new List<OpenOmsiLanFootprint>(28);
        foreach (var encoded in parts[2]
                     .Split(';', StringSplitOptions.RemoveEmptyEntries)
                     .Take(28))
        {
            var values = encoded.Split(',');
            if (values.Length != 6)
            {
                continue;
            }

            var numbers = new double[6];
            var valid = true;
            for (var i = 0; i < numbers.Length; i++)
            {
                if (!double.TryParse(
                        values[i],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out numbers[i]) ||
                    !double.IsFinite(numbers[i]))
                {
                    valid = false;
                    break;
                }
            }

            if (!valid ||
                Math.Abs(numbers[0]) > 100_000_000d ||
                Math.Abs(numbers[1]) > 100_000_000d ||
                Math.Abs(numbers[2]) > 100_000d)
            {
                continue;
            }

            parsed.Add(
                new OpenOmsiLanFootprint(
                    numbers[0],
                    numbers[1],
                    numbers[2],
                    ((numbers[3] % 360d) + 360d) % 360d,
                    Math.Clamp(numbers[4], 0d, 60d),
                    Math.Clamp(numbers[5], 0d, 8d)));
        }

        footprints = parsed;
        return true;
    }

    public static string EncodeInfo(OpenOmsiLanVehicleInfo info)
    {
        var bus = NormalizeVehiclePath(info.VehiclePath);
        var head =
            $"INFO|{info.PlayerId.ToString(CultureInfo.InvariantCulture)}|" +
            $"{CleanText(info.Name, 32)}|" +
            $"{bus ?? string.Empty}|" +
            $"{CleanText(info.Paint, 64)}|" +
            $"{CleanText(info.Line, 16)}|" +
            $"{CleanText(info.Destination, 64)}|" +
            $"{FormatFinite(info.LengthMeters, 0d, 60d)}|" +
            $"{FormatFinite(info.WidthMeters, 0d, 8d)}|" +
            $"{FormatFinite(info.BoxOffsetMeters, -40d, 40d)}|" +
            $"{info.SyncTableHash.ToString("X8", CultureInfo.InvariantCulture)}|" +
            $"{CleanText(info.Tour, 64)}|";

        var figure =
            NormalizeHumanPath(info.FigurePath) ?? string.Empty;
        var displayRoom = Math.Max(
            0,
            MaxDatagramBytes - head.Length - figure.Length - 1);
        var displayTexts = EncodeHexTexts(
            info.DisplayTexts,
            12,
            32,
            displayRoom);
        var withFigure = $"{head}{displayTexts}|{figure}";

        var freeTextureRoom = Math.Max(
            0,
            MaxDatagramBytes - withFigure.Length - 1);
        var freeTextures = EncodeHexTexts(
            info.FreeTextures,
            8,
            128,
            freeTextureRoom);

        return $"{withFigure}|{freeTextures}";
    }

    public static bool TryDecodeHello(
        string text,
        out OpenOmsiLanHello hello)
    {
        hello = default!;
        var parts = text.Split('|');
        if (parts.Length < 11 ||
            !string.Equals(parts[0], "HELLO", StringComparison.Ordinal) ||
            !byte.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var protocol))
        {
            return false;
        }

        var session = parts[2].Trim();
        ulong? requestedSession = null;
        if (!string.IsNullOrEmpty(session) && session != "-")
        {
            if (!ulong.TryParse(
                    session,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var parsedSession))
            {
                return false;
            }

            requestedSession = parsedSession & 0xFFFF_FFFF_FFFFUL;
        }

        if (!double.TryParse(
                parts[7],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var time) ||
            !double.IsFinite(time))
        {
            time = 0d;
        }

        ulong? nonce = null;
        if (ulong.TryParse(
                parts[10],
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var parsedNonce))
        {
            nonce = parsedNonce;
        }

        hello = new OpenOmsiLanHello(
            protocol,
            requestedSession,
            CleanText(parts[3], 32),
            NormalizeVehiclePath(parts[4]),
            new OpenOmsiLanWorld(
                CleanText(parts[5], 260),
                CleanDate(parts[6]),
                time,
                CleanText(parts[8], 260),
                CleanText(parts[9], 16)),
            nonce);
        return true;
    }

    public static bool TryDecodeInfo(
        string text,
        out OpenOmsiLanVehicleInfo info)
    {
        info = default!;
        var parts = text.Split('|');
        if (parts.Length < 11 ||
            !string.Equals(parts[0], "INFO", StringComparison.Ordinal) ||
            !ushort.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var id) ||
            id == 0 ||
            !TryFinite(parts[7], 0d, 60d, out var length) ||
            !TryFinite(parts[8], 0d, 8d, out var width) ||
            !TryFinite(parts[9], -40d, 40d, out var offset) ||
            !uint.TryParse(
                parts[10],
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var table))
        {
            return false;
        }

        info = new OpenOmsiLanVehicleInfo(
            id,
            CleanText(parts[2], 32),
            NormalizeVehiclePath(parts[3]),
            CleanText(parts.ElementAtOrDefault(4), 64),
            CleanText(parts.ElementAtOrDefault(5), 16),
            CleanText(parts.ElementAtOrDefault(6), 64),
            length,
            width,
            offset,
            table,
            CleanText(parts.ElementAtOrDefault(11), 64),
            DecodeHexTexts(parts.ElementAtOrDefault(12), 12, 32),
            NormalizeHumanPath(parts.ElementAtOrDefault(13)),
            DecodeHexTexts(parts.ElementAtOrDefault(14), 8, 128));
        return true;
    }

    public static string? NormalizeVehiclePath(string? value) =>
        NormalizeContentPath(value, ["bus", "ovh"]);

    public static string? NormalizeHumanPath(string? value) =>
        NormalizeContentPath(value, ["hum"]);

    public static string CleanText(string? value, int maxCharacters)
    {
        if (string.IsNullOrEmpty(value) || maxCharacters <= 0)
        {
            return string.Empty;
        }

        return new string(
                value
                    .Select(ch => ch == '|' || char.IsControl(ch) ? ' ' : ch)
                    .Take(maxCharacters * 4)
                    .ToArray())
            .Trim()
            .Take(maxCharacters)
            .Aggregate(
                new System.Text.StringBuilder(),
                static (builder, ch) => builder.Append(ch))
            .ToString()
            .TrimEnd();
    }

    private static string? NormalizeContentPath(
        string? value,
        IReadOnlyCollection<string> allowedExtensions)
    {
        var path = value?.Trim().Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(path) ||
            path.Length > 260 ||
            path.StartsWith('/') ||
            path.Contains(':') ||
            path.Contains('|') ||
            path.Any(char.IsControl))
        {
            return null;
        }

        var parts = path.Split('/');
        if (parts.Any(part =>
                string.IsNullOrWhiteSpace(part) ||
                string.IsNullOrEmpty(part.Trim('.', ' '))))
        {
            return null;
        }

        var extension = Path.GetExtension(parts[^1]).TrimStart('.');
        return allowedExtensions.Contains(
                extension,
                StringComparer.OrdinalIgnoreCase)
            ? string.Join("/", parts)
            : null;
    }

    private static string CleanDate(string? value)
    {
        if (DateOnly.TryParseExact(
                value?.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    private static string FormatFinite(
        double value,
        double min,
        double max)
    {
        if (!double.IsFinite(value))
        {
            value = 0d;
        }

        return Math.Clamp(value, min, max).ToString(
            "0.##",
            CultureInfo.InvariantCulture);
    }

    private static bool TryFinite(
        string? value,
        double min,
        double max,
        out double result)
    {
        if (double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result) &&
            double.IsFinite(result) &&
            result >= min &&
            result <= max)
        {
            return true;
        }

        result = 0d;
        return false;
    }

    private static string EncodeHexTexts(
        IReadOnlyList<string>? values,
        int maxItems,
        int maxCharacters,
        int maxOutputCharacters)
    {
        if (values is null ||
            values.Count == 0 ||
            maxOutputCharacters <= 0)
        {
            return string.Empty;
        }

        var output = new List<string>(Math.Min(values.Count, maxItems));
        var used = 0;
        foreach (var raw in values.Take(maxItems))
        {
            var safe = new string(
                (raw ?? string.Empty)
                    .Where(ch => !char.IsControl(ch))
                    .Take(maxCharacters)
                    .ToArray());
            var bytes = System.Text.Encoding.UTF8.GetBytes(safe);
            var hex = Convert.ToHexString(bytes).ToLowerInvariant();
            used += hex.Length + 1;
            if (used > Math.Min(maxOutputCharacters, 720))
            {
                break;
            }

            output.Add(hex);
        }

        return string.Join(",", output);
    }

    private static string[] DecodeHexTexts(
        string? value,
        int maxItems,
        int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var output = new List<string>();
        foreach (var hex in value.Split(',').Take(maxItems))
        {
            try
            {
                if ((hex.Length & 1) != 0)
                {
                    continue;
                }

                var text = System.Text.Encoding.UTF8.GetString(
                    Convert.FromHexString(hex));
                output.Add(
                    new string(
                        text.Where(ch => !char.IsControl(ch))
                            .Take(maxCharacters)
                            .ToArray()));
            }
            catch (FormatException)
            {
            }
        }

        return output.ToArray();
    }

    private static double NormalizeHeading(double value)
    {
        var result = value % 360d;
        return result < 0d ? result + 360d : result;
    }
}

public sealed record OpenOmsiLanWorld(
    string Map,
    string Date,
    double TimeSeconds,
    string Weather,
    string Season);

public sealed record OpenOmsiLanClock(
    OpenOmsiLanWorld World,
    double Speed);

public sealed record OpenOmsiLanHello(
    byte Protocol,
    ulong? RequestedSession,
    string Name,
    string? VehiclePath,
    OpenOmsiLanWorld World,
    ulong? Nonce);

public sealed record OpenOmsiLanVehicleInfo(
    ushort PlayerId,
    string Name,
    string? VehiclePath,
    string Paint,
    string Line,
    string Destination,
    double LengthMeters,
    double WidthMeters,
    double BoxOffsetMeters,
    uint SyncTableHash,
    string Tour,
    IReadOnlyList<string> DisplayTexts,
    string? FigurePath,
    IReadOnlyList<string> FreeTextures);

public sealed record OpenOmsiLanFootprint(
    double X,
    double Y,
    double Z,
    double HeadingDegrees,
    double LengthMeters,
    double WidthMeters);
