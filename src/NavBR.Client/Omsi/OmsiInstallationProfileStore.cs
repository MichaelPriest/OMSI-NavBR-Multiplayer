using System.Text.Json;

namespace NavBR.Client.Omsi;

internal static class OmsiInstallationProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static IReadOnlyList<OmsiInstallationProfile> Load()
    {
        var path = GetPath();
        if (!File.Exists(path))
        {
            return Array.Empty<OmsiInstallationProfile>();
        }

        try
        {
            var json = File.ReadAllText(path);
            var profiles = JsonSerializer.Deserialize<List<OmsiInstallationProfile>>(json, JsonOptions) ?? [];
            return profiles
                .Where(IsValidProfile)
                .GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .OrderByDescending(profile => profile.IsPreferred)
                .ThenBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<OmsiInstallationProfile>();
        }
    }

    public static void Save(IEnumerable<OmsiInstallationProfile> profiles)
    {
        var normalized = profiles
            .Where(IsValidProfile)
            .Select(Normalize)
            .GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();

        var preferredSeen = false;
        for (var i = 0; i < normalized.Length; i++)
        {
            if (!normalized[i].IsPreferred)
            {
                continue;
            }

            if (!preferredSeen)
            {
                preferredSeen = true;
                continue;
            }

            normalized[i] = normalized[i] with { IsPreferred = false };
        }

        var path = GetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(normalized, JsonOptions));
    }

    public static IReadOnlyList<OmsiInstallationProfile> DiscoverAndMerge(
        string? preferredPath = null)
    {
        var profiles = Load().ToDictionary(profile => profile.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var installation in OmsiInstallationLocator.Discover(preferredPath))
        {
            var id = CreateStableId(installation.InstallDirectory);
            if (profiles.ContainsKey(id))
            {
                continue;
            }

            profiles[id] = new OmsiInstallationProfile(
                id,
                CreateDefaultName(installation),
                installation.InstallDirectory,
                IsPreferred: profiles.Count == 0);
        }

        var merged = profiles.Values
            .OrderByDescending(profile => profile.IsPreferred)
            .ThenBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        Save(merged);
        return merged;
    }

    public static OmsiInstallationProfile Upsert(OmsiInstallationProfile profile)
    {
        var profiles = Load().ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var normalized = Normalize(profile);

        if (normalized.IsPreferred)
        {
            foreach (var key in profiles.Keys.ToArray())
            {
                profiles[key] = profiles[key] with { IsPreferred = false };
            }
        }

        profiles[normalized.Id] = normalized;
        Save(profiles.Values);
        return normalized;
    }

    public static void Remove(string id)
    {
        var profiles = Load().Where(profile =>
            !string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase)).ToArray();
        Save(profiles);
    }

    public static string GetPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer",
        "omsi-profiles.json");

    public static string CreateStableId(string installDirectory)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installDirectory)).ToUpperInvariant();
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant();
    }

    private static bool IsValidProfile(OmsiInstallationProfile? profile)
    {
        return profile is not null &&
               !string.IsNullOrWhiteSpace(profile.Id) &&
               !string.IsNullOrWhiteSpace(profile.Name) &&
               !string.IsNullOrWhiteSpace(profile.InstallDirectory);
    }

    private static OmsiInstallationProfile Normalize(OmsiInstallationProfile profile)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(profile.InstallDirectory.Trim()));
        return profile with
        {
            Id = string.IsNullOrWhiteSpace(profile.Id) ? CreateStableId(root) : profile.Id.Trim(),
            Name = profile.Name.Trim(),
            InstallDirectory = root,
            LaunchArguments = string.IsNullOrWhiteSpace(profile.LaunchArguments)
                ? null
                : profile.LaunchArguments.Trim(),
            EnabledMaps = NormalizeList(profile.EnabledMaps),
            EnabledVehicles = NormalizeList(profile.EnabledVehicles)
        };
    }

    private static string[]? NormalizeList(string[]? values)
    {
        if (values is null)
        {
            return null;
        }

        var normalized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return normalized.Length == 0 ? null : normalized;
    }

    private static string CreateDefaultName(OmsiInstallationInfo installation)
    {
        return installation.Source switch
        {
            OmsiInstallationSource.AerosoftRegistry => "OMSI 2 — Aerosoft",
            OmsiInstallationSource.SteamManifest => $"OMSI 2 — {Path.GetFileName(installation.InstallDirectory)}",
            _ => $"OMSI 2 — {Path.GetFileName(installation.InstallDirectory)}"
        };
    }
}
