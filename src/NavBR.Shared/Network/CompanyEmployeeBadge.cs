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
