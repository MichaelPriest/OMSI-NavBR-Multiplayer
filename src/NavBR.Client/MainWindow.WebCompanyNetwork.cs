using System.Windows;
using NavBR.Client.Network;
using NavBR.Shared.Network;

namespace NavBR.Client;

public partial class MainWindow
{
    private CompanyNodeSnapshot? _webCompanyNetworkSnapshot;
    private string? _webCompanyInviteCode;
    private string? _webCompanyInvitePayload;

    private object BuildWebCompanyNetworkState()
    {
        if (Application.Current is not App app)
        {
            return new
            {
                available = false,
                identity = (object?)null,
                membership = (object?)null,
                node = (object?)null,
                company = (object?)null,
                assignableRoles = Array.Empty<string>(),
                invite = (object?)null
            };
        }

        var runtime = app.NetworkRuntime;
        var identity = runtime.Identity;
        var membership = runtime.Membership;
        var hostedCompany = CompanyNodeStore.LoadCompany();
        var company = _webCompanyNetworkSnapshot ?? hostedCompany;
        var self = company?.Members.FirstOrDefault(member =>
            string.Equals(member.PlayerId, identity.PlayerId, StringComparison.OrdinalIgnoreCase));

        var canInvite = self is not null &&
                        (self.Permissions & CompanyPermission.InviteMembers) != 0;
        var canManageRoles = self is not null &&
                             (self.Permissions & CompanyPermission.ManageRoles) != 0;
        var canRemoveMembers = self is not null &&
                               (self.Permissions & CompanyPermission.RemoveMembers) != 0;

        var assignableRoles = self is null
            ? Array.Empty<string>()
            : Enum.GetValues<CompanyRole>()
                .Where(role => role != CompanyRole.President && self.Role < role)
                .Select(role => role.ToString())
                .ToArray();

        IReadOnlyList<object> members = company is null
            ? Array.Empty<object>()
            : company.Members
                .OrderBy(member => member.Role)
                .ThenBy(member => member.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(member =>
                {
                    var isOwner = string.Equals(
                        company.OwnerPlayerId,
                        member.PlayerId,
                        StringComparison.OrdinalIgnoreCase);
                    var belowActor = self is not null && self.Role < member.Role;

                    return (object)new
                    {
                        playerId = member.PlayerId,
                        displayName = member.DisplayName,
                        role = member.Role.ToString(),
                        permissions = member.Permissions.ToString(),
                        joinedAtUtc = member.JoinedAtUtc,
                        lastSeenAtUtc = member.LastSeenAtUtc,
                        isSelf = string.Equals(
                            identity.PlayerId,
                            member.PlayerId,
                            StringComparison.OrdinalIgnoreCase),
                        isOwner,
                        canChangeRole = canManageRoles && !isOwner && belowActor,
                        canRemove = canRemoveMembers && !isOwner && belowActor
                    };
                })
                .ToArray();

        return new
        {
            available = true,
            identity = new
            {
                playerId = identity.PlayerId,
                displayName = identity.DisplayName,
                createdAtUtc = identity.CreatedAtUtc
            },
            membership = membership is null
                ? null
                : new
                {
                    companyId = membership.CompanyId,
                    companyName = membership.CompanyName,
                    nodeUrl = membership.NodeUrl,
                    role = membership.Role.ToString(),
                    joinedAtUtc = membership.JoinedAtUtc
                },
            node = new
            {
                running = runtime.CompanyNode.IsRunning,
                port = runtime.CompanyNode.Port,
                localUrl = runtime.CompanyNode.LocalUrl,
                lanUrls = runtime.CompanyNode.IsRunning
                    ? runtime.CompanyNode.GetLanUrls()
                    : Array.Empty<string>()
            },
            company = company is null
                ? null
                : new
                {
                    companyId = company.CompanyId,
                    name = company.Name,
                    shortName = company.ShortName,
                    ownerPlayerId = company.OwnerPlayerId,
                    createdAtUtc = company.CreatedAtUtc,
                    updatedAtUtc = company.UpdatedAtUtc,
                    memberCount = company.Members.Count,
                    selfRole = self?.Role.ToString(),
                    canInvite,
                    canManageRoles,
                    canRemoveMembers,
                    members
                },
            assignableRoles,
            invite = string.IsNullOrWhiteSpace(_webCompanyInviteCode)
                ? null
                : new
                {
                    code = _webCompanyInviteCode,
                    payload = _webCompanyInvitePayload
                }
        };
    }

    private async Task RefreshWebCompanyNetworkAsync()
    {
        if (Application.Current is not App app)
        {
            return;
        }

        var runtime = app.NetworkRuntime;
        if (runtime.CompanyNode.IsRunning || runtime.Membership is not null)
        {
            _webCompanyNetworkSnapshot = await runtime.GetCompanySnapshotAsync();
            return;
        }

        _webCompanyNetworkSnapshot = CompanyNodeStore.LoadCompany();
    }

    private async Task StartWebCompanyNodeAsync()
    {
        if (Application.Current is not App app)
        {
            return;
        }

        _webCompanyNetworkSnapshot = await app.NetworkRuntime.StartCompanyNodeAsync();
    }

    private async Task StopWebCompanyNodeAsync()
    {
        if (Application.Current is not App app)
        {
            return;
        }

        await app.NetworkRuntime.CompanyNode.StopAsync();
        _webCompanyNetworkSnapshot = CompanyNodeStore.LoadCompany();
    }

    private void CreateWebCompanyInvite(string? roleText)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        var runtime = app.NetworkRuntime;
        if (!runtime.CompanyNode.IsRunning)
        {
            throw new InvalidOperationException(
                "Hospede o Company Node neste PC antes de criar um convite.");
        }

        var company = CompanyNodeStore.LoadCompany()
            ?? throw new InvalidOperationException("A Empresa Online não está configurada.");
        var self = company.Members.FirstOrDefault(member =>
            string.Equals(member.PlayerId, runtime.Identity.PlayerId, StringComparison.OrdinalIgnoreCase))
            ?? throw new UnauthorizedAccessException("Sua identidade não pertence a esta empresa.");

        var role = ParseCompanyRole(roleText, CompanyRole.Driver);
        if (role == CompanyRole.President || self.Role >= role)
        {
            throw new InvalidOperationException(
                "O cargo do convite deve estar abaixo do seu cargo atual.");
        }

        var code = CompanyNodeStore.CreateInvite(
            runtime.Identity.PlayerId,
            role,
            TimeSpan.FromDays(7),
            uses: 1);
        var address = runtime.CompanyNode.GetLanUrls().FirstOrDefault()
                      ?? runtime.CompanyNode.LocalUrl;

        _webCompanyInviteCode = code;
        _webCompanyInvitePayload =
            $"NAVBR_COMPANY_INVITE_V1\nserver={address}\ncompany={company.CompanyId}\ncode={code}";
        _webCompanyNetworkSnapshot = company;
    }

    private async Task JoinWebCompanyAsync(string? nodeUrl, string? inviteCode)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nodeUrl) || string.IsNullOrWhiteSpace(inviteCode))
        {
            throw new InvalidOperationException(
                "Informe o endereço do Company Node e o código do convite.");
        }

        var result = await app.NetworkRuntime.JoinAsync(nodeUrl, inviteCode);
        if (!result.Success || result.Company is null)
        {
            throw new InvalidOperationException(
                $"Entrada recusada: {result.Error ?? "erro desconhecido"}.");
        }

        _webCompanyNetworkSnapshot = result.Company;
        _webCompanyInviteCode = null;
        _webCompanyInvitePayload = null;
    }

    private async Task ChangeWebCompanyMemberRoleAsync(
        string? playerId,
        string? roleText)
    {
        if (Application.Current is not App app ||
            string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        var role = ParseCompanyRole(roleText, CompanyRole.Driver);
        if (role == CompanyRole.President)
        {
            throw new InvalidOperationException("O cargo Presidente é reservado.");
        }

        var result = await app.NetworkRuntime.ChangeCompanyMemberRoleAsync(
            playerId.Trim(),
            role);
        if (!result.Success || result.Company is null)
        {
            throw new InvalidOperationException(
                $"Alteração recusada: {result.Error ?? "erro desconhecido"}.");
        }

        _webCompanyNetworkSnapshot = result.Company;
    }

    private async Task RemoveWebCompanyMemberAsync(string? playerId)
    {
        if (Application.Current is not App app ||
            string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        var result = await app.NetworkRuntime.RemoveCompanyMemberAsync(playerId.Trim());
        if (!result.Success || result.Company is null)
        {
            throw new InvalidOperationException(
                $"Remoção recusada: {result.Error ?? "erro desconhecido"}.");
        }

        _webCompanyNetworkSnapshot = result.Company;
    }

    private static CompanyRole ParseCompanyRole(
        string? value,
        CompanyRole fallback) =>
        Enum.TryParse<CompanyRole>(value, ignoreCase: true, out var role)
            ? role
            : fallback;
}
