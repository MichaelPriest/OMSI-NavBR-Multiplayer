namespace NavBR.Client.Licensing;

public enum NavBrEntitlementSource
{
    AlphaOpen = 0,
    Steam = 1,
    LicenseKey = 2,
    OtherStore = 3
}

public sealed record NavBrEntitlement(
    bool IsEntitled,
    NavBrEntitlementSource Source,
    string ProductId,
    string? AccountId = null,
    DateTimeOffset? ValidUntil = null,
    string? Detail = null)
{
    public bool IsExpired => ValidUntil is { } value && value <= DateTimeOffset.UtcNow;

    public bool AllowsUse => IsEntitled && !IsExpired;
}
