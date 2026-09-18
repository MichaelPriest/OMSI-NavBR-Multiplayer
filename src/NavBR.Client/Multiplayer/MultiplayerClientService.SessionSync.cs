using Microsoft.AspNetCore.SignalR.Client;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

public sealed partial class MultiplayerClientService
{
    private HubConnection? _sessionSyncHandlerConnection;
    private IDisposable? _sessionSyncSubscription;

    public event Action<SessionOperationalState?>? SessionOperationalStateChanged;

    public SessionOperationalState? CurrentSessionOperationalState { get; private set; }

    public async Task<SessionOperationalState?> RefreshSessionOperationalStateAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = _connection;
        if (connection is null || connection.State != HubConnectionState.Connected)
        {
            SetSessionOperationalState(null);
            return null;
        }

        EnsureSessionSyncHandler(connection);
        var state = await connection.InvokeAsync<SessionOperationalState?>(
            "GetSessionOperationalState",
            cancellationToken);
        SetSessionOperationalState(IsAcceptedSessionState(state) ? state : null);
        return CurrentSessionOperationalState;
    }

    public async Task<SessionOperationalState?> PublishSessionOperationalStateAsync(
        VehicleTelemetry telemetry,
        long sequence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var connection = _connection;
        if (!IsTrafficAuthority ||
            connection is null ||
            connection.State != HubConnectionState.Connected ||
            _joinRequest is null)
        {
            return null;
        }

        EnsureSessionSyncHandler(connection);
        var state = new SessionOperationalState(
            _joinRequest.PlayerId,
            Math.Max(0L, sequence),
            DateTimeOffset.UtcNow,
            telemetry.MapName,
            telemetry.MapCompatibilityId ?? _joinRequest.MapCompatibilityId,
            telemetry.Line,
            telemetry.Route,
            telemetry.DestinationName,
            telemetry.NextStopName);

        var accepted = await connection.InvokeAsync<SessionOperationalState>(
            "PublishSessionOperationalState",
            state,
            cancellationToken);
        SetSessionOperationalState(accepted);
        return accepted;
    }

    private void EnsureSessionSyncHandler(HubConnection connection)
    {
        if (ReferenceEquals(_sessionSyncHandlerConnection, connection))
        {
            return;
        }

        _sessionSyncSubscription?.Dispose();
        _sessionSyncSubscription = connection.On<SessionOperationalState>(
            "sessionOperationalState",
            state =>
            {
                if (IsAcceptedSessionState(state))
                {
                    SetSessionOperationalState(state);
                }
            });
        _sessionSyncHandlerConnection = connection;
    }

    private bool IsAcceptedSessionState(SessionOperationalState? state)
    {
        if (state is null)
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(TrafficAuthorityPlayerId) ||
               string.Equals(
                   state.AuthorityPlayerId,
                   TrafficAuthorityPlayerId,
                   StringComparison.OrdinalIgnoreCase);
    }

    private void SetSessionOperationalState(SessionOperationalState? state)
    {
        if (Equals(CurrentSessionOperationalState, state))
        {
            return;
        }

        CurrentSessionOperationalState = state;
        SessionOperationalStateChanged?.Invoke(state);
    }
}
