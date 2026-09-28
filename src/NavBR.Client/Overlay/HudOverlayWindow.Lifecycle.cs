using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint GaRoot = 2;

    private readonly TimeSpan _hudVisibilityInterval = TimeSpan.FromMilliseconds(100);
    private DispatcherTimer? _hudVisibilityTimer;
    private bool _hudLifecycleInitialized;
    private bool _restoreOmsiFocusOnChatClose = true;
    private bool _hudVisibleForOmsi;
    private bool _hudEnabled = true;
    private bool _hudVisibilitySettingsHooked;
    private DateTimeOffset _lastOmsiForegroundUtc = DateTimeOffset.MinValue;
    private IntPtr _lastTopmostReferenceHandle;
    private IntPtr _hudOwnerHandle;
    private string? _lastHudRoomId;

    private void HudOverlayWindow_LifecycleLoaded(object sender, RoutedEventArgs e)
    {
        if (_hudLifecycleInitialized)
        {
            return;
        }

        _hudLifecycleInitialized = true;
        ChatInputPanel.IsVisibleChanged += ChatInputPanel_IsVisibleChanged;

        // Single owner for HUD startup. The constructor no longer installs a
        // second keyboard hook/timer set through an anonymous Loaded handler.
        InitializeImmersiveOperationHud();
        _presenceTimer.Start();
        FollowOmsiWindow();

        var hudSettings = NavBR.Client.Multiplayer.MultiplayerSettingsStore.Load();
        _hudEnabled = hudSettings.HudEnabled;
        ApplyTelematrixSettings(hudSettings);
        if (!_hudVisibilitySettingsHooked)
        {
            _hudVisibilitySettingsHooked = true;
            NavBR.Client.Multiplayer.MultiplayerSettingsStore.SettingsSaved +=
                OnHudVisibilitySettingsSaved;
        }

        OverlayRoot.Visibility = Visibility.Collapsed;
        _hudVisibleForOmsi = false;

        _hudVisibilityTimer = new DispatcherTimer
        {
            Interval = _hudVisibilityInterval
        };
        _hudVisibilityTimer.Tick += HudVisibilityTimer_Tick;
        _hudVisibilityTimer.Start();

        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, FinalizeHudLifecycleInitialization);

        RefreshHudChrome();
        RefreshHudVisibility();
        RefreshRoleplayButtonInteraction();
        RefreshTelematrixPanel();
        RenderEnhancedMiniMap();
    }

    private void FinalizeHudLifecycleInitialization()
    {
        InstallConflictFreeHotkeys();
    }

    private void HudOverlayWindow_LifecycleClosed(object? sender, EventArgs e)
    {
        _positionTimer.Stop();
        _presenceTimer.Stop();
        _keyboardHook?.Dispose();
        _keyboardHook = null;

        UnsubscribeHotkeySettings();
        if (_hudVisibilitySettingsHooked)
        {
            NavBR.Client.Multiplayer.MultiplayerSettingsStore.SettingsSaved -=
                OnHudVisibilitySettingsSaved;
            _hudVisibilitySettingsHooked = false;
        }

        if (_hudVisibilityTimer is null)
        {
            return;
        }

        _hudVisibilityTimer.Stop();
        _hudVisibilityTimer.Tick -= HudVisibilityTimer_Tick;
        _hudVisibilityTimer = null;
    }

    private void HudVisibilityTimer_Tick(object? sender, EventArgs e)
    {
        RefreshOmsiHotkeyConflicts();
        RefreshHudChrome();
        RefreshHudVisibility();
        RefreshRoleplayButtonInteraction();
        RenderEnhancedMiniMap();
    }

    private void RefreshHudChrome()
    {
        var connected = ReferenceEquals(ConnectionDot.Fill, Brushes.LimeGreen);
        var playerCount = connected ? _smoothedHudFrames.Count + 1 : 0;

        PlayerCountText.Text = playerCount.ToString(LocalizationService.CurrentCulture);
        PlayerCountText.ToolTip = LocalizationService.Format("MultiplayerPlayerCount", playerCount);
        ChatInputLabelText.Text = LocalizationService.Get("MultiplayerChat").ToUpper(LocalizationService.CurrentCulture);

        var chatShortcut = _chatHotkeyAvailable
            ? $"{_chatHotkey.Name}: {LocalizationService.Get("MultiplayerChat")}"
            : $"{_chatHotkey.Name}: OMSI";
        var voiceShortcut = _voiceHotkeyAvailable
            ? $"{_voiceHotkey.Name}: PTT"
            : $"{_voiceHotkey.Name}: OMSI";
        HudShortcutsText.Text =
            $"  •  {chatShortcut}  •  {voiceShortcut}  •  Ctrl+Alt+H: HUD";

        var hasHotkeyConflict = !_chatHotkeyAvailable || !_voiceHotkeyAvailable;
        HotkeyWarningPanel.Visibility = hasHotkeyConflict ? Visibility.Visible : Visibility.Collapsed;
        if (hasHotkeyConflict)
        {
            HotkeyWarningText.Text = BuildHotkeyConflictMessage();
            HotkeyWarningPanel.ToolTip = BuildHotkeyConflictTooltip();
        }

        if (connected)
        {
            if (HudStatusText.Text.StartsWith("Sala ", StringComparison.OrdinalIgnoreCase))
            {
                _lastHudRoomId = HudStatusText.Text[5..].Trim();
            }

            HudStatusText.Text = string.IsNullOrWhiteSpace(_lastHudRoomId)
                ? LocalizationService.Get("MultiplayerConnected")
                : $"{LocalizationService.Get("MultiplayerRoom")}: {_lastHudRoomId}";
        }
        else
        {
            _lastHudRoomId = null;
            HudStatusText.Text = LocalizationService.Get("MultiplayerDisconnected");
        }

        if (VoiceStatusText.Text.EndsWith(" falando", StringComparison.OrdinalIgnoreCase))
        {
            VoiceStatusText.Text = VoiceStatusText.Text[..^8].TrimEnd();
        }
        else if (VoiceStatusText.Text.StartsWith("Voz: ", StringComparison.OrdinalIgnoreCase))
        {
            VoiceStatusText.Text = VoiceStatusText.Text[5..].TrimStart();
        }
    }

    private void OnHudVisibilitySettingsSaved(
        NavBR.Client.Multiplayer.MultiplayerSettings settings)
    {
        _hudEnabled = settings.HudEnabled;
        ApplyTelematrixSettings(settings);
        RefreshHudVisibility();
        RefreshTelematrixPanel();
    }

    public void SetHudEnabled(bool enabled)
    {
        var current =
            NavBR.Client.Multiplayer.MultiplayerSettingsStore.Load();
        if (current.HudEnabled == enabled &&
            current.HudVisibilitySettingsVersion >= 1)
        {
            _hudEnabled = enabled;
            RefreshHudVisibility();
            return;
        }

        NavBR.Client.Multiplayer.MultiplayerSettingsStore.Save(
            current with
            {
                HudVisibilitySettingsVersion = 1,
                HudEnabled = enabled
            });
    }

    public void ToggleHudEnabled() => SetHudEnabled(!_hudEnabled);

    private void RefreshHudVisibility()
    {
        if (!_hudEnabled)
        {
            HideHudForOmsiState();
            return;
        }

        if (_omsiProcessId is not int processId)
        {
            HideHudForOmsiState();
            return;
        }

        // HUD is a gameplay surface, not an OMSI-process-wide overlay.
        // Do not show it in launcher/menu/options/dialog windows before a
        // real vehicle/map telemetry frame exists.
        if (_localTelemetry is null ||
            string.IsNullOrWhiteSpace(_localTelemetry.MapName))
        {
            HideHudForOmsiState();
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                HideHudForOmsiState();
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var foreground = GetForegroundWindow();
            var overlayHandle = new WindowInteropHelper(this).Handle;
            var foregroundBelongsToOmsi = WindowBelongsToProcess(foreground, processId);
            var foregroundBelongsToNavBr = WindowBelongsToProcess(foreground, Environment.ProcessId);
            var overlayOwnsForeground = _chatInteractive &&
                                        overlayHandle != IntPtr.Zero &&
                                        foreground == overlayHandle;

            if (foregroundBelongsToNavBr &&
                !overlayOwnsForeground &&
                !_hudLayoutEditMode)
            {
                HideHudForOmsiState();
                return;
            }

            if (foregroundBelongsToOmsi)
            {
                // Learn the gameplay HWND only while real gameplay telemetry is
                // alive. Once learned, any other OMSI top-level foreground HWND
                // is treated as a menu/dialog and the HUD is hidden.
                if (!IsUsableOmsiWindow(_omsiWindowHandle) ||
                    !WindowBelongsToProcess(_omsiWindowHandle, processId) ||
                    _lastOmsiForegroundUtc == DateTimeOffset.MinValue)
                {
                    _omsiWindowHandle = foreground;
                    _hudOwnerHandle = IntPtr.Zero;
                }
                else if (foreground != _omsiWindowHandle)
                {
                    HideHudForOmsiState();
                    return;
                }

                _lastOmsiForegroundUtc = now;
            }
            else if (!overlayOwnsForeground)
            {
                // Strict gameplay-only visibility: as soon as another process
                // becomes foreground, remove the whole overlay window instead
                // of leaving a desktop-global transparent TOPMOST surface.
                HideHudForOmsiState();
                return;
            }

            var gameplayHandle = _omsiWindowHandle;
            if (!IsUsableOmsiWindow(gameplayHandle) ||
                !WindowBelongsToProcess(gameplayHandle, processId))
            {
                HideHudForOmsiState();
                return;
            }

            SyncHudGeometryStable(gameplayHandle);
            ShowHudForOmsiState(overlayHandle, gameplayHandle);
        }
        catch
        {
            HideHudForOmsiState();
        }
    }

    private void SyncHudGeometryStable(IntPtr omsiHandle)
    {
        if (!GetWindowRect(omsiHandle, out var rect))
        {
            return;
        }

        var dpi = GetDpiForWindow(omsiHandle);
        var scale = dpi > 0 ? 96d / dpi : 1d;
        var left = rect.Left * scale;
        var top = rect.Top * scale;
        var width = Math.Max(1d, (rect.Right - rect.Left) * scale);
        var height = Math.Max(1d, (rect.Bottom - rect.Top) * scale);

        const double tolerance = 0.5d;
        if (Math.Abs(Left - left) > tolerance)
        {
            Left = left;
        }

        if (Math.Abs(Top - top) > tolerance)
        {
            Top = top;
        }

        if (Math.Abs(Width - width) > tolerance)
        {
            Width = width;
        }

        if (Math.Abs(Height - height) > tolerance)
        {
            Height = height;
        }
    }

    private static bool IsUsableOmsiWindow(IntPtr handle) =>
        handle != IntPtr.Zero &&
        IsWindowVisibleNative(handle) &&
        !IsIconicNative(handle);

    private void ShowHudForOmsiState(IntPtr overlayHandle, IntPtr omsiHandle)
    {
        EnsureOverlayOwnedByOmsi(overlayHandle, omsiHandle);

        var becomingVisible =
            !_hudVisibleForOmsi ||
            OverlayRoot.Visibility != Visibility.Visible ||
            !IsVisible;
        if (becomingVisible)
        {
            OverlayRoot.Visibility = Visibility.Visible;
            _hudVisibleForOmsi = true;
            if (!IsVisible)
            {
                Show();
            }
        }

        _lastTopmostReferenceHandle = omsiHandle;
    }

    private void HideHudForOmsiState()
    {
        if (_localPushToTalk)
        {
            SetLocalPushToTalk(false);
        }

        if (_chatInteractive)
        {
            _restoreOmsiFocusOnChatClose = false;
            CloseChatInput();
        }

        if (!_hudVisibleForOmsi && OverlayRoot.Visibility == Visibility.Collapsed)
        {
            return;
        }

        OverlayRoot.Visibility = Visibility.Collapsed;
        _hudVisibleForOmsi = false;
        _lastTopmostReferenceHandle = IntPtr.Zero;

        if (!_hudLayoutEditMode && IsVisible)
        {
            Hide();
        }
    }

    private void ChatInputPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not bool isVisible || isVisible)
        {
            return;
        }

        var shouldRestore = _restoreOmsiFocusOnChatClose;
        _restoreOmsiFocusOnChatClose = true;
        if (!shouldRestore)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, RestoreOmsiFocus);
    }

    private void RestoreOmsiFocus()
    {
        if (_omsiWindowHandle == IntPtr.Zero || IsIconicNative(_omsiWindowHandle))
        {
            return;
        }

        _ = SetForegroundWindowNative(_omsiWindowHandle);
    }

    private static bool WindowBelongsToProcess(IntPtr windowHandle, int processId)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(windowHandle, out var foregroundProcessId);
        return foregroundProcessId == (uint)processId;
    }

    private void EnsureOverlayOwnedByOmsi(
        IntPtr overlayHandle,
        IntPtr omsiHandle)
    {
        if (overlayHandle == IntPtr.Zero ||
            omsiHandle == IntPtr.Zero ||
            _hudOwnerHandle == omsiHandle)
        {
            return;
        }

        // Native HWND ownership keeps the overlay above OMSI but not above
        // unrelated desktop applications. This is intentionally different
        // from HWND_TOPMOST / WPF Topmost, which caused the HUD to leak across
        // every window on the desktop.
        _ = SetNativeOwner(overlayHandle, omsiHandle);
        _hudOwnerHandle = omsiHandle;
    }

    private static IntPtr SetNativeOwner(IntPtr window, IntPtr owner)
    {
        const int gwlHwndParent = -8;
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(window, gwlHwndParent, owner)
            : new IntPtr(SetWindowLong32(
                window,
                gwlHwndParent,
                owner.ToInt32()));
    }

    [DllImport("user32.dll", EntryPoint = "IsIconic")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconicNative(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "IsWindowVisible")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisibleNative(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindowNative(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(
        IntPtr hWnd,
        int nIndex,
        int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(
        IntPtr hWnd,
        int nIndex,
        IntPtr dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
