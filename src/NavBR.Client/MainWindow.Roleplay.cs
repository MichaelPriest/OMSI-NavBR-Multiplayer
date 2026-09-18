using System.Windows;
using System.Windows.Threading;
using NavBR.Client.Multiplayer;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client;

public partial class MainWindow
{
    private RoleplayCharacterController? _roleplayCharacterController;
    private RoleplayCharacterWindow? _roleplayCharacterWindow;
    private DispatcherTimer? _roleplayAutoPromptTimer;
    private string? _roleplayPromptedMapKey;
    private bool _roleplayLifetimeHooked;

    internal void InitializeRoleplayForShell()
    {
        HookRoleplayLifetime();

        if (_roleplayAutoPromptTimer is not null)
        {
            return;
        }

        _roleplayAutoPromptTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1d)
        };
        _roleplayAutoPromptTimer.Tick += (_, _) => TryPromptRoleplayCharacter();
        _roleplayAutoPromptTimer.Start();
    }

    private void TryPromptRoleplayCharacter()
    {
        if (!ExperimentalFeatureFlags.RoleplayCharacterEnabled ||
            !IsRoleplayMapReadyForShell())
        {
            return;
        }

        var mapKey = GetRoleplayMapKeyForShell();
        if (string.IsNullOrWhiteSpace(mapKey))
        {
            return;
        }

        RoleplayCharacterSelectionStore.ResetForMap(mapKey);
        if (RoleplayCharacterSelectionStore.Get(mapKey) is not null)
        {
            return;
        }

        if (string.Equals(
                _roleplayPromptedMapKey,
                mapKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var options = GetRoleplayCharacterOptionsForShell();
        if (options.Count == 0)
        {
            return;
        }

        _roleplayPromptedMapKey = mapKey;
        OpenRoleplayCharacterWindowForShell();
    }

    internal IReadOnlyList<RoleplayCharacterOption> GetRoleplayCharacterOptionsForShell() =>
        _telemetryProvider.ReadRoleplayCharacterOptions();

    internal string? GetRoleplayMapKeyForShell()
    {
        var map = GetActiveMapForMultiplayer();
        return _lastTelemetry?.MapCompatibilityId ??
               map?.CompatibilityId ??
               _lastTelemetry?.MapName ??
               map?.FolderName;
    }

    internal bool IsRoleplayMapReadyForShell() =>
        _lastTelemetry?.IsInGame == true &&
        !string.IsNullOrWhiteSpace(GetRoleplayMapKeyForShell());

    internal RoleplayCharacterController GetRoleplayControllerForShell()
    {
        if (_roleplayCharacterController is not null)
        {
            return _roleplayCharacterController;
        }

        var controller = new RoleplayCharacterController(
            () => _lastTelemetry,
            GetActiveMapForMultiplayer,
            GetRoleplayMapKeyForShell);

        controller.NetworkStateReady += state =>
        {
            var multiplayer = _multiplayerWindow;
            if (multiplayer?.IsConnected == true)
            {
                _ = multiplayer.PublishLocalRoleplayCharacterAsync(state);
            }
        };

        controller.StateChanged += state =>
        {
            if (state is null && _multiplayerWindow?.IsConnected == true)
            {
                _ = _multiplayerWindow.ReleaseLocalRoleplayCharacterAsync();
            }

            UpdateHudRoleplayStateForShell();
        };

        _roleplayCharacterController = controller;
        HookRoleplayLifetime();
        return controller;
    }

    internal void UpdateHudRoleplayStateForShell()
    {
        if (_hudOverlay is null)
        {
            return;
        }

        var mapKey = GetRoleplayMapKeyForShell();
        var selected = RoleplayCharacterSelectionStore.Get(mapKey);
        var controller = _roleplayCharacterController;

        _hudOverlay.SetRoleplayState(
            featureEnabled: ExperimentalFeatureFlags.RoleplayCharacterEnabled,
            mapReady: IsRoleplayMapReadyForShell(),
            hasSelection: selected is not null,
            active: controller?.IsActive == true,
            characterName: selected?.DisplayName);
    }

    internal async void HandleHudRoleplayButtonRequestedForShell()
    {
        var mapKey = GetRoleplayMapKeyForShell();
        var selected = RoleplayCharacterSelectionStore.Get(mapKey);

        if (!ExperimentalFeatureFlags.RoleplayCharacterEnabled ||
            !IsRoleplayMapReadyForShell() ||
            selected is null)
        {
            OpenRoleplayCharacterWindowForShell();
            UpdateHudRoleplayStateForShell();
            return;
        }

        var controller = GetRoleplayControllerForShell();
        if (controller.IsActive)
        {
            await controller.StopAsync("roleplay-returned-to-bus");
            UpdateHudRoleplayStateForShell();
            return;
        }

        var started = await controller.StartAsync();
        UpdateHudRoleplayStateForShell();

        if (!started)
        {
            OpenRoleplayCharacterWindowForShell();
        }
    }

    internal void OpenRoleplayCharacterWindowForShell()
    {
        if (_roleplayCharacterWindow is not null)
        {
            if (_roleplayCharacterWindow.WindowState == WindowState.Minimized)
            {
                _roleplayCharacterWindow.WindowState = WindowState.Normal;
            }

            _roleplayCharacterWindow.Activate();
            return;
        }

        var window = new RoleplayCharacterWindow(
            GetRoleplayCharacterOptionsForShell,
            GetRoleplayMapKeyForShell,
            IsRoleplayMapReadyForShell,
            GetRoleplayControllerForShell())
        {
            Owner = this
        };

        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_roleplayCharacterWindow, window))
            {
                _roleplayCharacterWindow = null;
            }
        };

        _roleplayCharacterWindow = window;
        window.Show();
    }

    private void HookRoleplayLifetime()
    {
        if (_roleplayLifetimeHooked)
        {
            return;
        }

        _roleplayLifetimeHooked = true;
        Closed += (_, _) =>
        {
            _roleplayAutoPromptTimer?.Stop();
            _roleplayAutoPromptTimer = null;

            if (_roleplayCharacterController is { } controller)
            {
                _ = controller.DisposeAsync();
                _roleplayCharacterController = null;
            }

            RoleplayCharacterSelectionStore.Clear();
        };
    }
}
