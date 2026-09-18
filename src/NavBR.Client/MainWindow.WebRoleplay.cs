using NavBR.Client.Multiplayer;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client;

public partial class MainWindow
{
    private string? _webRoleplayStatus;

    private object BuildWebRoleplayState()
    {
        var mapKey = GetRoleplayMapKeyForShell();
        var selected = RoleplayCharacterSelectionStore.Get(mapKey);
        IReadOnlyList<RoleplayCharacterOption> options = IsRoleplayMapReadyForShell()
            ? GetRoleplayCharacterOptionsForShell()
            : Array.Empty<RoleplayCharacterOption>();
        var controller = GetRoleplayControllerForShell();
        var current = controller.CurrentState;

        return new
        {
            enabled = ExperimentalFeatureFlags.RoleplayCharacterEnabled,
            mapReady = IsRoleplayMapReadyForShell(),
            mapKey,
            runtimeAvailable = controller.IsRuntimeAvailable,
            active = controller.IsActive,
            terrainFollowing = controller.IsGroundFollowing,
            status = _webRoleplayStatus,
            selected = selected is null
                ? null
                : new
                {
                    id = selected.Id,
                    displayName = selected.DisplayName,
                    sourceValue = selected.SourceValue,
                    isActiveDriver = selected.IsActiveDriver
                },
            characters = options
                .Select(option => new
                {
                    id = option.Id,
                    displayName = option.DisplayName,
                    sourceValue = option.SourceValue,
                    isActiveDriver = option.IsActiveDriver,
                    selected = string.Equals(
                        option.Id,
                        selected?.Id,
                        StringComparison.OrdinalIgnoreCase)
                })
                .ToArray(),
            current = current is null
                ? null
                : new
                {
                    characterId = current.CharacterId,
                    characterName = current.CharacterName,
                    mapName = current.MapName,
                    mapCompatibilityId = current.MapCompatibilityId,
                    localX = current.LocalX,
                    localY = current.LocalY,
                    localZ = current.LocalZ,
                    headingDegrees = current.HeadingDegrees,
                    speedMps = current.SpeedMps,
                    activity = current.Activity.ToString(),
                    isActive = current.IsActive,
                    humanIndex = current.HumanIndex,
                    timestamp = current.Timestamp
                }
        };
    }

    private async Task SetRoleplayEnabledFromWebAsync(bool enabled)
    {
        var settings = MultiplayerSettingsStore.Load();
        MultiplayerSettingsStore.Save(settings with
        {
            ExperimentalRoleplayCharacterEnabled = enabled
        });
        ExperimentalFeatureFlags.SetRoleplayCharacterEnabled(enabled);

        if (!enabled)
        {
            if (_roleplayCharacterController is { IsActive: true } controller)
            {
                await controller.StopAsync("roleplay-disabled");
            }

            RoleplayCharacterSelectionStore.Clear();
            _webRoleplayStatus = "roleplay-disabled";
        }
        else
        {
            _webRoleplayStatus = "roleplay-enabled";
        }

        UpdateHudRoleplayStateForShell();
    }

    private void SelectRoleplayCharacterFromWeb(string? characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            throw new InvalidOperationException("Selecione um personagem válido.");
        }

        var mapKey = GetRoleplayMapKeyForShell();
        if (string.IsNullOrWhiteSpace(mapKey) || !IsRoleplayMapReadyForShell())
        {
            throw new InvalidOperationException("O mapa do OMSI ainda não está pronto para Personagem / RP.");
        }

        if (_roleplayCharacterController?.IsActive == true)
        {
            throw new InvalidOperationException("Retorne ao ônibus antes de trocar de personagem.");
        }

        var option = GetRoleplayCharacterOptionsForShell()
            .FirstOrDefault(item =>
                string.Equals(item.Id, characterId, StringComparison.OrdinalIgnoreCase));
        if (option is null)
        {
            throw new InvalidOperationException("O personagem selecionado não está mais disponível no mapa atual.");
        }

        RoleplayCharacterSelectionStore.Set(mapKey, option);
        _webRoleplayStatus = "roleplay-character-selected";
        _multiplayerWindow?.SetLocalRoleplayCharacterState(null);
        UpdateHudRoleplayStateForShell();
    }

    private async Task StartRoleplayFromWebAsync()
    {
        var controller = GetRoleplayControllerForShell();
        var started = await controller.StartAsync();
        if (!started && string.IsNullOrWhiteSpace(_webRoleplayStatus))
        {
            _webRoleplayStatus = "roleplay-start-failed";
        }

        UpdateHudRoleplayStateForShell();
    }

    private async Task StopRoleplayFromWebAsync()
    {
        if (_roleplayCharacterController is { } controller)
        {
            await controller.StopAsync("roleplay-returned-to-bus");
        }

        _webRoleplayStatus = "roleplay-returned-to-bus";
        UpdateHudRoleplayStateForShell();
    }
}
