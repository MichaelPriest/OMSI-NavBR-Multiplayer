using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace NavBR.Client;

public partial class WebShellWindow : Window
{
    private readonly Func<object> _stateProvider;
    private readonly Action _launchOmsi;
    private readonly Func<string, JsonElement?, Task>? _commandHandler;
    private readonly Func<string?>? _activeMapDirectoryProvider;
    private readonly DispatcherTimer _pushTimer;
    private bool _ready;
    private string? _mappedMapDirectory;

    public bool IsReady => _ready;
    public event EventHandler? ShellReady;

    public WebShellWindow(
        Func<object> stateProvider,
        Action launchOmsi,
        Func<string, JsonElement?, Task>? commandHandler = null,
        Func<string?>? activeMapDirectoryProvider = null)
    {
        ArgumentNullException.ThrowIfNull(stateProvider);
        ArgumentNullException.ThrowIfNull(launchOmsi);

        _stateProvider = stateProvider;
        _launchOmsi = launchOmsi;
        _commandHandler = commandHandler;
        _activeMapDirectoryProvider = activeMapDirectoryProvider;

        InitializeComponent();

        _pushTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _pushTimer.Tick += (_, _) => PushState();

        Loaded += async (_, _) => await InitializeWebViewAsync();
        Closed += (_, _) => _pushTimer.Stop();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            await WebView.EnsureCoreWebView2Async();
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            WebView.CoreWebView2.WebMessageReceived += WebMessageReceived;
            WebView.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess)
                {
                    ShowError($"Falha de navegação WebView2: {args.WebErrorStatus}");
                    return;
                }

                var wasReady = _ready;
                _ready = true;
                PushState();
                _pushTimer.Start();
                if (!wasReady)
                {
                    ShellReady?.Invoke(this, EventArgs.Empty);
                }
            };

            var preferredRoot = Path.Combine(AppContext.BaseDirectory, "WebUI", "dist");
            var fallbackRoot = Path.Combine(AppContext.BaseDirectory, "WebUI", "bootstrap");
            var root = File.Exists(Path.Combine(preferredRoot, "index.html"))
                ? preferredRoot
                : fallbackRoot;

            if (!File.Exists(Path.Combine(root, "index.html")))
            {
                ShowError($"Arquivos da interface web não encontrados em {root}.");
                return;
            }

            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "navbr.local",
                root,
                CoreWebView2HostResourceAccessKind.Allow);

            WebView.CoreWebView2.Navigate("https://navbr.local/index.html");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            if (!message.RootElement.TryGetProperty("command", out var commandElement))
            {
                return;
            }

            var command = commandElement.GetString();
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            switch (command)
            {
                case "launchOmsi":
                    _launchOmsi();
                    PushState();
                    return;
                case "refreshState":
                    PushState();
                    return;
            }

            JsonElement? payload = message.RootElement.TryGetProperty("payload", out var payloadElement)
                ? payloadElement.Clone()
                : null;

            if (_commandHandler is not null)
            {
                await _commandHandler(command, payload);
                PushState();
            }
        }
        catch (JsonException)
        {
            // Ignore malformed UI messages; the native shell remains authoritative.
        }
        catch (Exception ex)
        {
            WebView.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(new
            {
                type = "navbr-command-error",
                message = ex.Message
            }));
        }
    }

    private void PushState()
    {
        if (!_ready || WebView.CoreWebView2 is null)
        {
            return;
        }

        RefreshActiveMapResourceMapping();

        var envelope = new
        {
            type = "navbr-state",
            payload = _stateProvider()
        };

        WebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(envelope));
    }

    private void RefreshActiveMapResourceMapping()
    {
        if (WebView.CoreWebView2 is null || _activeMapDirectoryProvider is null)
        {
            return;
        }

        string? directory = null;
        try
        {
            directory = _activeMapDirectoryProvider();
        }
        catch
        {
            directory = null;
        }

        directory = !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
            ? Path.GetFullPath(directory)
            : null;

        if (string.Equals(
                directory,
                _mappedMapDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            WebView.CoreWebView2.ClearVirtualHostNameToFolderMapping("navbr-map.local");
        }
        catch
        {
        }

        _mappedMapDirectory = directory;
        if (_mappedMapDirectory is null)
        {
            return;
        }

        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "navbr-map.local",
            _mappedMapDirectory,
            CoreWebView2HostResourceAccessKind.Allow);
    }

    private void ShowError(string message)
    {
        _pushTimer.Stop();
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
    }
}
