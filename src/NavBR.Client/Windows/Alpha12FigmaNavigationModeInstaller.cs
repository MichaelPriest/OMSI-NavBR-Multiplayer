using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace NavBR.Client.Windows;

/// <summary>
/// Figma fidelity pass for the Navigation page header. The 2D/3D switch lives
/// in the page header (matching the approved desktop frame) instead of an extra
/// explanatory card. The proven 2D roadmap remains embedded and 3D continues
/// to open the real NavBR 3D view backed by OMSI/session telemetry.
/// </summary>
internal static class Alpha12FigmaNavigationModeInstaller
{
    private static readonly HashSet<MainWindow> Installed = new();
    private static ControlTemplate? ModeButtonTemplate;

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
        // button would duplicate the segmented header selector, so remove only
        // that trigger; OpenNavigation3D keeps using the same implementation.
        RemoveLegacy3DButton(navigationPage);

        // Preserve the original heading instance as the localization source,
        // but replace its visual position with the Figma header row.
        window.GpsHeadingText.Visibility = Visibility.Collapsed;
        var header = BuildHeader(window);
        stack.Children.Insert(0, header);

        navigationPage.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

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

    private static Grid BuildHeader(MainWindow window)
    {
        var header = new Grid
        {
            MinHeight = 48d,
            Margin = new Thickness(0d, 0d, 0d, 16d)
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush(218, 230, 238),
            FontSize = 24d,
            FontWeight = FontWeights.SemiBold
        };
        title.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text))
        {
            Source = window.GpsHeadingText,
            Mode = BindingMode.OneWay
        });
        header.Children.Add(title);

        var switcher = new Border
        {
            Padding = new Thickness(4d),
            Background = Brush(13, 26, 36),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(10d),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var modes = new StackPanel { Orientation = Orientation.Horizontal };

        var twoD = ModeButton("2D", selected: true);
        twoD.ToolTip = "Mapa 2D integrado à navegação";
        modes.Children.Add(twoD);

        var threeD = ModeButton("3D", selected: false);
        threeD.Margin = new Thickness(4d, 0d, 0d, 0d);
        threeD.ToolTip = "Abrir visualização 3D da rota e dos ônibus online";
        threeD.Click += (_, _) => window.OpenNavigation3D();
        modes.Children.Add(threeD);

        switcher.Child = modes;
        Grid.SetColumn(switcher, 1);
        header.Children.Add(switcher);

        return header;
    }

    private static Button ModeButton(string text, bool selected)
    {
        var button = new Button
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
            Cursor = System.Windows.Input.Cursors.Hand,
            Template = GetModeButtonTemplate()
        };

        return button;
    }

    private static ControlTemplate GetModeButtonTemplate()
    {
        if (ModeButtonTemplate is not null)
        {
            return ModeButtonTemplate;
        }

        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7d));
        border.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Control.BorderBrush))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(Control.BorderThickness))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetBinding(ContentPresenter.ContentProperty, new Binding(nameof(ContentControl.Content))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        presenter.SetBinding(ContentPresenter.MarginProperty, new Binding(nameof(Control.Padding))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        presenter.SetBinding(ContentPresenter.TextElementForegroundProperty, new Binding(nameof(Control.Foreground))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.AppendChild(presenter);

        ModeButtonTemplate = new ControlTemplate(typeof(Button)) { VisualTree = border };
        return ModeButtonTemplate;
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
