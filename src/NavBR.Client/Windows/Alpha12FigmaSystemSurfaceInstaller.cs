using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Hardware;

namespace NavBR.Client.Windows;

/// <summary>
/// Final Figma pass for the embedded System/Hardware surface. It keeps the
/// existing serial bridge and live telemetry behavior intact, but removes the
/// duplicate internal heading and maps the remaining legacy orange/green/red
/// accents to the shared Alpha.12 semantic palette.
/// </summary>
internal static class Alpha12FigmaSystemSurfaceInstaller
{
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        var hardware = Enumerate<HardwareCockpitView>(window).FirstOrDefault();
        if (hardware is null)
        {
            Installed.Remove(window);
            return;
        }

        ApplyHardwareSurface(hardware);
        window.Closed += (_, _) => Installed.Remove(window);
    }

    private static void ApplyHardwareSurface(HardwareCockpitView hardware)
    {
        hardware.Background = Background();

        var blocks = Enumerate<TextBlock>(hardware).ToArray();
        var internalTitle = blocks.FirstOrDefault(block =>
            string.Equals(block.Text, "Hardware Cockpit Bridge", StringComparison.OrdinalIgnoreCase));
        if (internalTitle is not null)
        {
            internalTitle.Visibility = Visibility.Collapsed;
            if (FindSiblingAfter(internalTitle) is TextBlock subtitle)
            {
                subtitle.Visibility = Visibility.Collapsed;
            }
        }

        foreach (var border in Enumerate<Border>(hardware))
        {
            if (border.Background is SolidColorBrush background && IsNeutralDark(background))
            {
                border.Background = Card();
                border.BorderBrush = BorderColor();
                if (border.BorderThickness.Left > 0d || border.BorderThickness.Top > 0d ||
                    border.BorderThickness.Right > 0d || border.BorderThickness.Bottom > 0d)
                {
                    border.BorderThickness = new Thickness(1d);
                }
                if (MaxRadius(border.CornerRadius) > 0d)
                {
                    border.CornerRadius = new CornerRadius(Math.Max(10d, MaxRadius(border.CornerRadius)));
                }
            }
        }

        foreach (var combo in Enumerate<ComboBox>(hardware))
        {
            combo.MinHeight = Math.Max(36d, combo.MinHeight);
            combo.Background = Elevated();
            combo.Foreground = Text();
            combo.BorderBrush = BorderColor();
            combo.BorderThickness = new Thickness(1d);
            combo.Padding = new Thickness(10d, 6d, 10d, 6d);
        }

        foreach (var textBox in Enumerate<TextBox>(hardware))
        {
            textBox.Background = Brush(7, 18, 27);
            textBox.Foreground = Text();
            textBox.BorderBrush = BorderColor();
            textBox.BorderThickness = new Thickness(1d);
            textBox.CaretBrush = Interaction();
            textBox.SelectionBrush = Accent();
        }

        foreach (var button in Enumerate<Button>(hardware))
        {
            StyleButton(button);
        }

        foreach (var expander in Enumerate<Expander>(hardware))
        {
            expander.Foreground = Muted();
            expander.Background = Brushes.Transparent;
            expander.BorderBrush = BorderColor();
            expander.BorderThickness = new Thickness(0d);
            expander.Padding = new Thickness(0d);
        }

        foreach (var block in blocks)
        {
            NormalizeSemanticColor(block);
        }
    }

    private static void StyleButton(Button button)
    {
        var label = button.Content?.ToString() ?? string.Empty;
        var disconnect = label.Contains("Desconectar", StringComparison.OrdinalIgnoreCase) ||
                         label.Contains("Disconnect", StringComparison.OrdinalIgnoreCase);
        var connect = !disconnect &&
                      (label.Contains("Conectar", StringComparison.OrdinalIgnoreCase) ||
                       label.Contains("Connect", StringComparison.OrdinalIgnoreCase));

        button.MinHeight = Math.Max(36d, button.MinHeight);
        button.Padding = new Thickness(14d, 7d, 14d, 7d);
        button.Foreground = Brushes.White;
        button.FontWeight = FontWeights.SemiBold;
        button.BorderThickness = new Thickness(1d);
        button.Background = disconnect
            ? Brush(61, 27, 32)
            : connect
                ? Accent()
                : Elevated();
        button.BorderBrush = disconnect
            ? Error()
            : connect
                ? Interaction()
                : BorderColor();
    }

    private static void NormalizeSemanticColor(TextBlock block)
    {
        if (block.Foreground is not SolidColorBrush foreground)
        {
            return;
        }

        var color = foreground.Color;
        if (Near(color, 244, 122, 24))
        {
            block.Foreground = Warning();
        }
        else if (Near(color, 78, 201, 137))
        {
            block.Foreground = Success();
        }
        else if (Near(color, 237, 111, 111))
        {
            block.Foreground = Error();
        }
        else if (Near(color, 145, 164, 180) || Near(color, 183, 199, 213))
        {
            block.Foreground = Muted();
        }
    }

    private static DependencyObject? FindSiblingAfter(DependencyObject element)
    {
        var parent = VisualTreeHelper.GetParent(element);
        if (parent is null)
        {
            return null;
        }

        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < count - 1; index++)
        {
            if (ReferenceEquals(VisualTreeHelper.GetChild(parent, index), element))
            {
                return VisualTreeHelper.GetChild(parent, index + 1);
            }
        }
        return null;
    }

    private static bool Near(Color color, byte r, byte g, byte b, int tolerance = 12) =>
        Math.Abs(color.R - r) <= tolerance &&
        Math.Abs(color.G - g) <= tolerance &&
        Math.Abs(color.B - b) <= tolerance;

    private static bool IsNeutralDark(SolidColorBrush brush)
    {
        var color = brush.Color;
        var max = Math.Max(color.R, Math.Max(color.G, color.B));
        var min = Math.Min(color.R, Math.Min(color.G, color.B));
        return max <= 72 && max - min <= 34;
    }

    private static double MaxRadius(CornerRadius radius) =>
        Math.Max(Math.Max(radius.TopLeft, radius.TopRight), Math.Max(radius.BottomLeft, radius.BottomRight));

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

    private static SolidColorBrush Background() => Brush(6, 16, 26);
    private static SolidColorBrush Card() => Brush(10, 19, 26);
    private static SolidColorBrush Elevated() => Brush(13, 26, 36);
    private static SolidColorBrush BorderColor() => Brush(28, 42, 51);
    private static SolidColorBrush Text() => Brush(218, 230, 238);
    private static SolidColorBrush Muted() => Brush(151, 171, 185);
    private static SolidColorBrush Accent() => Brush(61, 137, 196);
    private static SolidColorBrush Interaction() => Brush(113, 198, 255);
    private static SolidColorBrush Success() => Brush(56, 201, 140);
    private static SolidColorBrush Warning() => Brush(242, 184, 75);
    private static SolidColorBrush Error() => Brush(239, 91, 100);
    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
