using System.Windows.Threading;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private const int VkF9 = 0x78;
    private const int VkF10 = 0x79;

    private void InstallConflictFreeHotkeys()
    {
        try
        {
            _keyboardHook?.Dispose();
            _keyboardHook = null;
            _pressedKeys.Clear();

            _keyboardHook = new GlobalKeyboardHook();
            _keyboardHook.KeyChanged += (virtualKey, isDown) =>
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Input,
                    () => HandleConflictFreeHotkey(virtualKey, isDown));
        }
        catch
        {
            // O HUD continua funcional sem atalhos globais.
        }
    }

    private void HandleConflictFreeHotkey(int virtualKey, bool isDown)
    {
        if (isDown)
        {
            if (!_pressedKeys.Add(virtualKey))
            {
                return;
            }
        }
        else
        {
            _pressedKeys.Remove(virtualKey);
        }

        if (virtualKey == VkF10)
        {
            if (isDown)
            {
                if (!_chatInteractive && IsOmsiForeground())
                {
                    SetLocalPushToTalk(true);
                }
            }
            else if (_localPushToTalk)
            {
                SetLocalPushToTalk(false);
            }

            return;
        }

        if (virtualKey == VkF9 && isDown && !_chatInteractive && IsOmsiForeground())
        {
            OpenChatInput();
        }
    }
}
