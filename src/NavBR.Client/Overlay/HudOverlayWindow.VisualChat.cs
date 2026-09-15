using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private DispatcherTimer? _visualChatTimer;
    private StackPanel? _visualChatMessagesPanel;
    private ScrollViewer? _visualChatScrollViewer;
    private TextBlock? _visualChatCountText;
    private string? _visualChatFingerprint;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        InitializeVisualChat();
        EnsureBusDashboard();
        InitializeBusStopHud();
    }

    private void InitializeVisualChat()
    {
        if (_visualChatMessagesPanel is not null)
        {
            return;
        }

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(0, 0, 0, 7)
        };

        _visualChatCountText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromArgb(185, 255, 255, 255)),
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(_visualChatCountText, Dock.Right);
        header.Children.Add(_visualChatCountText);

        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleRow.Children.Add(new Border
        {
            Width = 7,
            Height = 7,
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 7, 0),
            Background = new SolidColorBrush(Color.FromRgb(255, 157, 36))
        });
        titleRow.Children.Add(new TextBlock
        {
            Text = "CHAT NAVBR",
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(titleRow);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _visualChatMessagesPanel = new StackPanel
        {
            Orientation = Orientation.Vertical
        };

        _visualChatScrollViewer = new ScrollViewer
        {
            Content = _visualChatMessagesPanel,
            MaxHeight = 205,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = true
        };
        Grid.SetRow(_visualChatScrollViewer, 1);
        root.Children.Add(_visualChatScrollViewer);

        // The legacy TextBlock remains allocated because older code still writes
        // to it, but the visual tree now uses the richer message panel above.
        ChatPanel.Child = root;
        ChatPanel.Background = new SolidColorBrush(Color.FromArgb(220, 11, 15, 20));
        ChatPanel.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 157, 36));
        ChatPanel.BorderThickness = new Thickness(1);
        ChatPanel.CornerRadius = new CornerRadius(10);
        ChatPanel.Padding = new Thickness(10, 9, 10, 10);

        _visualChatTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _visualChatTimer.Tick += (_, _) => RefreshVisualChat();
        _visualChatTimer.Start();
        Closed += (_, _) => _visualChatTimer?.Stop();

        RefreshVisualChat(force: true);
    }

    private void RefreshVisualChat(bool force = false)
    {
        var panel = _visualChatMessagesPanel;
        if (panel is null)
        {
            return;
        }

        var last = _chatMessages.LastOrDefault();
        var lastTimestamp = last is null ? 0L : last.TimestampUtc.ToUnixTimeMilliseconds();
        var fingerprint = $"{_chatMessages.Count}|{lastTimestamp}|{last?.PlayerId}|{last?.Text}|{_chatInteractive}";
        if (!force && string.Equals(fingerprint, _visualChatFingerprint, StringComparison.Ordinal))
        {
            return;
        }

        _visualChatFingerprint = fingerprint;
        panel.Children.Clear();

        var visibleMessages = _chatMessages.TakeLast(_chatInteractive ? 12 : 6).ToArray();
        if (_visualChatCountText is not null)
        {
            _visualChatCountText.Text = _chatInteractive
                ? $"{_chatMessages.Count} mensagem{(_chatMessages.Count == 1 ? string.Empty : "s")} • ESC fecha"
                : $"{_chatMessages.Count} mensagem{(_chatMessages.Count == 1 ? string.Empty : "s")}";
        }

        if (visibleMessages.Length == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Nenhuma mensagem ainda.",
                Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                FontSize = 11,
                Margin = new Thickness(2, 4, 2, 4)
            });
            return;
        }

        foreach (var message in visibleMessages)
        {
            panel.Children.Add(BuildChatBubble(message));
        }

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => _visualChatScrollViewer?.ScrollToEnd()));
    }

    private FrameworkElement BuildChatBubble(ChatMessage message)
    {
        if (message.IsSystem)
        {
            return new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 3),
                Padding = new Thickness(8, 3, 8, 3),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                Child = new TextBlock
                {
                    Text = message.Text,
                    Foreground = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)),
                    FontSize = 10,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 440
                }
            };
        }

        var isLocal = string.Equals(
            message.DisplayName?.Trim(),
            _localDisplayName?.Trim(),
            StringComparison.OrdinalIgnoreCase);

        var content = new StackPanel();
        var header = new DockPanel { LastChildFill = true };
        var time = new TextBlock
        {
            Text = message.TimestampUtc.ToLocalTime().ToString("HH:mm"),
            Foreground = new SolidColorBrush(Color.FromArgb(145, 255, 255, 255)),
            FontSize = 9,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(time, Dock.Right);
        header.Children.Add(time);
        header.Children.Add(new TextBlock
        {
            Text = isLocal ? "Você" : message.DisplayName,
            Foreground = isLocal
                ? new SolidColorBrush(Color.FromRgb(255, 181, 76))
                : new SolidColorBrush(Color.FromRgb(95, 210, 255)),
            FontWeight = FontWeights.SemiBold,
            FontSize = 10.5
        });
        content.Children.Add(header);
        content.Children.Add(new TextBlock
        {
            Text = message.Text,
            Foreground = Brushes.White,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 405,
            Margin = new Thickness(0, 2, 0, 0)
        });

        return new Border
        {
            HorizontalAlignment = isLocal ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = 440,
            Margin = new Thickness(isLocal ? 42 : 0, 3, isLocal ? 0 : 42, 3),
            Padding = new Thickness(9, 6, 9, 7),
            CornerRadius = new CornerRadius(9),
            Background = isLocal
                ? new SolidColorBrush(Color.FromArgb(130, 92, 55, 18))
                : new SolidColorBrush(Color.FromArgb(155, 18, 27, 36)),
            BorderBrush = isLocal
                ? new SolidColorBrush(Color.FromArgb(120, 255, 157, 36))
                : new SolidColorBrush(Color.FromArgb(65, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Child = content
        };
    }
}
