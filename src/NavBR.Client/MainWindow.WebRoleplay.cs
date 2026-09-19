using NavBR.Client.Multiplayer;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client;

public partial class MainWindow
{
    private string? _webRoleplayStatus;
    private string? _webRoleplayLastInteractionName;
    private bool? _webRoleplayLastInteractionSucceeded;
    private string? _webRoleplayLastInteractionStatus;

    private object BuildWebRoleplayState()
    {
        var mapKey = GetRoleplayMapKeyForShell();
        IReadOnlyList<RoleplayCharacterOption> options = IsRoleplayMapReadyForShell()
            ? GetRoleplayCharacterOptionsForShell()
            : Array.Empty<RoleplayCharacterOption>();
        var controller = GetRoleplayControllerForShell();
        var selected = RoleplayCharacterSelectionStore.Get(mapKey);

        // The current RP backend safely detaches the human who is already
        // driving the player's bus. Other Map.Drivers entries are catalog
        // choices only until independent human spawning exists.
        if (!controller.IsActive &&
            !string.IsNullOrWhiteSpace(mapKey) &&
            options.FirstOrDefault(option => option.IsActiveDriver) is { } activeDriver &&
            !string.Equals(
                selected?.Id,
                activeDriver.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            RoleplayCharacterSelectionStore.Set(mapKey, activeDriver);
            selected = activeDriver;
            _webRoleplayStatus = "roleplay-active-driver-auto-selected";
        }
        var current = controller.CurrentState;
        var nativeAnimation = controller.CurrentNativeAnimationDiagnostics;
        var nativeActivityObservation =
            controller.CurrentNativeActivityObservation;
        var busDistanceMeters = controller.GetBusDistanceMeters();
        var interactions = GetRoleplayVehicleInteractionsForShell();
        var lastInteraction =
            !string.IsNullOrWhiteSpace(_webRoleplayLastInteractionName) &&
            _webRoleplayLastInteractionSucceeded.HasValue
                ? new
                {
                    name = _webRoleplayLastInteractionName,
                    succeeded = _webRoleplayLastInteractionSucceeded.Value,
                    status = _webRoleplayLastInteractionStatus
                }
                : null;
        var canInteractWithBus =
            controller.IsActive &&
            controller.IsInteractionRuntimeAvailable &&
            busDistanceMeters is double interactionDistance &&
            interactionDistance <= controller.InteractionRangeMeters &&
            interactions.Count > 0;

        return new
        {
            enabled = ExperimentalFeatureFlags.RoleplayCharacterEnabled,
            mapReady = IsRoleplayMapReadyForShell(),
            mapKey,
            runtimeAvailable = controller.IsRuntimeAvailable,
            active = controller.IsActive,
            terrainFollowing = controller.IsGroundFollowing,
            nativeAnimation = nativeAnimation is null
                ? null
                : new
                {
                    aiMode = nativeAnimation.AiMode,
                    aiModeEx = nativeAnimation.AiModeEx,
                    aiSubMode = nativeAnimation.AiSubMode,
                    sollSpeedMps = nativeAnimation.SollSpeedMps,
                    actSpeedMps = nativeAnimation.ActSpeedMps,
                    lastMovedDistanceMeters =
                        nativeAnimation.LastMovedDistanceMeters,
                    animationState = nativeAnimation.AnimationState,
                    activityLegRaw = nativeAnimation.ActivityLegRaw,
                    activityArmUmbrellaRaw =
                        nativeAnimation.ActivityArmUmbrellaRaw,
                    activityArmKiRaw = nativeAnimation.ActivityArmKiRaw,
                    activityHeadKiRaw = nativeAnimation.ActivityHeadKiRaw,
                    legacyFieldsDrivenByNavBr = true
                },
            nativeActivityObservation = nativeActivityObservation is null
                ? null
                : new
                {
                    samples = nativeActivityObservation.Samples,
                    movingSamples = nativeActivityObservation.MovingSamples,
                    transitionCount =
                        nativeActivityObservation.TransitionCount,
                    movingTransitionCount =
                        nativeActivityObservation.MovingTransitionCount,
                    changedThisFrame =
                        nativeActivityObservation.ChangedThisFrame,
                    lastTransitionAtUtc =
                        nativeActivityObservation.LastTransitionAtUtc
                },
            busDistanceMeters,
            enterBusRangeMeters = controller.EnterBusRangeMeters,
            canEnterBus = controller.IsActive &&
                          busDistanceMeters is double distance &&
                          distance <= controller.EnterBusRangeMeters,
            interactionRuntimeAvailable = controller.IsInteractionRuntimeAvailable,
            interactionRangeMeters = controller.InteractionRangeMeters,
            canInteractWithBus,
            interactions = interactions
                .Select(name => new { name })
                .ToArray(),
            lastInteraction,
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
            ResetWebRoleplayInteractionFeedback();
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

        var options = GetRoleplayCharacterOptionsForShell();
        var option = options.FirstOrDefault(item =>
            string.Equals(item.Id, characterId, StringComparison.OrdinalIgnoreCase));
        if (option is null)
        {
            throw new InvalidOperationException("O personagem selecionado não está mais disponível no mapa atual.");
        }

        if (!option.IsActiveDriver)
        {
            var activeDriver = options.FirstOrDefault(item => item.IsActiveDriver);
            if (activeDriver is null)
            {
                _webRoleplayStatus = "roleplay-active-driver-not-detected";
                throw new InvalidOperationException(
                    "O OMSI ainda não informou qual humano está dirigindo o ônibus atual.");
            }

            option = activeDriver;
            _webRoleplayStatus = "roleplay-active-driver-auto-selected";
        }

        RoleplayCharacterSelectionStore.Set(mapKey, option);
        ResetWebRoleplayInteractionFeedback();
        _webRoleplayStatus = "roleplay-character-selected";
        _multiplayerWindow?.SetLocalRoleplayCharacterState(null);
        UpdateHudRoleplayStateForShell();
    }

    private async Task StartRoleplayFromWebAsync()
    {
        ResetWebRoleplayInteractionFeedback();

        // Clicking "Sair do ônibus" is an explicit local request to enter RP.
        // Arm the experimental write flag here as well so the button cannot be
        // a no-op merely because the separate toggle was still off.
        if (!ExperimentalFeatureFlags.RoleplayCharacterEnabled)
        {
            var settings = MultiplayerSettingsStore.Load();
            MultiplayerSettingsStore.Save(settings with
            {
                ExperimentalRoleplayCharacterEnabled = true
            });
            ExperimentalFeatureFlags.SetRoleplayCharacterEnabled(true);
            _webRoleplayStatus = "roleplay-enabled";
        }

        var mapKey = GetRoleplayMapKeyForShell();
        var options = IsRoleplayMapReadyForShell()
            ? GetRoleplayCharacterOptionsForShell()
            : Array.Empty<RoleplayCharacterOption>();
        var activeDriver = options.FirstOrDefault(option => option.IsActiveDriver)
            ?? new RoleplayCharacterOption(
                "active-driver:auto",
                "Motorista atual",
                "OMSI live driver",
                DefinitionPointer: 0,
                IsActiveDriver: true);

        if (!string.IsNullOrWhiteSpace(mapKey))
        {
            RoleplayCharacterSelectionStore.Set(mapKey, activeDriver);
        }

        var controller = GetRoleplayControllerForShell();
        var started = await controller.StartAsync();
        if (!started && string.IsNullOrWhiteSpace(_webRoleplayStatus))
        {
            _webRoleplayStatus = "roleplay-start-failed";
        }

        UpdateHudRoleplayStateForShell();
    }

    private IReadOnlyList<string> GetRoleplayVehicleInteractionsForShell()
    {
        if (_roleplayCharacterController?.IsActive != true)
        {
            return Array.Empty<string>();
        }

        return OmsiVehicleInteractionCatalog.Read(
            _currentOmsi?.InstallDirectory,
            _lastTelemetry?.VehiclePath);
    }

    private async Task TriggerRoleplayVehicleFromWebAsync(
        string? triggerName)
    {
        triggerName = triggerName?.Trim();
        if (string.IsNullOrWhiteSpace(triggerName))
        {
            _webRoleplayStatus = "roleplay-interaction-invalid";
            return;
        }

        var interactions = GetRoleplayVehicleInteractionsForShell();
        if (!interactions.Contains(triggerName, StringComparer.Ordinal))
        {
            _webRoleplayStatus = "roleplay-interaction-not-in-catalog";
            return;
        }

        if (_roleplayCharacterController is not { } controller)
        {
            _webRoleplayStatus = "roleplay-interaction-unavailable";
            return;
        }

        _webRoleplayLastInteractionName = triggerName;
        _webRoleplayLastInteractionSucceeded = null;
        _webRoleplayLastInteractionStatus = null;

        var succeeded = await controller.TryTriggerBusInteractionAsync(triggerName);
        _webRoleplayLastInteractionSucceeded = succeeded;
        _webRoleplayLastInteractionStatus = _webRoleplayStatus;
        UpdateHudRoleplayStateForShell();
    }

    private void ResetWebRoleplayInteractionFeedback()
    {
        _webRoleplayLastInteractionName = null;
        _webRoleplayLastInteractionSucceeded = null;
        _webRoleplayLastInteractionStatus = null;
    }

    private async Task EnterRoleplayBusFromWebAsync()
    {
        if (_roleplayCharacterController is not { } controller)
        {
            _webRoleplayStatus = "roleplay-bus-position-unavailable";
            return;
        }

        _ = await controller.TryEnterBusAsync();
        UpdateHudRoleplayStateForShell();
    }

    private async Task StopRoleplayFromWebAsync()
    {
        if (_roleplayCharacterController is { } controller)
        {
            await controller.StopAsync("roleplay-emergency-return");
        }

        _webRoleplayStatus = "roleplay-emergency-return";
        UpdateHudRoleplayStateForShell();
    }
}
