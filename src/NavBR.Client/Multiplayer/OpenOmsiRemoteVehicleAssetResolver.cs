using System.Collections.Concurrent;

namespace NavBR.Client.Multiplayer;

/// <summary>
/// Resolves a remote NavBR vehicle against every content root visible to the
/// local openOMSI runtime. Each root delegates to the same SHA-256 resolver
/// used by the OMSI 2 physical backend, so cross-runtime spawning never falls
/// back to a path-only identity.
/// </summary>
internal sealed class OpenOmsiRemoteVehicleAssetResolver
{
    private readonly Func<IReadOnlyList<string>> _contentRootsSource;
    private readonly ConcurrentDictionary<string, OmsiVehicleAssetResolver>
        _resolverByRoot = new(StringComparer.OrdinalIgnoreCase);

    public OpenOmsiRemoteVehicleAssetResolver(
        Func<IReadOnlyList<string>>? contentRootsSource)
    {
        _contentRootsSource =
            contentRootsSource ??
            (() => Array.Empty<string>());
    }

    public async Task<string?> ResolveAsync(
        string? reportedPath,
        string? compatibilityId,
        CancellationToken cancellationToken = default)
    {
        var roots = ReadRoots();
        if (roots.Length == 0 ||
            string.IsNullOrWhiteSpace(compatibilityId))
        {
            return null;
        }

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resolver = _resolverByRoot.GetOrAdd(
                root,
                static normalizedRoot =>
                    new OmsiVehicleAssetResolver(
                        () => normalizedRoot));

            var resolved = await resolver.ResolveAsync(
                reportedPath,
                compatibilityId,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    private string[] ReadRoots()
    {
        IReadOnlyList<string>? values;
        try
        {
            values = _contentRootsSource();
        }
        catch
        {
            return [];
        }

        if (values is null || values.Count == 0)
        {
            return [];
        }

        var roots = new List<string>(values.Count);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            try
            {
                var root = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(value));
                if (!Directory.Exists(root) ||
                    roots.Contains(
                        root,
                        StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                roots.Add(root);
            }
            catch
            {
            }
        }

        return roots.ToArray();
    }
}
