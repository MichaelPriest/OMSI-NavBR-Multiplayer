using System.Text.Json;

namespace NavBR.Client.WinUI;

internal static class JsonState
{
    public static JsonElement Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value)
            ? value
            : default;

    public static string? String(JsonElement element, string name)
    {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    public static bool Bool(JsonElement element, string name)
    {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.True;
    }

    public static int? Int(JsonElement element, string name)
    {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt32(out var result)
            ? result
            : null;
    }

    public static double? Double(JsonElement element, string name)
    {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.Number &&
               value.TryGetDouble(out var result)
            ? result
            : null;
    }

    public static IEnumerable<JsonElement> Array(JsonElement element, string name)
    {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : Enumerable.Empty<JsonElement>();
    }

    public static bool IsObject(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object;
}
