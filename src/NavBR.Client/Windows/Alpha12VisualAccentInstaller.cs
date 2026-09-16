using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NavBR.Client.Windows;

internal static class Alpha12VisualAccentInstaller
{
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        Apply(window);
        window.LayoutUpdated += Window_LayoutUpdated;
        window.Closed += (_, _) =>
        {
            window.LayoutUpdated -= Window_LayoutUpdated;
            Installed.Remove(window);
        };

        void Window_LayoutUpdated(object? sender, EventArgs e) => Apply(window);
    }

    private static void Apply(MainWindow window)
    {
        StylePrimaryButton(window.MultiplayerButton);

        foreach (var text in Enumerate<TextBlock>(window))
        {
            if (text.Text.Equals("ALPHA.12", StringComparison.OrdinalIgnoreCase) ||
                text.Text.StartsWith("ALPHA.12 •", StringComparison.OrdinalIgnoreCase) ||
                text.Text.StartsWith("v0.3.0-alpha.12", StringComparison.OrdinalIgnoreCase))
            {
                text.Foreground = Accent();
                if (FindAncestor<Border>(text) is { } badge)
                {
                    badge.Background = Brush(7, 34, 51);
                    badge.BorderBrush = Brush(24, 85, 117);
                }
            }

            if (text.Text is "AUTO" or "PRONTO" or "PEER HOST" or "SERIAL")
            {
                text.Foreground = Accent();
            }
        }

        var pageButtons = Enumerate<Button>(window)
            .Where(IsShellPageButton)
            .ToArray();
        foreach (var button in pageButtons)
        {
            if (button.Tag as string == "alpha12-blue-handler")
            {
                continue;
            }

            button.Tag = "alpha12-blue-handler";
            button.Click += (_, _) =>
            {
                foreach (var candidate in pageButtons)
                {
                    StyleNeutralButton(candidate);
                }
                StyleSelectedButton(button);
            };
        }
    }

    private static bool IsShellPageButton(Button button)
    {
        if (button.Content is not string text)
        {
            return false;
        }

        return text.StartsWith("⌂", StringComparison.Ordinal)
            || text.StartsWith("⌖", StringComparison.Ordinal)
            || text.StartsWith("●", StringComparison.Ordinal)
            || text.StartsWith("▣", StringComparison.Ordinal)
            || text.StartsWith("✦", StringComparison.Ordinal);
    }

    private static void StyleSelectedButton(Button button)
    {
        button.Background = Brush(11, 48, 75);
        button.BorderBrush = Brush(40, 133, 193);
        button.BorderThickness = new Thickness(3d, 0d, 0d, 0d);
        button.Foreground = Brushes.White;
    }

    private static void StyleNeutralButton(Button button)
    {
        button.Background = Brush(10, 19, 25);
        button.BorderBrush = Brush(28, 42, 51);
        button.BorderThickness = new Thickness(1d);
        button.Foreground = Brush(218, 230, 238);
    }

    private static void StylePrimaryButton(Button button)
    {
        button.Background = Brush(19, 103, 171);
        button.BorderBrush = Brush(55, 155, 221);
        button.Foreground = Brushes.White;
    }

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject? current = child;
        while (current is not null)
        {
            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
            if (current is T match)
            {
                return match;
            }
        }
        return null;
    }

    private static IEnumerable<T> Enumerate<T>(DependencyObject root)
        where T : DependencyObject
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

    private static SolidColorBrush Accent() => Brush(82, 196, 255);

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}