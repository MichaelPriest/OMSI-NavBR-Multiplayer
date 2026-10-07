using NavBR.Shared.PluginBridge;

namespace NavBR.Client.PluginBridge;

public sealed record LocalOmsiScriptVarsSnapshot(
    DateTimeOffset CapturedAtUtc,
    uint VarTableHash,
    ushort[] VariableIndices,
    float[] VariableValues)
{
    public static LocalOmsiScriptVarsSnapshot? FromMessage(
        PluginBridgeMessage message)
    {
        if (message.VarTableHash is not uint hash ||
            hash == 0 ||
            message.VariableIndices is not { } ids ||
            message.VariableValues is not { } values ||
            ids.Length != values.Length ||
            ids.Length > 256 ||
            values.Any(value => !float.IsFinite(value)))
        {
            return null;
        }

        var capturedAt = message.TimestampUnixMilliseconds is long ms
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
            : DateTimeOffset.UtcNow;

        return new LocalOmsiScriptVarsSnapshot(
            capturedAt,
            hash,
            ids.ToArray(),
            values.ToArray());
    }
}

public static class LocalOmsiScriptVarsSnapshotStore
{
    private static LocalOmsiScriptVarsSnapshot? _latest;

    public static LocalOmsiScriptVarsSnapshot? Latest =>
        Volatile.Read(ref _latest);

    public static void Update(PluginBridgeMessage message)
    {
        var snapshot = LocalOmsiScriptVarsSnapshot.FromMessage(message);
        if (snapshot is not null)
        {
            Volatile.Write(ref _latest, snapshot);
        }
    }

    public static void Clear() =>
        Volatile.Write(ref _latest, null);
}
