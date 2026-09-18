using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using NavBR.Client.Diagnostics;

namespace NavBR.Client.Windows;

internal sealed class NavBRTrayIconService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private Icon? _icon;
    private MainWindow? _mainWindow;
    private bool _exitRequested;
    private bool _notificationShown;
    private bool _disposed;

    public void Attach(MainWindow mainWindow)
    {
        if (_disposed || _mainWindow is not null)
        {
            return;
        }

        _mainWindow = mainWindow;
        _icon = LoadApplicationIcon();
        _notifyIcon = new NotifyIcon
        {
            Text = "OMSI NavBR Multiplayer",
            Icon = _icon,
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowMainWindow();
            }
        };

        mainWindow.StateChanged += MainWindow_StateChanged;
        mainWindow.Closing += MainWindow_Closing;
        mainWindow.Closed += MainWindow_Closed;

        NavBRAppLog.Info("tray-icon-ready");
    }

    public void PrepareForSystemExit()
    {
        _exitRequested = true;
    }

    public void RequestExit()
    {
        if (_exitRequested)
        {
            return;
        }

        _exitRequested = true;
        NavBRAppLog.Info("tray-exit-requested");

        var app = System.Windows.Application.Current;
        if (app is null)
        {
            Dispose();
            return;
        }

        app.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (_mainWindow is not null)
                {
                    _mainWindow.Close();
                }
            }
            finally
            {
                Dispose();
                app.Shutdown();
            }
        });
    }

    public void ShowMainWindow()
    {
        var window = _mainWindow;
        if (window is null)
        {
            return;
        }

        window.Dispatcher.BeginInvoke(() =>
        {
            window.ShowPrimaryInterfaceForShell();
        });
    }

    public void HideMainWindow(bool showNotification = true)
    {
        var window = _mainWindow;
        if (window is null || _exitRequested)
        {
            return;
        }

        window.Dispatcher.BeginInvoke(() =>
        {
            window.HidePrimaryInterfaceForShell();

            if (showNotification)
            {
                ShowBackgroundNotificationOnce();
            }

            NavBRAppLog.Info("main-window-hidden-to-tray");
        });
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("Abrir NavBR");
        openItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(openItem);

        var toggleItem = new ToolStripMenuItem("Mostrar / ocultar");
        toggleItem.Click += (_, _) =>
        {
            var window = _mainWindow;
            if (window is null)
            {
                return;
            }

            if (window.IsPrimaryInterfaceVisibleForShell())
            {
                HideMainWindow(showNotification: false);
            }
            else
            {
                ShowMainWindow();
            }
        };
        menu.Items.Add(toggleItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Sair do NavBR");
        exitItem.Click += (_, _) => RequestExit();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (_exitRequested || _mainWindow?.WindowState != WindowState.Minimized)
        {
            return;
        }

        HideMainWindow();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        var app = System.Windows.Application.Current;
        if (_exitRequested || app?.Dispatcher.HasShutdownStarted == true)
        {
            return;
        }

        e.Cancel = true;
        HideMainWindow();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        DetachWindowEvents();
    }

    private void ShowBackgroundNotificationOnce()
    {
        if (_notificationShown || _notifyIcon is null)
        {
            return;
        }

        _notificationShown = true;
        try
        {
            _notifyIcon.BalloonTipTitle = "OMSI NavBR Multiplayer";
            _notifyIcon.BalloonTipText = "O NavBR continua rodando em segundo plano. Use o ícone na bandeja para abrir ou sair.";
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(3500);
        }
        catch
        {
            // Tray notifications are optional; hiding to tray must still work.
        }
    }

    private static Icon LoadApplicationIcon()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                var extracted = Icon.ExtractAssociatedIcon(executablePath);
                if (extracted is not null)
                {
                    return (Icon)extracted.Clone();
                }
            }
        }
        catch
        {
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private void DetachWindowEvents()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.StateChanged -= MainWindow_StateChanged;
        _mainWindow.Closing -= MainWindow_Closing;
        _mainWindow.Closed -= MainWindow_Closed;
        _mainWindow = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DetachWindowEvents();

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _icon?.Dispose();
        _icon = null;
    }
}
