using NavBR.Shared.PluginBridge;
using System.Globalization;

namespace NavBR.OpenOmsiPlugin;

internal static class OpenOmsiRouteGeometryResolver
{
    private sealed record TileElement(
        bool IsSpline,
        string File,
        long Id,
        double X,
        double Y,
        double Z,
        double Heading,
        double Length,
        double Radius,
        double GradientStart,
        double GradientEnd,
        double? DeltaHeight,
        double CantStart,
        double CantEnd,
        bool Mirror);

    private sealed record PathShape(
        double[] Start,
        double Heading,
        double Radius,
        double Length,
        double DeltaZ,
        double OffsetX);

    public static OpenOmsiRoutePoint[] Resolve(
        OpenOmsiContentContext content,
        OpenOmsiRouteStep[] steps)
    {
        if (!content.MapAvailable || steps.Length == 0)
        {
            return [];
        }

        var layout = OpenOmsiMapLayoutReader.Read(content);
        if (layout is null)
        {
            return [];
        }

        var candidates = new List<OpenOmsiRoutePoint[][]>();
        foreach (var step in steps)
        {
            var tile = layout.Tiles.FirstOrDefault(t => t.Index == step.TileIndex);
            if (tile is null)
            {
                continue;
            }

            var tilePath = ResolveRelativeFile(content.MapDirectory!, tile.File);
            if (tilePath is null)
            {
                continue;
            }

            var element = FindElement(tilePath, step.ObjectId);
            if (element is null)
            {
                continue;
            }

            var basePoints = element.IsSpline
                ? ResolveSplinePath(content.ContentRoot!, layout, tile, element, step.PathIndex)
                : ResolveObjectPath(content.ContentRoot!, layout, tile, element, step.PathIndex);

            if (basePoints.Length < 2)
            {
                continue;
            }

            candidates.Add(
            [
                basePoints,
                [.. basePoints.Reverse()]
            ]);
        }

        if (candidates.Count == 0)
        {
            return [];
        }

        var result = new List<OpenOmsiRoutePoint>();
        OpenOmsiRoutePoint[]? previous = null;

        foreach (var pair in candidates)
        {
            var selected = previous is null
                ? pair[0]
                : SelectClosestOrientation(previous, pair[0], pair[1]);

            if (result.Count > 0 &&
                Distance(result[^1], selected[0]) < 0.75d)
            {
                result.AddRange(selected.Skip(1));
            }
            else
            {
                result.AddRange(selected);
            }

            previous = selected;
        }

        return [.. result];
    }

    private static OpenOmsiRoutePoint[] SelectClosestOrientation(
        OpenOmsiRoutePoint[] previous,
        OpenOmsiRoutePoint[] forward,
        OpenOmsiRoutePoint[] reverse)
    {
        var end = previous[^1];
        return Distance(end, forward[0]) <= Distance(end, reverse[0])
            ? forward
            : reverse;
    }

    private static OpenOmsiRoutePoint[] ResolveObjectPath(
        string root,
        OpenOmsiMapLayout layout,
        OpenOmsiMapTileRef tile,
        TileElement element,
        int pathIndex)
    {
        var objectPath = ResolveRelativeFile(root, element.File);
        if (objectPath is null)
        {
            return [];
        }

        var path = ReadObjectPath(objectPath, pathIndex);
        if (path is null || path.Length <= 0.01d)
        {
            return [];
        }

        var (kx, ky) = OpenOmsiMapLayoutReader.TileScale(layout, tile.Y);
        var objectX = element.X * kx;
        var objectY = element.Y * ky;
        var heading = element.Heading;
        var headingRad = heading * Math.PI / 180d;
        var sh = Math.Sin(headingRad);
        var ch = Math.Cos(headingRad);

        var localX = path.Start[0];
        var localY = path.Start[1];
        var startX = objectX + localX * ch + localY * sh;
        var startY = objectY - localX * sh + localY * ch;
        var startZ = element.Z + path.Start[2];
        var points = SampleArc(
            startX,
            startY,
            startZ,
            heading + path.Heading,
            path.Length,
            path.Radius,
            path.DeltaZ);

        return ToWorldPoints(layout, tile, points, alreadyScaledLocal: true);
    }

    private static OpenOmsiRoutePoint[] ResolveSplinePath(
        string root,
        OpenOmsiMapLayout layout,
        OpenOmsiMapTileRef tile,
        TileElement element,
        int pathIndex)
    {
        var splinePath = ResolveRelativeFile(root, element.File);
        if (splinePath is null)
        {
            return [];
        }

        var offset = ReadSplinePathOffset(splinePath, pathIndex);
        if (offset is null || element.Length <= 0.01d)
        {
            return [];
        }

        var (kx, ky) = OpenOmsiMapLayoutReader.TileScale(layout, tile.Y);
        var k = (kx + ky) / 2d;
        var startX = element.X * kx;
        var startY = element.Y * ky;
        var length = element.Length * k;
        var radius = element.Radius * k;
        var lateral = (element.Mirror ? -1d : 1d) * offset.Value.X;

        var n = (int)Math.Clamp(Math.Ceiling(length / 3d), 1d, 300d);
        var points = new List<(double X, double Y, double Z)>(n + 1);
        for (var i = 0; i <= n; i++)
        {
            var s = length * i / n;
            var (x, y, h) = ArcPoint(
                startX,
                startY,
                element.Heading,
                s,
                radius);

            var hr = h * Math.PI / 180d;
            var rightX = Math.Cos(hr);
            var rightY = -Math.Sin(hr);
            var t = s / Math.Max(length, 1e-6d);
            var centerZ = SplineHeightAt(
                element.Z,
                length,
                s,
                element.GradientStart,
                element.GradientEnd,
                element.DeltaHeight);
            var cant = element.CantStart +
                (element.CantEnd - element.CantStart) * t;
            var cantX = Math.Clamp(
                lateral,
                -offset.Value.HalfCantWidth,
                offset.Value.HalfCantWidth);
            var cantZ = -cantX * cant / 100d;

            points.Add((
                x + rightX * lateral,
                y + rightY * lateral,
                centerZ + offset.Value.Z + cantZ));
        }

        return ToWorldPoints(layout, tile, points, alreadyScaledLocal: true);
    }

    private static OpenOmsiRoutePoint[] ToWorldPoints(
        OpenOmsiMapLayout layout,
        OpenOmsiMapTileRef tile,
        IEnumerable<(double X, double Y, double Z)> points,
        bool alreadyScaledLocal)
    {
        return points
            .Select(point =>
            {
                var worldX = tile.X * layout.TileSizeMeters + point.X;
                var worldY = tile.Y * layout.TileSizeMeters + point.Y;
                return new OpenOmsiRoutePoint(
                    worldX,
                    worldY,
                    point.Z);
            })
            .ToArray();
    }

    private static List<(double X, double Y, double Z)> SampleArc(
        double startX,
        double startY,
        double startZ,
        double heading,
        double length,
        double radius,
        double deltaZ)
    {
        var n = (int)Math.Clamp(Math.Ceiling(length / 3d), 1d, 300d);
        var result = new List<(double X, double Y, double Z)>(n + 1);
        for (var i = 0; i <= n; i++)
        {
            var s = length * i / n;
            var (x, y, _) = ArcPoint(startX, startY, heading, s, radius);
            result.Add((
                x,
                y,
                startZ + deltaZ * (s / Math.Max(length, 1e-6d))));
        }

        return result;
    }


    private static double SplineHeightAt(
        double startZ,
        double length,
        double distance,
        double gradientStart,
        double gradientEnd,
        double? deltaHeight)
    {
        var l = Math.Max(length, 1e-6d);
        var t = Math.Clamp(distance / l, 0d, 1d);
        if (deltaHeight is not null)
        {
            var m0 = gradientStart / 100d * l;
            var m1 = gradientEnd / 100d * l;
            var t2 = t * t;
            var t3 = t2 * t;
            return startZ +
                (t3 - 2d * t2 + t) * m0 +
                (3d * t2 - 2d * t3) * deltaHeight.Value +
                (t3 - t2) * m1;
        }

        var gradient =
            gradientStart +
            (gradientEnd - gradientStart) * t * 0.5d;
        return startZ + distance * gradient / 100d;
    }

    private static (double X, double Y, double Heading) ArcPoint(
        double startX,
        double startY,
        double headingDegrees,
        double distance,
        double radius)
    {
        var heading = headingDegrees * Math.PI / 180d;
        var dx = Math.Sin(heading);
        var dy = Math.Cos(heading);
        if (Math.Abs(radius) < 1e-6d)
        {
            return (
                startX + dx * distance,
                startY + dy * distance,
                headingDegrees);
        }

        var r = Math.Abs(radius);
        var turn = Math.Sign(radius);
        var rightX = dy;
        var rightY = -dx;
        var centerX = startX + rightX * r * turn;
        var centerY = startY + rightY * r * turn;
        var px = startX - centerX;
        var py = startY - centerY;
        var angle = -turn * distance / r;
        var sa = Math.Sin(angle);
        var ca = Math.Cos(angle);

        return (
            centerX + px * ca - py * sa,
            centerY + px * sa + py * ca,
            headingDegrees + turn * (distance / r) * 180d / Math.PI);
    }

    private static TileElement? FindElement(
        string tilePath,
        long wantedId)
    {
        var lines = ReadLines(tilePath);
        var version = ReadTileVersion(lines);
        if (version is > 0 and < 6)
        {
            return null;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var keyword = NormalizeKeyword(lines[i]);
            if (keyword is "object")
            {
                var cursor = i;
                if (Has(version, 9))
                {
                    _ = ReadParameter(lines, ref cursor);
                }

                var file = ReadParameter(lines, ref cursor)?.Trim();
                var id = Has(version, 6)
                    ? ParseLong(ReadParameter(lines, ref cursor))
                    : null;
                var x = ParseDouble(ReadParameter(lines, ref cursor));
                var y = ParseDouble(ReadParameter(lines, ref cursor));
                var z = ParseDouble(ReadParameter(lines, ref cursor));
                var heading = ParseDouble(ReadParameter(lines, ref cursor));
                if (Has(version, 12))
                {
                    _ = ReadParameter(lines, ref cursor);
                    _ = ReadParameter(lines, ref cursor);
                }

                var labels = Has(version, 4)
                    ? ParseInt(ReadParameter(lines, ref cursor)) ?? 0
                    : 0;
                for (var n = 0; n < labels && cursor + 1 < lines.Length; n++)
                {
                    cursor++;
                }

                if (id == wantedId &&
                    !string.IsNullOrWhiteSpace(file) &&
                    x is not null &&
                    y is not null &&
                    z is not null &&
                    heading is not null)
                {
                    return new(
                        false,
                        file,
                        id.Value,
                        x.Value,
                        y.Value,
                        z.Value,
                        heading.Value,
                        0d,
                        0d,
                        0d,
                        0d,
                        null,
                        0d,
                        0d,
                        false);
                }

                i = Math.Max(i, cursor);
                continue;
            }

            if (keyword is not ("spline" or "spline_h"))
            {
                continue;
            }

            var splineCursor = i;
            if (Has(version, 9))
            {
                _ = ReadParameter(lines, ref splineCursor);
            }

            var splineFile = ReadParameter(lines, ref splineCursor)?.Trim();
            var splineId = Has(version, 6)
                ? ParseLong(ReadParameter(lines, ref splineCursor))
                : null;
            if (Has(version, 11))
            {
                _ = ReadParameter(lines, ref splineCursor);
                _ = ReadParameter(lines, ref splineCursor);
            }
            else
            {
                _ = ReadParameter(lines, ref splineCursor);
            }

            var sx = ParseDouble(ReadParameter(lines, ref splineCursor));
            var sz = ParseDouble(ReadParameter(lines, ref splineCursor));
            var sy = ParseDouble(ReadParameter(lines, ref splineCursor));
            var sh = ParseDouble(ReadParameter(lines, ref splineCursor));
            var length = ParseDouble(ReadParameter(lines, ref splineCursor));
            var radius = ParseDouble(ReadParameter(lines, ref splineCursor));
            var gradientStart = ParseDouble(ReadParameter(lines, ref splineCursor)) ?? 0d;
            var gradientEnd = ParseDouble(ReadParameter(lines, ref splineCursor)) ?? 0d;
            var deltaHeight = keyword == "spline_h"
                ? ParseDouble(ReadParameter(lines, ref splineCursor))
                : null;
            var cantStart = 0d;
            var cantEnd = 0d;
            if (Has(version, 5))
            {
                cantStart = ParseDouble(ReadParameter(lines, ref splineCursor)) ?? 0d;
                cantEnd = ParseDouble(ReadParameter(lines, ref splineCursor)) ?? 0d;
            }

            var mirror = false;
            var peek = splineCursor + 1;
            while (peek < lines.Length)
            {
                var text = lines[peek].Trim();
                if (text.StartsWith("[", StringComparison.Ordinal))
                {
                    break;
                }

                if (text.Equals("mirror", StringComparison.OrdinalIgnoreCase))
                {
                    mirror = true;
                    splineCursor = peek;
                    break;
                }

                if (text.StartsWith("Object Nr.", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                splineCursor = peek;
                peek++;
            }

            if (splineId == wantedId &&
                !string.IsNullOrWhiteSpace(splineFile) &&
                sx is not null &&
                sy is not null &&
                sz is not null &&
                sh is not null &&
                length is not null &&
                radius is not null)
            {
                return new(
                    true,
                    splineFile,
                    splineId.Value,
                    sx.Value,
                    sy.Value,
                    sz.Value,
                    sh.Value,
                    length.Value,
                    radius.Value,
                    gradientStart,
                    gradientEnd,
                    deltaHeight,
                    cantStart,
                    cantEnd,
                    mirror);
            }

            i = Math.Max(i, splineCursor);
        }

        return null;
    }

    private static PathShape? ReadObjectPath(string path, int wantedIndex)
    {
        var lines = ReadLines(path);
        var index = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            var keyword = NormalizeKeyword(lines[i]);
            if (keyword is not ("path" or "path_2"))
            {
                continue;
            }

            index++;
            var count = keyword == "path_2" ? 14 : 12;
            var values = new double[count];
            var ok = true;
            for (var n = 0; n < count; n++)
            {
                var value = ParseDouble(ReadParameter(lines, ref i));
                if (value is null)
                {
                    ok = false;
                    break;
                }
                values[n] = value.Value;
            }

            if (!ok || index != wantedIndex)
            {
                continue;
            }

            return new(
                Start: [values[0], values[1], values[2]],
                Heading: values[3],
                Radius: values[4],
                Length: values[5],
                DeltaZ: values[7],
                OffsetX: 0d);
        }

        return null;
    }

    private static (double X, double Z, double HalfCantWidth)? ReadSplinePathOffset(
        string path,
        int wantedIndex)
    {
        var lines = ReadLines(path);
        var index = -1;
        var halfCantWidth = 10d;

        for (var h = 0; h < lines.Length; h++)
        {
            if (NormalizeKeyword(lines[h]) != "halfcantwidth")
            {
                continue;
            }

            var cursor = h;
            var parsed = ParseDouble(ReadParameter(lines, ref cursor));
            if (parsed is >= 0d)
            {
                halfCantWidth = parsed.Value;
            }
        }
        for (var i = 0; i < lines.Length; i++)
        {
            var keyword = NormalizeKeyword(lines[i]);
            if (keyword is not ("path" or "path_2"))
            {
                continue;
            }

            index++;
            _ = ReadParameter(lines, ref i);
            var x = ParseDouble(ReadParameter(lines, ref i));
            var z = ParseDouble(ReadParameter(lines, ref i));
            _ = ReadParameter(lines, ref i);
            _ = ReadParameter(lines, ref i);
            if (keyword == "path_2")
            {
                _ = ReadParameter(lines, ref i);
            }

            if (index == wantedIndex && x is not null && z is not null)
            {
                return (x.Value, z.Value, halfCantWidth);
            }
        }

        return null;
    }

    private static string? ResolveRelativeFile(
        string root,
        string relative)
    {
        var parts = relative
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = root;

        foreach (var part in parts)
        {
            var direct = Path.Combine(current, part);
            if (File.Exists(direct) || Directory.Exists(direct))
            {
                current = direct;
                continue;
            }

            try
            {
                var match = Directory
                    .EnumerateFileSystemEntries(current)
                    .FirstOrDefault(entry =>
                        string.Equals(
                            Path.GetFileName(entry),
                            part,
                            StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    return null;
                }

                current = match;
            }
            catch
            {
                return null;
            }
        }

        return File.Exists(current)
            ? current
            : null;
    }

    private static int ReadTileVersion(string[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (NormalizeKeyword(lines[i]) != "version")
            {
                continue;
            }

            var cursor = i;
            return ParseInt(ReadParameter(lines, ref cursor)) ?? 0;
        }

        return 0;
    }

    private static bool Has(int version, int minimum) =>
        version == 0 || version >= minimum;

    private static string NormalizeKeyword(string line)
    {
        var text = line.Trim();
        if (text.Length >= 2 &&
            text[0] == '[' &&
            text[^1] == ']')
        {
            return text[1..^1].Trim().ToLowerInvariant();
        }

        return string.Empty;
    }

    private static string[] ReadLines(string path)
    {
        var bytes = File.ReadAllBytes(path);
        string text;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            text = System.Text.Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        else
        {
            try
            {
                text = new System.Text.UTF8Encoding(false, true).GetString(bytes);
            }
            catch (System.Text.DecoderFallbackException)
            {
                text = System.Text.Encoding.Latin1.GetString(bytes);
            }
        }

        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static string? ReadParameter(string[] lines, ref int index)
    {
        index++;
        return index < lines.Length ? lines[index] : null;
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    private static long? ParseLong(string? value) =>
        long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    private static double? ParseDouble(string? value) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed) &&
        double.IsFinite(parsed)
            ? parsed
            : null;

    private static double Distance(
        OpenOmsiRoutePoint a,
        OpenOmsiRoutePoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
