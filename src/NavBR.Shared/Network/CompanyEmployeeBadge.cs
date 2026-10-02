using System.Security.Cryptography;
using System.Text;

namespace NavBR.Shared.Network;

public sealed record CompanyEmployeeBadge(
    string CompanyId,
    string CompanyName,
    string CompanyShortName,
    string PlayerId,
    string DisplayName,
    string EmployeeNumber,
    CompanyRole Role,
    CompanyPermission Permissions,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public static class CompanyEmployeeBadgeFactory
{
    public static CompanyEmployeeBadge? Create(
        CompanyNodeSnapshot? company,
        CompanyMemberRecord? member)
    {
        if (company is null ||
            member is null ||
            string.IsNullOrWhiteSpace(member.EmployeeNumber))
        {
            return null;
        }

        return new CompanyEmployeeBadge(
            company.CompanyId,
            company.Name,
            company.ShortName,
            member.PlayerId,
            member.DisplayName,
            member.EmployeeNumber.Trim(),
            member.Role,
            member.Permissions,
            member.BadgeIssuedAtUtc ?? member.JoinedAtUtc,
            company.UpdatedAtUtc);
    }

    public static bool MatchesMember(
        CompanyEmployeeBadge? badge,
        CompanyNodeSnapshot? company,
        CompanyMemberRecord? member)
    {
        if (badge is null || company is null || member is null)
        {
            return false;
        }

        return string.Equals(badge.CompanyId, company.CompanyId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(badge.PlayerId, member.PlayerId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(badge.EmployeeNumber, member.EmployeeNumber, StringComparison.OrdinalIgnoreCase) &&
               badge.Role == member.Role &&
               badge.Permissions == member.Permissions;
    }
}


public sealed record CompanyBadgePresenceProof(
    string IdentityPublicKeySpkiBase64,
    long TimestampUnixMilliseconds,
    string SignatureBase64);

public static class CompanyBadgePresenceSignatures
{
    public static byte[] BuildPayload(
        string sessionPlayerId,
        CompanyEmployeeBadge badge,
        long timestampUnixMilliseconds)
    {
        var canonical = string.Join('\n',
            "company-badge-presence-v1",
            sessionPlayerId.Trim().ToUpperInvariant(),
            badge.CompanyId.Trim().ToUpperInvariant(),
            badge.PlayerId.Trim().ToUpperInvariant(),
            badge.EmployeeNumber.Trim().ToUpperInvariant(),
            ((int)badge.Role).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ((int)badge.Permissions).ToString(System.Globalization.CultureInfo.InvariantCulture),
            timestampUnixMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Encoding.UTF8.GetBytes(canonical);
    }

    public static bool Verify(
        string sessionPlayerId,
        CompanyEmployeeBadge badge,
        CompanyBadgePresenceProof proof)
    {
        if (string.IsNullOrWhiteSpace(sessionPlayerId) ||
            string.IsNullOrWhiteSpace(badge.PlayerId) ||
            string.IsNullOrWhiteSpace(proof.IdentityPublicKeySpkiBase64) ||
            string.IsNullOrWhiteSpace(proof.SignatureBase64))
        {
            return false;
        }

        try
        {
            var publicKey = Convert.FromBase64String(proof.IdentityPublicKeySpkiBase64);
            var identityPlayerId = CompanyNetworkSignatures.BuildPlayerId(publicKey);
            if (!string.Equals(
                    identityPlayerId,
                    badge.PlayerId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var signature = Convert.FromBase64String(proof.SignatureBase64);
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            return ecdsa.VerifyData(
                BuildPayload(sessionPlayerId, badge, proof.TimestampUnixMilliseconds),
                signature,
                HashAlgorithmName.SHA256);
        }
        catch
        {
            return false;
        }
    }
}
