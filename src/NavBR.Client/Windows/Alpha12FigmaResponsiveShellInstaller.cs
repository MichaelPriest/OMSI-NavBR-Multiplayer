using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NavBR.Client.Windows;

/// <summary>
/// Adds the responsive/collapsible behaviour defined by the Figma shell.
/// Desktop navigation stays at 252 px normally and can collapse to a compact
/// 72 px icon rail for smaller screens such as 1366x768.
/// </summary>
internal static class Alpha12FigmaResponsiveShellInstaller
{
    private const double ExpandedWidth = 252d;
    private const double CollapsedWidth = 72d;
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window) ||
            window.Content is not Grid root ||
            root.ColumnDefinitions.Count < 2)
        {
            Installed.Remove(window);
            return;
        }

        var sidebar = root.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 0 && border.Child is DockPanel);
        if (sidebar?.Child is not DockPanel dock)
        {
            Installed.Remove(window);
            return;
        }

        var scroller = dock.Children.OfType<ScrollViewer>().FirstOrDefault();
        if (scroller?.Content is not StackPanel body)
        {
            Installed.Remove(window);
            return;
        }

        var footer = dock.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => !ReferenceEquals(panel, body));

        var originalButtonContent = new Dictionary<Button, object?>();
        var sidebarText = Enumerate<TextBlock>(sidebar).ToArray();

        var toggle = new Button
        {
            Content = "≪  Recolher",
            Height = 36d,
            Margin = new Thickness(0d, 0d, 0d, 14d),
            Padding = new Thickness(12d, 7d, 12d, 7d),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brush(13, 26, 36),
            Foreground = Brush(151, 171, 185),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            FontSize = 11d,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Recolher menu lateral"
        };
        body.Children.Insert(0, toggle);

        var collapsed = false;

        void CaptureButtons()
        {
            foreach (var button in Enumerate<Button>(sidebar))
            {
                if (ReferenceEquals(button, toggle) || originalButtonContent.ContainsKey(button))
                {
                    continue;
                }

                originalButtonContent[button] = button.Content;
            }
        }

        void ApplyState()
        {
            CaptureButtons();
            root.ColumnDefinitions[0].Width = new GridLength(collapsed ? CollapsedWidth : ExpandedWidth);
            sidebar.Padding = new Thickness(0d);

            toggle.Content = collapsed ? "≫" : "≪  Recolher";
            toggle.HorizontalContentAlignment = collapsed
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Left;
            toggle.ToolTip = collapsed ? "Expandir menu lateral" : "Recolher menu lateral";

            foreach (var pair in originalButtonContent)
            {
                var button = pair.Key;
                if (pair.Value is string text)
                {
                    button.Content = collapsed ? CompactLabel(text) : text;
                }

                button.HorizontalContentAlignment = collapsed
                    ? HorizontalAlignment.Center
                    : HorizontalAlignment.Left;
                button.Padding = collapsed
                    ? new Thickness(8d)
                    : new Thickness(16d, 9d, 12d, 9d);
                button.ToolTip = collapsed && pair.Value is string fullText
                    ? StripIconSpacing(fullText)
                    : null;
            }

            foreach (var text in sidebarText)
            {
                text.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            }

            if (footer is not null)
            {
                footer.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        toggle.Click += (_, _) =>
        {
            collapsed = !collapsed;
            ApplyState();
        };

        ApplyState();
        window.Closed += (_, _) => Installed.Remove(window);
    }

    private static string CompactLabel(string value)
    {
        var trimmed = value.TrimStart();
        if (trimmed.Length == 0)
        {
            return "•";
        }

        // NavBR navigation labels begin with a compact symbol followed by
        // two spaces. Preserve the whole symbol token for compact mode.
        var split = trimmed.IndexOf("  ", StringComparison.Ordinal);
        if (split > 0)
        {
            return trimmed[..split].Trim();
        }

        return trimmed[..1];
    }

    private static string StripIconSpacing(string value)
    {
        var trimmed = value.Trim();
        var split = trimmed.IndexOf("  ", StringComparison.Ordinal);
        return split >= 0 && split + 2 < trimmed.Length
            ? trimmed[(split + 2)..].Trim()
            : trimmed;
    }

    private static IEnumerable<T> Enumerate<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in Enumerate<T>(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
