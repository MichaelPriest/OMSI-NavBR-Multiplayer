using System.Text.Json;

namespace NavBR.Client.Multiplayer;

internal static class PublicRoomFavoritesStore
{
    private const int CurrentVersion = 1;
    private static readonly object Sync = new();
    private static HashSet<string>? _favorites;

    public static bool IsFavorite(string? roomId)
    {
        var normalized = Normalize(roomId);
        if (normalized is null)
        {
            return false;
        }

        lock (Sync)
        {
            return GetFavoritesLocked().Contains(normalized);
        }
    }

    public static bool Toggle(string? roomId)
    {
        var normalized = Normalize(roomId);
        if (normalized is null)
        {
            return false;
        }

        lock (Sync)
        {
            var favorites = GetFavoritesLocked();
            var nowFavorite = favorites.Add(normalized);
            if (!nowFavorite)
            {
                favorites.Remove(normalized);
            }
            SaveLocked(favorites);
            return nowFavorite;
        }
    }

    public static int Count
    {
        get
        {
            lock (Sync)
            {
                return GetFavoritesLocked().Count;
            }
        }
    }

    private static HashSet<string> GetFavoritesLocked()
    {
        if (_favorites is not null)
        {
            return _favorites;
        }

        try
        {
            var path = GetPath();
            if (!File.Exists(path))
            {
                return _favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<FavoriteModel>(json);
            if (model is null || model.Version != CurrentVersion)
            {
                return _favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return _favorites = model.RoomIds
                .Select(Normalize)
                .Where(value => value is not null)
                .Select(value => value!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return _favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveLocked(HashSet<string> favorites)
    {
        try
        {
            var path = GetPath();
            var directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            var temp = path + ".tmp";
            var model = new FavoriteModel(
                CurrentVersion,
                favorites.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray());
            File.WriteAllText(
                temp,
                JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // Favorites are a convenience feature and must never block room browsing.
        }
    }

    private static string GetPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer",
        "public-room-favorites.json");

    private static string? Normalize(string? roomId) =>
        string.IsNullOrWhiteSpace(roomId) ? null : roomId.Trim();

    private sealed record FavoriteModel(int Version, string[] RoomIds);
}
