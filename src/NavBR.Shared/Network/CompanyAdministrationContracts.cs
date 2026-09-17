using System.Security.Cryptography;
using System.Text;

namespace NavBR.Shared.Network;

[Flags]
public enum CompanyPermission
{
    None = 0,
    InviteMembers = 1 << 0,
    RemoveMembers = 1 << 1,
    ManageRoles = 1 << 2,
    ManageFleet = 1 << 3,
    ManageLines = 1 << 4,
    ManageOperations = 1 << 5,
    UseDispatcher = 1 << 6,
    ManageAlliances = 1 << 7,
    ManageCompany = 1 << 8,
    HostCompanyNode = 1 << 9,
    All = InviteMembers | RemoveMembers | ManageRoles | ManageFleet | ManageLines |
          ManageOperations | UseDispatcher | ManageAlliances | ManageCompany | HostCompanyNode
}

public enum CompanyRole
{
    President = 0,
    VicePresident = 1,
    Director = 2,
    OperationsManager = 3,
    Dispatcher = 4,
    Supervisor = 5,
    SeniorDriver = 6,
    Driver = 7,
    Trainee = 8
}

public static class CompanyRolePolicy
{
    public static CompanyPermission DefaultPermissions(CompanyRole role) => role switch
    {
        CompanyRole.President => CompanyPermission.All,
        CompanyRole.VicePresident => CompanyPermission.All,
        CompanyRole.Director => CompanyPermission.InviteMembers |
                                CompanyPermission.RemoveMembers |
                                CompanyPermission.ManageRoles |
                                CompanyPermission.ManageFleet |
                                CompanyPermission.ManageLines |
                                CompanyPermission.ManageOperations |
                                CompanyPermission.UseDispatcher |
                                CompanyPermission.ManageAlliances |
                                CompanyPermission.HostCompanyNode,
        CompanyRole.OperationsManager => CompanyPermission.InviteMembers |
                                         CompanyPermission.ManageFleet |
                                         CompanyPermission.ManageLines |
                                         CompanyPermission.ManageOperations |
                                         CompanyPermission.UseDispatcher,
        CompanyRole.Dispatcher => CompanyPermission.ManageOperations |
                                  CompanyPermission.UseDispatcher,
        CompanyRole.Supervisor => CompanyPermission.ManageOperations |
                                  CompanyPermission.UseDispatcher,
        CompanyRole.SeniorDriver => CompanyPermission.ManageOperations,
        _ => CompanyPermission.None
    };
}

public sealed record NavBRPublicIdentity(
    string PlayerId,
    string DisplayName,
    string PublicKeySpkiBase64,
    DateTimeOffset CreatedAtUtc);

public sealed record CompanyMemberRecord(
    string PlayerId,
    string DisplayName,
    string PublicKeySpkiBase64,
    CompanyRole Role,
    CompanyPermission Permissions,
    DateTimeOffset JoinedAtUtc,
    DateTimeOffset LastSeenAtUtc);

public sealed record CompanyNodeSnapshot(
    int SchemaVersion,
    string CompanyId,
    string Name,
    string ShortName,
    string OwnerPlayerId,
    IReadOnlyList<CompanyMemberRecord> Members,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CompanyJoinRequest(
    string CompanyId,
    string InviteCode,
    NavBRPublicIdentity Identity,
    long TimestampUnixMilliseconds,
    string SignatureBase64);

public sealed record CompanyJoinResponse(
    bool Success,
    string? Error,
    CompanyNodeSnapshot? Company);

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

public static class CompanyNetworkSignatures
{
    public static byte[] BuildJoinPayload(
        string companyId,
        string inviteCode,
        NavBRPublicIdentity identity,
        long timestampUnixMilliseconds)
    {
        var canonical = string.Join('\n',
            companyId.Trim(),
            inviteCode.Trim().ToUpperInvariant(),
            identity.PlayerId.Trim().ToUpperInvariant(),
            identity.DisplayName.Trim(),
            identity.PublicKeySpkiBase64.Trim(),
            timestampUnixMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Encoding.UTF8.GetBytes(canonical);
    }

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

    public static bool VerifyJoinRequest(CompanyJoinRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Identity.PlayerId) ||
            string.IsNullOrWhiteSpace(request.Identity.PublicKeySpkiBase64) ||
            string.IsNullOrWhiteSpace(request.SignatureBase64))
        {
            return false;
        }

        return VerifyIdentitySignature(
            request.Identity,
            BuildJoinPayload(
                request.CompanyId,
                request.InviteCode,
                request.Identity,
                request.TimestampUnixMilliseconds),
            request.SignatureBase64);
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

    public static string BuildPlayerId(ReadOnlySpan<byte> publicKey)
    {
        var hash = SHA256.HashData(publicKey);
        var hex = Convert.ToHexString(hash.AsSpan(0, 10));
        return $"NB-{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}";
    }

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
            var expectedPlayerId = BuildPlayerId(publicKey);
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
