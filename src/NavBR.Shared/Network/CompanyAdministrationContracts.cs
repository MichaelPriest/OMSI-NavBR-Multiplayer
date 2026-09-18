using System.Security.Cryptography;
using System.Text;

namespace NavBR.Shared.Network;

public sealed record CompanyRoleChangeRequest(
    string CompanyId,
    NavBRPublicIdentity ActorIdentity,
    string TargetPlayerId,
    CompanyRole NewRole,
    long TimestampUnixMilliseconds,
    string SignatureBase64);

public sealed record CompanyMemberRemoveRequest(
    string CompanyId,
    NavBRPublicIdentity ActorIdentity,
    string TargetPlayerId,
    long TimestampUnixMilliseconds,
    string SignatureBase64);

public sealed record CompanyMemberActionResponse(
    bool Success,
    string? Error,
    CompanyNodeSnapshot? Company);

public static class CompanyAdministrationSignatures
{
    public static byte[] BuildRoleChangePayload(
        string companyId,
        NavBRPublicIdentity actorIdentity,
        string targetPlayerId,
        CompanyRole newRole,
        long timestampUnixMilliseconds)
    {
        var canonical = string.Join('\n',
            "role-change-v1",
            companyId.Trim().ToUpperInvariant(),
            actorIdentity.PlayerId.Trim().ToUpperInvariant(),
            targetPlayerId.Trim().ToUpperInvariant(),
            ((int)newRole).ToString(System.Globalization.CultureInfo.InvariantCulture),
            timestampUnixMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Encoding.UTF8.GetBytes(canonical);
    }

    public static byte[] BuildMemberRemovePayload(
        string companyId,
        NavBRPublicIdentity actorIdentity,
        string targetPlayerId,
        long timestampUnixMilliseconds)
    {
        var canonical = string.Join('\n',
            "member-remove-v1",
            companyId.Trim().ToUpperInvariant(),
            actorIdentity.PlayerId.Trim().ToUpperInvariant(),
            targetPlayerId.Trim().ToUpperInvariant(),
            timestampUnixMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Encoding.UTF8.GetBytes(canonical);
    }

    public static bool VerifyRoleChangeRequest(CompanyRoleChangeRequest request) =>
        VerifyIdentitySignature(
            request.ActorIdentity,
            BuildRoleChangePayload(
                request.CompanyId,
                request.ActorIdentity,
                request.TargetPlayerId,
                request.NewRole,
                request.TimestampUnixMilliseconds),
            request.SignatureBase64);

    public static bool VerifyMemberRemoveRequest(CompanyMemberRemoveRequest request) =>
        VerifyIdentitySignature(
            request.ActorIdentity,
            BuildMemberRemovePayload(
                request.CompanyId,
                request.ActorIdentity,
                request.TargetPlayerId,
                request.TimestampUnixMilliseconds),
            request.SignatureBase64);

    private static bool VerifyIdentitySignature(
        NavBRPublicIdentity identity,
        byte[] payload,
        string signatureBase64)
    {
        if (string.IsNullOrWhiteSpace(identity.PlayerId) ||
            string.IsNullOrWhiteSpace(identity.PublicKeySpkiBase64) ||
            string.IsNullOrWhiteSpace(signatureBase64))
        {
            return false;
        }

        try
        {
            var publicKey = Convert.FromBase64String(identity.PublicKeySpkiBase64);
            var expectedPlayerId = CompanyNetworkSignatures.BuildPlayerId(publicKey);
            if (!string.Equals(expectedPlayerId, identity.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var signature = Convert.FromBase64String(signatureBase64);
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            return ecdsa.VerifyData(payload, signature, HashAlgorithmName.SHA256);
        }
        catch
        {
            return false;
        }
    }
}
