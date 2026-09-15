namespace NavBR.Shared.Multiplayer;

public static class OmsiCompatibilityEvaluator
{
    public static RoomCompatibilityReport Compare(
        OmsiCompatibilityManifest? local,
        OmsiCompatibilityManifest? remote,
        bool requireVehicleForPhysicalMultiplayer = false)
    {
        var issues = new List<CompatibilityIssue>();

        if (local is null || remote is null)
        {
            issues.Add(new CompatibilityIssue(
                "manifest-missing",
                requireVehicleForPhysicalMultiplayer
                    ? CompatibilityIssueSeverity.Blocking
                    : CompatibilityIssueSeverity.Warning,
                "Compatibility manifest is unavailable for one of the players."));
            return new RoomCompatibilityReport(!requireVehicleForPhysicalMultiplayer, issues);
        }

        CompareRequiredFingerprint(
            issues,
            "map",
            local.MapCompatibilityId,
            remote.MapCompatibilityId,
            "Map compatibility differs between players.",
            blocking: true,
            required: true);

        if (!string.Equals(local.OmsiVersion, remote.OmsiVersion, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new CompatibilityIssue(
                "omsi-version",
                CompatibilityIssueSeverity.Warning,
                "OMSI versions differ. The session may work, but deep integration must be treated as experimental.",
                local.OmsiVersion,
                remote.OmsiVersion));
        }

        if (local.PluginProtocolVersion != remote.PluginProtocolVersion)
        {
            issues.Add(new CompatibilityIssue(
                "plugin-protocol",
                CompatibilityIssueSeverity.Blocking,
                "NavBR plugin protocol versions are incompatible.",
                local.PluginProtocolVersion.ToString(),
                remote.PluginProtocolVersion.ToString()));
        }

        if (requireVehicleForPhysicalMultiplayer &&
            (string.IsNullOrWhiteSpace(remote.VehiclePath) ||
             string.IsNullOrWhiteSpace(remote.VehicleCompatibilityId)))
        {
            issues.Add(new CompatibilityIssue(
                "vehicle-identity-missing",
                CompatibilityIssueSeverity.Blocking,
                "Remote vehicle identity is required before a physical bus can be created in OMSI.",
                local.VehiclePath,
                remote.VehiclePath));
        }

        CompareRequiredFingerprint(
            issues,
            "vehicle",
            local.VehicleCompatibilityId,
            remote.VehicleCompatibilityId,
            "Vehicle definitions differ. Physical remote-bus rendering may not match.",
            blocking: requireVehicleForPhysicalMultiplayer,
            required: requireVehicleForPhysicalMultiplayer);

        CompareRequiredFingerprint(
            issues,
            "hof",
            local.HofCompatibilityId,
            remote.HofCompatibilityId,
            "HOF definitions differ. Line/destination display may not match.",
            blocking: false,
            required: false);

        if (!string.IsNullOrWhiteSpace(local.MapName) &&
            !string.IsNullOrWhiteSpace(remote.MapName) &&
            !string.Equals(local.MapName, remote.MapName, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new CompatibilityIssue(
                "map-name",
                CompatibilityIssueSeverity.Warning,
                "Map names differ even though fingerprint comparison may be unavailable.",
                local.MapName,
                remote.MapName));
        }

        var localCaps = new HashSet<string>(
            local.Capabilities ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        var remoteCaps = new HashSet<string>(
            remote.Capabilities ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var capability in localCaps.Where(cap => !remoteCaps.Contains(cap)).OrderBy(cap => cap))
        {
            issues.Add(new CompatibilityIssue(
                $"capability:{capability}",
                CompatibilityIssueSeverity.Info,
                $"Remote player does not advertise capability '{capability}'."));
        }

        return new RoomCompatibilityReport(
            !issues.Any(issue => issue.Severity == CompatibilityIssueSeverity.Blocking),
            issues);
    }

    private static void CompareRequiredFingerprint(
        ICollection<CompatibilityIssue> issues,
        string code,
        string? local,
        string? remote,
        string message,
        bool blocking,
        bool required)
    {
        if (string.IsNullOrWhiteSpace(local) || string.IsNullOrWhiteSpace(remote))
        {
            issues.Add(new CompatibilityIssue(
                $"{code}-unknown",
                required
                    ? CompatibilityIssueSeverity.Blocking
                    : CompatibilityIssueSeverity.Info,
                $"{code.ToUpperInvariant()} fingerprint is unavailable for one of the players.",
                local,
                remote));
            return;
        }

        if (string.Equals(local, remote, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        issues.Add(new CompatibilityIssue(
            $"{code}-mismatch",
            blocking ? CompatibilityIssueSeverity.Blocking : CompatibilityIssueSeverity.Warning,
            message,
            local,
            remote));
    }
}
