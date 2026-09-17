namespace NavBR.Client.Multiplayer;

/// <summary>
/// Session-local RP avatar selection shared by normal/single-player and
/// multiplayer surfaces. Definition pointers are deliberately never persisted.
/// </summary>
internal static class RoleplayCharacterSelectionStore
{
    private static readonly object Sync = new();
    private static string? _mapKey;
    private static RoleplayCharacterOption? _selected;

    public static event Action? Changed;

    public static RoleplayCharacterOption? Get(string? mapKey)
    {
        lock (Sync)
        {
            if (!string.Equals(_mapKey, mapKey, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return _selected;
        }
    }

    public static void Set(string mapKey, RoleplayCharacterOption selected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapKey);
        ArgumentNullException.ThrowIfNull(selected);

        lock (Sync)
        {
            _mapKey = mapKey;
            _selected = selected;
        }

        Changed?.Invoke();
    }

    public static void ResetForMap(string? mapKey)
    {
        var changed = false;
        lock (Sync)
        {
            if (string.Equals(_mapKey, mapKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            changed = _selected is not null || _mapKey is not null;
            _mapKey = mapKey;
            _selected = null;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public static void Clear()
    {
        var changed = false;
        lock (Sync)
        {
            changed = _selected is not null || _mapKey is not null;
            _mapKey = null;
            _selected = null;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }
}
