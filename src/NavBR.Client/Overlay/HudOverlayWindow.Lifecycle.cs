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

    private readonly TimeSpan _hudVisibilityInterval = TimeSpan.FromMilliseconds(100);
    private readonly TimeSpan _hudFocusGracePeriod = TimeSpan.FromMilliseconds(900);
    private DispatcherTimer? _hudVisibilityTimer;
    private bool _hudLifecycleInitialized;
    private bool _restoreOmsiFocusOnChatClose = true;
    private bool _hudVisibleForOmsi;
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

        // Começa oculto e só aparece quando o OMSI realmente estiver ativo.
        // Isso evita um flash do HUD no desktop durante a criação da janela.
        OverlayRoot.Visibility = Visibility.Collapsed;
        _hudVisibleForOmsi = false;

        _hudVisibilityTimer = new DispatcherTimer
        {
            Interval = _hudVisibilityInterval
        };
        _hudVisibilityTimer.Tick += HudVisibilityTimer_Tick;
        _hudVisibilityTimer.Start();

        // O construtor antigo ainda inicia um timer de posicionamento de 250 ms.
        // Desliga esse loop depois que todos os handlers de Loaded terminarem e
        // deixa este lifecycle como fonte única de geometria/z-order do HUD.
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, FinalizeHudLifecycleInitialization);

        RefreshHudChrome();
        RefreshHudVisibility();
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

        // Presentation-only refresh. Bitmap geometry is owned exclusively by
        // RenderMiniMap(), preventing alpha.7's competing Width/Height/Left/Top
        // updates from making the roadmap alternate/flicker.
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
        HudShortcutsText.Text = $"  •  {chatShortcut}  •  {voiceShortcut}";

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

    private void RefreshHudVisibility()
    {
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

            // Keep a single canonical OMSI surface. Dialogs and auxiliary OMSI
            // windows belong to the same process but must never become the HUD
            // reference window, otherwise the minimap appears on top of menus.
            var mainOmsiHandle = process.MainWindowHandle;
            if (!IsUsableOmsiWindow(mainOmsiHandle))
            {
                HideHudForOmsiState();
                return;
            }

            _omsiWindowHandle = mainOmsiHandle;

            var now = DateTimeOffset.UtcNow;
            var foreground = GetForegroundWindow();
            var overlayHandle = new WindowInteropHelper(this).Handle;
            var foregroundBelongsToOmsi = WindowBelongsToProcess(foreground, processId);
            var foregroundBelongsToNavBr = WindowBelongsToProcess(foreground, Environment.ProcessId);
            var overlayOwnsForeground = _chatInteractive &&
                                        overlayHandle != IntPtr.Zero &&
                                        foreground == overlayHandle;

            // NavBR dialogs/windows are not part of the game HUD surface. Hide
            // immediately when one of them has focus so the minimap does not
            // appear on top of Settings, Multiplayer, Feedback or new windows.
            // Chat input and layout edit are intentional interactive exceptions.
            if (foregroundBelongsToNavBr &&
                !overlayOwnsForeground &&
                !_hudLayoutEditMode)
            {
                HideHudForOmsiState();
                return;
            }

            if (foregroundBelongsToOmsi)
            {
                // Only the primary OMSI window is the gameplay surface. Any
                // other top-level HWND owned by Omsi.exe is treated as an
                // in-game dialog/menu and hides the HUD immediately.
                if (foreground != mainOmsiHandle)
                {
                    HideHudForOmsiState();
                    return;
                }

                _lastOmsiForegroundUtc = now;
            }
            else if (!overlayOwnsForeground)
            {
                var neverFocused = _lastOmsiForegroundUtc == DateTimeOffset.MinValue;
                var focusLostTooLong = !neverFocused &&
                                       now - _lastOmsiForegroundUtc > _hudFocusGracePeriod;
                if (neverFocused || focusLostTooLong)
                {
                    HideHudForOmsiState();
                    return;
                }
            }

            SyncHudGeometryStable(mainOmsiHandle);
            ShowHudForOmsiState(overlayHandle, mainOmsiHandle);
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

        // Não reaplica TOPMOST a cada tick. Isso evita churn de z-order que
        // pode causar piscadas em DirectX/WPF. Só reforça ao mostrar ou quando
        // a janela de referência do OMSI muda.
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
