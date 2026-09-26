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
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint GaRoot = 2;

    private readonly TimeSpan _hudVisibilityInterval = TimeSpan.FromMilliseconds(100);
    private readonly TimeSpan _hudFocusGracePeriod = TimeSpan.FromMilliseconds(900);
    private DispatcherTimer? _hudVisibilityTimer;
    private bool _hudLifecycleInitialized;
    private bool _restoreOmsiFocusOnChatClose = true;
    private bool _hudVisibleForOmsi;
    private bool _hudEnabled = true;
    private bool _hudVisibilitySettingsHooked;
    private DateTimeOffset _lastOmsiForegroundUtc = DateTimeOffset.MinValue;
    private IntPtr _lastTopmostReferenceHandle;
    private string? _lastHudRoomId;

    private void HudOverlayWindow_LifecycleLoaded(object sender, RoutedEventArgs e)
    {
        if (_hudLifecycleInitialized)
        {
            return;
        }

        _hudLifecycleInitialized = true;
        ChatInputPanel.IsVisibleChanged += ChatInputPanel_IsVisibleChanged;

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
        _positionTimer.Stop();
        InstallConflictFreeHotkeys();
    }

    private void HudOverlayWindow_LifecycleClosed(object? sender, EventArgs e)
    {
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
                // alpha.8 worked because the real OMSI gameplay HWND was learned
                // from the actual foreground window. Process.MainWindowHandle is
                // not reliable for all OMSI setups and may refer to another
                // top-level surface. Learn the gameplay surface here instead.
                if (!IsUsableOmsiWindow(_omsiWindowHandle) ||
                    !WindowBelongsToProcess(_omsiWindowHandle, processId) ||
                    _lastOmsiForegroundUtc == DateTimeOffset.MinValue)
                {
                    _omsiWindowHandle = foreground;
                }
                else
                {
                    var gameplayRoot = GetAncestor(_omsiWindowHandle, GaRoot);
                    if (gameplayRoot == IntPtr.Zero)
                    {
                        gameplayRoot = _omsiWindowHandle;
                    }

                    var foregroundRoot = GetAncestor(foreground, GaRoot);
                    if (foregroundRoot == IntPtr.Zero)
                    {
                        foregroundRoot = foreground;
                    }

                    // A separate OMSI top-level window is a menu/dialog. Keep
                    // the learned gameplay HWND unchanged and hide immediately.
                    if (foreground != _omsiWindowHandle &&
                        foregroundRoot != gameplayRoot)
                    {
                        HideHudForOmsiState();
                        return;
                    }
                }

                _lastOmsiForegroundUtc = now;
            }
            else if (!overlayOwnsForeground)
            {
                // Do not use Process.MainWindowHandle as a visibility fallback.
                // Until a real OMSI foreground surface has been learned, stay
                // hidden. This prevents a wrong HWND from blocking the HUD later.
                if (!IsUsableOmsiWindow(_omsiWindowHandle) ||
                    !WindowBelongsToProcess(_omsiWindowHandle, processId))
                {
                    HideHudForOmsiState();
                    return;
                }

                var neverFocused = _lastOmsiForegroundUtc == DateTimeOffset.MinValue;
                var focusLostTooLong = !neverFocused &&
                                       now - _lastOmsiForegroundUtc > _hudFocusGracePeriod;
                if (neverFocused || focusLostTooLong)
                {
                    HideHudForOmsiState();
                    return;
                }
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
        var becomingVisible = !_hudVisibleForOmsi || OverlayRoot.Visibility != Visibility.Visible;
        if (becomingVisible)
        {
            OverlayRoot.Visibility = Visibility.Visible;
            _hudVisibleForOmsi = true;
        }

        if (becomingVisible || _lastTopmostReferenceHandle != omsiHandle)
        {
            EnsureOverlayTopmost(overlayHandle);
            _lastTopmostReferenceHandle = omsiHandle;
        }
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

    private static void EnsureOverlayTopmost(IntPtr overlayHandle)
    {
        if (overlayHandle == IntPtr.Zero)
        {
            return;
        }

        _ = SetWindowPos(
            overlayHandle,
            HwndTopmost,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
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
