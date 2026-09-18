using System.ComponentModel;
using System.Windows;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private bool _persistentLifetimeInitialized;
    private bool _allowApplicationClose;

    internal bool HasBackgroundSession => _host.IsRunning || _client.IsConnected;

    private void InitializePersistentLifetime()
    {
        if (_persistentLifetimeInitialized)
        {
            return;
        }

        _persistentLifetimeInitialized = true;
        Closing += MultiplayerWindow_Closing;
        StateChanged += MultiplayerWindow_StateChanged;

        if (Owner is Window owner)
        {
            owner.Closing += Owner_Closing;
        }
    }

    internal void AllowApplicationShutdown()
    {
        _allowApplicationClose = true;
    }

    private void Owner_Closing(object? sender, CancelEventArgs e)
    {
        _allowApplicationClose = true;
    }

    private void MultiplayerWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowApplicationClose)
        {
            return;
        }

        // Closing the Central is a UI action only. The room host, SignalR
        // client, voice and telemetry continue running in the application.
        // Keeping the window alive also keeps every existing event subscription
        // and overlay integration attached to the same session instance.
        e.Cancel = true;
        ShowInTaskbar = false;
        WindowState = WindowState.Minimized;
    }

    private void MultiplayerWindow_StateChanged(object? sender, EventArgs e)
    {
        // The MultiplayerWindow is now a hidden native controller. State
        // transitions must never resurrect the retired WPF visual surface.
        ShowInTaskbar = false;
    }
}
