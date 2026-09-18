using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace NavBR.Client;

public partial class WebShellWindow : Window
{
    private readonly Func<object> _stateProvider;
    private readonly Action _launchOmsi;
    private readonly DispatcherTimer _pushTimer;
    private bool _ready;

    public WebShellWindow(Func<object> stateProvider, Action launchOmsi)
    {
        ArgumentNullException.ThrowIfNull(stateProvider);
        ArgumentNullException.ThrowIfNull(launchOmsi);

        _stateProvider = stateProvider;
        _launchOmsi = launchOmsi;

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

                _ready = true;
                PushState();
                _pushTimer.Start();
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

    private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            if (!message.RootElement.TryGetProperty("command", out var commandElement))
            {
                return;
            }

            switch (commandElement.GetString())
            {
                case "launchOmsi":
                    _launchOmsi();
                    PushState();
                    break;
                case "refreshState":
                    PushState();
                    break;
            }
        }
        catch (JsonException)
        {
            // Ignore malformed UI messages; the native shell remains authoritative.
        }
    }

    private void PushState()
    {
        if (!_ready || WebView.CoreWebView2 is null)
        {
            return;
        }

        var envelope = new
        {
            type = "navbr-state",
            payload = _stateProvider()
        };

        WebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(envelope));
    }

    private void ShowError(string message)
    {
        _pushTimer.Stop();
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
    }
}
