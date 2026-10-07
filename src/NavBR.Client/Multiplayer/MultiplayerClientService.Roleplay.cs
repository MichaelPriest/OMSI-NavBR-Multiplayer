using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

public sealed partial class MultiplayerClientService
{
    private readonly HashSet<string> _remoteRoleplayPlayers =
        new(StringComparer.OrdinalIgnoreCase);

    public event Action<RoleplayCharacterFrame>? RoleplayCharacterReceived;
    public event Action<string>? RoleplayCharacterRemoved;

    public Task PublishRoleplayCharacterAsync(
        RoleplayCharacterState character,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        SetOpenOmsiLocalRoleplayState(character);
        return Task.CompletedTask;
    }

    public Task ReleaseRoleplayCharacterAsync(
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        SetOpenOmsiLocalRoleplayState(null);
        return Task.CompletedTask;
    }

    private void ApplyRoleplayCharacter(RoleplayCharacterFrame frame)
    {
        if (string.IsNullOrWhiteSpace(frame.Player.PlayerId) ||
            !frame.Character.IsActive)
        {
            return;
        }

        _remoteRoleplayPlayers.Add(frame.Player.PlayerId);
        RoleplayCharacterReceived?.Invoke(frame);
    }

    private void RemoveRoleplayCharacter(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        _remoteRoleplayPlayers.Remove(playerId);
        RoleplayCharacterRemoved?.Invoke(playerId);
    }

    private void ClearRoleplayCharacters()
    {
        foreach (var playerId in _remoteRoleplayPlayers.ToArray())
        {
            RoleplayCharacterRemoved?.Invoke(playerId);
        }

        _remoteRoleplayPlayers.Clear();
    }
}
