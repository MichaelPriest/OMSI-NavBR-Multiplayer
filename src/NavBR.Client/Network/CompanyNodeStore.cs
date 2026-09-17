using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NavBR.Client.Operations;
using NavBR.Shared.Network;

namespace NavBR.Client.Network;

internal sealed record CompanyInviteSecret(
    string Hash,
    CompanyRole Role,
    DateTimeOffset ExpiresAtUtc,
    int RemainingUses);

internal sealed record CompanyNodeState(
    CompanyNodeSnapshot Company,
    IReadOnlyList<CompanyInviteSecret> Invites);

internal static class CompanyNodeStore
{
    private static readonly object Sync = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer",
        "Network");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "company-node.json");
    private static CompanyNodeState? _cached;

    public static CompanyNodeSnapshot? LoadCompany()
    {
        lock (Sync)
        {
            _cached ??= LoadCore();
            return _cached?.Company;
        }
    }

    public static CompanyNodeSnapshot EnsureLocalCompany(
        NavBRPublicIdentity identity,
        VirtualCompanyData localCompany)
    {
        lock (Sync)
        {
            _cached ??= LoadCore();
            if (_cached is not null)
            {
                return _cached.Company;
            }

            if (string.IsNullOrWhiteSpace(localCompany.Name))
            {
                throw new InvalidOperationException(
                    "Crie primeiro a Empresa/Frota local antes de hospedar uma empresa online.");
            }

            var now = DateTimeOffset.UtcNow;
            var president = new CompanyMemberRecord(
                identity.PlayerId,
                identity.DisplayName,
                identity.PublicKeySpkiBase64,
                CompanyRole.President,
                CompanyPermission.All,
                now,
                now);
            var company = new CompanyNodeSnapshot(
                1,
                $"NC-{Guid.NewGuid():N}".ToUpperInvariant(),
                localCompany.Name.Trim(),
                NormalizeShortName(localCompany.ShortName, localCompany.Name),
                identity.PlayerId,
                [president],
                now,
                now);
            _cached = new CompanyNodeState(company, Array.Empty<CompanyInviteSecret>());
            Persist(_cached);
            return company;
        }
    }

    public static string CreateInvite(
        string actorPlayerId,
        CompanyRole role = CompanyRole.Driver,
        TimeSpan? lifetime = null,
        int uses = 1)
    {
        lock (Sync)
        {
            _cached ??= LoadCore() ?? throw new InvalidOperationException("Nenhuma Empresa Online foi criada neste PC.");
            var actor = _cached.Company.Members.FirstOrDefault(member =>
                string.Equals(member.PlayerId, actorPlayerId, StringComparison.OrdinalIgnoreCase));
            if (actor is null ||
                (actor.Permissions & CompanyPermission.InviteMembers) == 0)
            {
                throw new UnauthorizedAccessException("Sua identidade não tem permissão para convidar membros.");
            }

            var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(10));
            var code = $"NBR-{raw[..4]}-{raw[4..8]}-{raw[8..12]}-{raw[12..16]}-{raw[16..20]}";
            var invite = new CompanyInviteSecret(
                HashInvite(code),
                role,
                DateTimeOffset.UtcNow + (lifetime ?? TimeSpan.FromDays(7)),
                Math.Clamp(uses, 1, 50));

            var active = _cached.Invites
                .Where(item => item.ExpiresAtUtc > DateTimeOffset.UtcNow && item.RemainingUses > 0)
                .TakeLast(99)
                .Append(invite)
                .ToArray();
            _cached = _cached with { Invites = active };
            Persist(_cached);
            return code;
        }
    }

    public static CompanyJoinResponse TryJoin(CompanyJoinRequest request)
    {
        lock (Sync)
        {
            _cached ??= LoadCore();
            if (_cached is null)
            {
                return new CompanyJoinResponse(false, "company_not_configured", null);
            }

            if (!string.Equals(
                    request.CompanyId,
                    _cached.Company.CompanyId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new CompanyJoinResponse(false, "company_id_mismatch", null);
            }

            DateTimeOffset requestTime;
            try
            {
                requestTime = DateTimeOffset.FromUnixTimeMilliseconds(request.TimestampUnixMilliseconds);
            }
            catch
            {
                return new CompanyJoinResponse(false, "invalid_timestamp", null);
            }

            if (Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalMinutes) > 5d)
            {
                return new CompanyJoinResponse(false, "request_expired", null);
            }

            if (!CompanyNetworkSignatures.VerifyJoinRequest(request))
            {
                return new CompanyJoinResponse(false, "invalid_signature", null);
            }

            var inviteHash = HashInvite(request.InviteCode);
            var inviteIndex = _cached.Invites
                .Select((invite, index) => (invite, index))
                .FirstOrDefault(pair =>
                    pair.invite.ExpiresAtUtc > DateTimeOffset.UtcNow &&
                    pair.invite.RemainingUses > 0 &&
                    FixedEquals(pair.invite.Hash, inviteHash));
            if (inviteIndex.invite is null)
            {
                return new CompanyJoinResponse(false, "invite_invalid_or_expired", null);
            }

            var now = DateTimeOffset.UtcNow;
            var members = _cached.Company.Members.ToList();
            var existingIndex = members.FindIndex(member =>
                string.Equals(member.PlayerId, request.Identity.PlayerId, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                var previous = members[existingIndex];
                members[existingIndex] = previous with
                {
                    DisplayName = request.Identity.DisplayName.Trim(),
                    PublicKeySpkiBase64 = request.Identity.PublicKeySpkiBase64,
                    LastSeenAtUtc = now
                };
            }
            else
            {
                if (members.Count >= 500)
                {
                    return new CompanyJoinResponse(false, "member_limit_reached", null);
                }

                members.Add(new CompanyMemberRecord(
                    request.Identity.PlayerId,
                    request.Identity.DisplayName.Trim(),
                    request.Identity.PublicKeySpkiBase64,
                    inviteIndex.invite.Role,
                    CompanyRolePolicy.DefaultPermissions(inviteIndex.invite.Role),
                    now,
                    now));
            }

            var invites = _cached.Invites.ToList();
            var consumed = inviteIndex.invite with
            {
                RemainingUses = inviteIndex.invite.RemainingUses - 1
            };
            if (consumed.RemainingUses <= 0)
            {
                invites.RemoveAt(inviteIndex.index);
            }
            else
            {
                invites[inviteIndex.index] = consumed;
            }

            var company = _cached.Company with
            {
                Members = members,
                UpdatedAtUtc = now
            };
            _cached = new CompanyNodeState(company, invites);
            Persist(_cached);
            return new CompanyJoinResponse(true, null, company);
        }
    }

    private static CompanyNodeState? LoadCore()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var parsed = JsonSerializer.Deserialize<CompanyNodeState>(File.ReadAllText(FilePath));
            if (parsed?.Company is null || string.IsNullOrWhiteSpace(parsed.Company.CompanyId))
            {
                return null;
            }
            return parsed;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeShortName(string? shortName, string companyName)
    {
        var value = shortName?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            value = new string(companyName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(4)
                .Select(word => char.ToUpperInvariant(word[0]))
                .ToArray());
        }
        if (string.IsNullOrWhiteSpace(value))
        {
            value = "NAVBR";
        }
        return value.Length <= 12 ? value : value[..12];
    }

    private static string HashInvite(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant())));

    private static bool FixedEquals(string leftHex, string rightHex)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(leftHex),
                Convert.FromHexString(rightHex));
        }
        catch
        {
            return false;
        }
    }

    private static void Persist(CompanyNodeState state)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, overwrite: true);
    }
}
