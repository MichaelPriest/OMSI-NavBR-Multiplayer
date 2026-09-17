using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NavBR.Client.Windows;

/// <summary>
/// Adds the Figma 2D/3D navigation mode control without changing the proven
/// roadmap implementation. 2D remains embedded in the navigation page while
/// 3D opens the real NavBR 3D view backed by OMSI map/route/session telemetry.
/// </summary>
internal static class Alpha12FigmaNavigationModeInstaller
{
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        var navigationPage = FindAncestor<ScrollViewer>(window.GpsHeadingText);
        if (navigationPage?.Content is not StackPanel stack)
        {
            Installed.Remove(window);
            return;
        }

        // Alpha12NavigationPolishInstaller still initializes the proven 3D
        // feature for compatibility. In the Figma shell its small legacy
        // button would duplicate the new 2D/3D selector, so remove only that
        // visual trigger; OpenNavigation3D remains the same implementation.
        RemoveLegacy3DButton(navigationPage);

        var bar = BuildModeBar(window);
        stack.Children.Insert(Math.Min(1, stack.Children.Count), bar);

        window.Closed += (_, _) => Installed.Remove(window);
    }

    private static void RemoveLegacy3DButton(DependencyObject navigationPage)
    {
        var legacy = Enumerate<Button>(navigationPage)
            .FirstOrDefault(button =>
                button.Content is string text &&
                string.Equals(text.Trim(), "3D", StringComparison.OrdinalIgnoreCase));

        if (legacy?.Parent is Panel panel)
        {
            panel.Children.Remove(legacy);
        }
    }

    private static Border BuildModeBar(MainWindow window)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var context = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        context.Children.Add(new TextBlock
        {
            Text = "MODO DO MAPA",
            Foreground = Brush(96, 113, 125),
            FontSize = 9d,
            FontWeight = FontWeights.Bold
        });
        context.Children.Add(new TextBlock
        {
            Text = "Alterne entre o roadmap 2D e a visualização 3D da operação.",
            Foreground = Brush(151, 171, 185),
            FontSize = 11d,
            Margin = new Thickness(0d, 4d, 0d, 0d)
        });
        grid.Children.Add(context);

        var switcher = new Border
        {
            Padding = new Thickness(4d),
            Background = Brush(13, 26, 36),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(10d),
            VerticalAlignment = VerticalAlignment.Center
        };
        var modes = new StackPanel { Orientation = Orientation.Horizontal };

        var twoD = ModeButton("2D", selected: true);
        twoD.ToolTip = "Mapa 2D integrado à página de navegação";
        modes.Children.Add(twoD);

        var threeD = ModeButton("3D", selected: false);
        threeD.Margin = new Thickness(4d, 0d, 0d, 0d);
        threeD.ToolTip = "Abrir mapa 3D com rota ativa e ônibus online";
        threeD.Click += (_, _) => window.OpenNavigation3D();
        modes.Children.Add(threeD);

        switcher.Child = modes;
        Grid.SetColumn(switcher, 1);
        grid.Children.Add(switcher);

        return new Border
        {
            Margin = new Thickness(0d, 0d, 0d, 16d),
            Padding = new Thickness(16d, 12d, 12d, 12d),
            Background = Brush(10, 19, 26),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Child = grid
        };
    }

    private static Button ModeButton(string text, bool selected)
    {
        return new Button
        {
            Content = text,
            MinWidth = 58d,
            Height = 34d,
            Padding = new Thickness(14d, 6d, 14d, 6d),
            Background = selected ? Brush(61, 137, 196) : Brushes.Transparent,
            Foreground = selected ? Brushes.White : Brush(151, 171, 185),
            BorderBrush = selected ? Brush(113, 198, 255) : Brushes.Transparent,
            BorderThickness = selected ? new Thickness(1d) : new Thickness(0d),
            FontSize = 12d,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };
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
