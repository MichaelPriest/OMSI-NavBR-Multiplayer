namespace NavBR.Shared.OpenOmsi;

/// <summary>
/// Advertises the openOMSI LAN v6 transport owned by a NavBR room member.
/// SignalR remains the NavBR service sidecar; movement uses this endpoint.
/// </summary>
public sealed record OpenOmsiTransportDescriptor(
    byte Protocol,
    string Host,
    int Port,
    string SessionId)
{
    public bool IsValid =>
        Protocol == OpenOmsiLanProtocol.ProtocolVersion &&
        !string.IsNullOrWhiteSpace(Host) &&
        Port is >= 1 and <= 65535 &&
        !string.IsNullOrWhiteSpace(SessionId);
}
