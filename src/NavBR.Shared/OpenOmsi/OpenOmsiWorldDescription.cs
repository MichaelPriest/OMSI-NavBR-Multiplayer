using System.Globalization;

namespace NavBR.Shared.OpenOmsi;

public readonly record struct OpenOmsiWorldEntityRef(
    bool IsPerson,
    uint Id);

public abstract record OpenOmsiWorldDescription(uint Id)
{
    public sealed record Car(
        uint Id,
        string File,
        byte? Scheme,
        string Line,
        string Destination)
        : OpenOmsiWorldDescription(Id);

    public sealed record Person(
        uint Id,
        string File)
        : OpenOmsiWorldDescription(Id);
}

public static class OpenOmsiWorldDescriptionCodec
{
    public static string Encode(
        OpenOmsiWorldDescription description) =>
        description switch
        {
            OpenOmsiWorldDescription.Car car =>
                $"DESC|c|{car.Id.ToString(CultureInfo.InvariantCulture)}|" +
                $"{CleanContentPath(car.File, ["bus", "ovh", "sco"]) ?? string.Empty}|" +
                $"{(car.Scheme is byte scheme ? scheme.ToString(CultureInfo.InvariantCulture) : "-")}|" +
                $"{OpenOmsiLanProtocol.CleanText(car.Line, 16)}|" +
                $"{OpenOmsiLanProtocol.CleanText(car.Destination, 64)}",
            OpenOmsiWorldDescription.Person person =>
                $"DESC|p|{person.Id.ToString(CultureInfo.InvariantCulture)}|" +
                $"{CleanContentPath(person.File, ["hum"]) ?? string.Empty}",
            _ => throw new ArgumentOutOfRangeException(
                nameof(description))
        };

    public static bool TryDecode(
        string text,
        out OpenOmsiWorldDescription description)
    {
        description = default!;
        var parts = text.Split('|');
        if (parts.Length < 4 ||
            !string.Equals(
                parts[0],
                "DESC",
                StringComparison.Ordinal) ||
            !uint.TryParse(
                parts[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var id) ||
            id > OpenOmsiWorldCodec.MaxId)
        {
            return false;
        }

        switch (parts[1])
        {
            case "c":
            {
                var file = CleanContentPath(
                    parts[3],
                    ["bus", "ovh", "sco"]);
                if (file is null)
                {
                    return false;
                }

                byte? scheme = null;
                var schemeText =
                    parts.ElementAtOrDefault(4)?.Trim();
                if (!string.IsNullOrWhiteSpace(schemeText) &&
                    schemeText != "-")
                {
                    if (!byte.TryParse(
                            schemeText,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var parsedScheme))
                    {
                        return false;
                    }
                    scheme = parsedScheme;
                }

                description =
                    new OpenOmsiWorldDescription.Car(
                        id,
                        file,
                        scheme,
                        OpenOmsiLanProtocol.CleanText(
                            parts.ElementAtOrDefault(5),
                            16),
                        OpenOmsiLanProtocol.CleanText(
                            parts.ElementAtOrDefault(6),
                            64));
                return true;
            }

            case "p":
            {
                var file = CleanContentPath(
                    parts[3],
                    ["hum"]);
                if (file is null)
                {
                    return false;
                }

                description =
                    new OpenOmsiWorldDescription.Person(
                        id,
                        file);
                return true;
            }

            default:
                return false;
        }
    }

    public static string EncodeWant(
        ushort requesterId,
        IEnumerable<OpenOmsiWorldEntityRef> references)
    {
        var body = string.Join(
            ",",
            references
                .Where(item =>
                    item.Id <= OpenOmsiWorldCodec.MaxId)
                .Take(64)
                .Select(item =>
                    $"{(item.IsPerson ? 'p' : 'c')}{item.Id.ToString(CultureInfo.InvariantCulture)}"));
        return
            $"WANT|{requesterId.ToString(CultureInfo.InvariantCulture)}|{body}";
    }

    public static bool TryDecodeWant(
        string text,
        out ushort requesterId,
        out IReadOnlyList<OpenOmsiWorldEntityRef> references)
    {
        requesterId = 0;
        references =
            Array.Empty<OpenOmsiWorldEntityRef>();

        var parts = text.Split('|', 3);
        if (parts.Length < 3 ||
            !string.Equals(
                parts[0],
                "WANT",
                StringComparison.Ordinal) ||
            !ushort.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out requesterId) ||
            requesterId == 0)
        {
            return false;
        }

        var parsed =
            new List<OpenOmsiWorldEntityRef>(64);
        foreach (var token in parts[2]
                     .Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries)
                     .Take(64))
        {
            var value = token.Trim();
            if (value.Length < 2)
            {
                continue;
            }

            var isPerson = value[0] switch
            {
                'p' => true,
                'c' => false,
                _ => (bool?)null
            };
            if (isPerson is null ||
                !uint.TryParse(
                    value.AsSpan(1),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var id) ||
                id > OpenOmsiWorldCodec.MaxId)
            {
                continue;
            }

            parsed.Add(
                new OpenOmsiWorldEntityRef(
                    isPerson.Value,
                    id));
        }

        references = parsed;
        return parsed.Count > 0;
    }

    private static string? CleanContentPath(
        string? value,
        IReadOnlyCollection<string> extensions)
    {
        var path = value?
            .Trim()
            .Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(path) ||
            path.Length > 260 ||
            path.StartsWith(
                "/",
                StringComparison.Ordinal) ||
            path.Contains(':') ||
            path.Contains('|') ||
            path.Any(char.IsControl))
        {
            return null;
        }

        var parts = path.Split('/');
        if (parts.Any(part =>
                string.IsNullOrWhiteSpace(part) ||
                string.IsNullOrEmpty(
                    part.Trim('.', ' '))))
        {
            return null;
        }

        var extension =
            Path.GetExtension(parts[^1])
                .TrimStart('.');
        return extensions.Contains(
                extension,
                StringComparer.OrdinalIgnoreCase)
            ? string.Join("/", parts)
            : null;
    }
}
