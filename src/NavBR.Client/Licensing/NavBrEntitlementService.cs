namespace NavBR.Client.Licensing;

/// <summary>
/// Commercial entitlement seam for future Steam/direct-store integration.
/// Alpha builds intentionally remain open and do not block startup or multiplayer.
/// Store-specific verification must be implemented behind this service instead
/// of being mixed into multiplayer, telemetry or OMSI plugin code.
/// </summary>
public sealed class NavBrEntitlementService
{
    public const string ProductId = "omsi-navbr-multiplayer";

    public NavBrEntitlement Current { get; private set; } = CreateAlphaEntitlement();

    public event Action<NavBrEntitlement>? EntitlementChanged;

    public Task<NavBrEntitlement> RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Alpha/Beta policy: open access. Steam, direct license-key and other
        // providers will plug in here later without changing online session code.
        SetCurrent(CreateAlphaEntitlement());
        return Task.FromResult(Current);
    }

    internal void ApplyVerifiedEntitlement(NavBrEntitlement entitlement)
    {
        ArgumentNullException.ThrowIfNull(entitlement);
        SetCurrent(entitlement);
    }

    private void SetCurrent(NavBrEntitlement entitlement)
    {
        if (Equals(Current, entitlement))
        {
            return;
        }

        Current = entitlement;
        EntitlementChanged?.Invoke(entitlement);
    }

    private static NavBrEntitlement CreateAlphaEntitlement() =>
        new(
            IsEntitled: true,
            Source: NavBrEntitlementSource.AlphaOpen,
            ProductId: ProductId,
            Detail: "Alpha/Beta open access");
}
