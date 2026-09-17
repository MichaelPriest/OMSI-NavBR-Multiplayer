using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using NavBR.Shared.Network;

namespace NavBR.Client.Network;

internal sealed record NavBRIdentityData(
    string PlayerId,
    string DisplayName,
    string KeyName,
    string PublicKeySpkiBase64,
    DateTimeOffset CreatedAtUtc);

internal static class NavBRIdentityStore
{
    private static readonly object Sync = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer",
        "Network");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "identity.json");
    private static NavBRIdentityData? _cached;

    public static NavBRPublicIdentity LoadOrCreate(string? preferredDisplayName = null)
    {
        lock (Sync)
        {
            _cached ??= LoadCore() ?? CreateCore(preferredDisplayName);
            return ToPublic(_cached);
        }
    }

    public static NavBRPublicIdentity UpdateDisplayName(string displayName)
    {
        lock (Sync)
        {
            _cached ??= LoadCore() ?? CreateCore(displayName);
            var normalized = NormalizeDisplayName(displayName);
            if (!string.Equals(_cached.DisplayName, normalized, StringComparison.Ordinal))
            {
                _cached = _cached with { DisplayName = normalized };
                Persist(_cached);
            }
            return ToPublic(_cached);
        }
    }

    public static string SignJoinRequest(
        string companyId,
        string inviteCode,
        long timestampUnixMilliseconds)
    {
        lock (Sync)
        {
            _cached ??= LoadCore() ?? CreateCore(null);
            var identity = ToPublic(_cached);
            using var ecdsa = OpenKey(_cached.KeyName);
            var signature = ecdsa.SignData(
                CompanyNetworkSignatures.BuildJoinPayload(
                    companyId,
                    inviteCode,
                    identity,
                    timestampUnixMilliseconds),
                HashAlgorithmName.SHA256);
            return Convert.ToBase64String(signature);
        }
    }

    private static NavBRIdentityData? LoadCore()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var parsed = JsonSerializer.Deserialize<NavBRIdentityData>(File.ReadAllText(FilePath));
            if (parsed is null ||
                string.IsNullOrWhiteSpace(parsed.PlayerId) ||
                string.IsNullOrWhiteSpace(parsed.KeyName) ||
                string.IsNullOrWhiteSpace(parsed.PublicKeySpkiBase64) ||
                !CngKey.Exists(parsed.KeyName))
            {
                return null;
            }

            using var ecdsa = OpenKey(parsed.KeyName);
            var publicKey = ecdsa.ExportSubjectPublicKeyInfo();
            var expectedId = CompanyNetworkSignatures.BuildPlayerId(publicKey);
            if (!string.Equals(expectedId, parsed.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return parsed with
            {
                PlayerId = expectedId,
                DisplayName = NormalizeDisplayName(parsed.DisplayName),
                PublicKeySpkiBase64 = Convert.ToBase64String(publicKey)
            };
        }
        catch
        {
            return null;
        }
    }

    private static NavBRIdentityData CreateCore(string? preferredDisplayName)
    {
        Directory.CreateDirectory(DirectoryPath);
        var keyName = $"NavBR.Identity.{Guid.NewGuid():N}";
        using var key = CngKey.Create(
            CngAlgorithm.ECDsaP256,
            keyName,
            new CngKeyCreationParameters
            {
                ExportPolicy = CngExportPolicies.None,
                KeyUsage = CngKeyUsages.Signing,
                KeyCreationOptions = CngKeyCreationOptions.None
            });
        using var ecdsa = new ECDsaCng(key);
        var publicKey = ecdsa.ExportSubjectPublicKeyInfo();
        var created = new NavBRIdentityData(
            CompanyNetworkSignatures.BuildPlayerId(publicKey),
            NormalizeDisplayName(preferredDisplayName),
            keyName,
            Convert.ToBase64String(publicKey),
            DateTimeOffset.UtcNow);
        Persist(created);
        return created;
    }

    private static ECDsaCng OpenKey(string keyName) => new(CngKey.Open(keyName));

    private static NavBRPublicIdentity ToPublic(NavBRIdentityData data) => new(
        data.PlayerId,
        data.DisplayName,
        data.PublicKeySpkiBase64,
        data.CreatedAtUtc);

    private static string NormalizeDisplayName(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            text = Environment.UserName?.Trim();
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            text = "Motorista";
        }
        return text.Length <= 40 ? text : text[..40];
    }

    private static void Persist(NavBRIdentityData data)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, overwrite: true);
    }
}
