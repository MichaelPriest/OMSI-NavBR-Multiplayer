using NavBR.Client.Multiplayer;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client;

public partial class MainWindow
{
    private RoleplayCharacterController? _roleplayCharacterController;
    private RoleplayCharacterWindow? _roleplayCharacterWindow;
    private bool _roleplayLifetimeHooked;

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
        };

        _roleplayCharacterController = controller;
        HookRoleplayLifetime();
        return controller;
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
            if (_roleplayCharacterController is { } controller)
            {
                _ = controller.DisposeAsync();
                _roleplayCharacterController = null;
            }

            RoleplayCharacterSelectionStore.Clear();
        };
    }
}
