using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace NavBR.Client.Windows;

/// <summary>
/// Figma fidelity pass for the Navigation page. The 2D/3D switch lives in the
/// page header and the content follows the approved map + route-rail layout.
/// The embedded roadmap and the 3D window continue to use real OMSI/session
/// data; this class only owns presentation and the live route identity header.
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
        var navigationCard = FindAncestor<Border>(window.GpsHeadingText);
        if (navigationPage?.Content is not StackPanel stack || navigationCard is null)
        {
            Installed.Remove(window);
            return;
        }

        RemoveLegacy3DButton(navigationPage);

        // BuildNavigation originally adds a generic page heading before the map.
        // The approved frame has one single navigation header, so remove that
        // legacy heading and replace it with the Figma header row.
        if (stack.Children.Count > 1)
        {
            stack.Children.RemoveAt(0);
        }

        window.GpsHeadingText.Visibility = Visibility.Collapsed;
        var header = BuildHeader(window);
        stack.Children.Insert(0, header);

        navigationPage.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        ConfigureMapChrome(window);
        ConfigureMapAndRouteRail(window, navigationPage, navigationCard, out var routeCard);

        // LiveData + OrderedStops are installed immediately after this class in
        // App.xaml.cs. Defer the line/destination row until that synchronous
        // setup has finished so KeepHeadingOnly cannot remove it.
        window.Dispatcher.BeginInvoke(
            new Action(() => InstallRouteIdentity(window, routeCard)),
            DispatcherPriority.ContextIdle);

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

    private static void ConfigureMapAndRouteRail(
        MainWindow window,
        ScrollViewer navigationPage,
        Border navigationCard,
        out Border? routeCard)
    {
        routeCard = null;
        if (navigationCard.Parent is not Grid contentGrid || contentGrid.ColumnDefinitions.Count < 3)
        {
            return;
        }

        // Approved 1920x1080 frame: dominant map, 20px gutter and compact
        // information rail. Star sizing keeps the same visual hierarchy on
        // smaller desktop resolutions instead of forcing a fixed map width.
        contentGrid.ColumnDefinitions[0].Width = new GridLength(1d, GridUnitType.Star);
        contentGrid.ColumnDefinitions[1].Width = new GridLength(20d);
        contentGrid.ColumnDefinitions[2].Width = new GridLength(360d);
        contentGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
        contentGrid.VerticalAlignment = VerticalAlignment.Stretch;

        navigationCard.Margin = new Thickness(0d);
        navigationCard.Background = Brush(10, 19, 26);
        navigationCard.BorderBrush = Brush(28, 42, 51);
        navigationCard.BorderThickness = new Thickness(1d);
        navigationCard.CornerRadius = new CornerRadius(12d);
        navigationCard.HorizontalAlignment = HorizontalAlignment.Stretch;
        navigationCard.VerticalAlignment = VerticalAlignment.Top;

        routeCard = contentGrid.Children
            .OfType<Border>()
            .FirstOrDefault(border => !ReferenceEquals(border, navigationCard));

        if (routeCard is not null)
        {
            routeCard.Margin = new Thickness(0d);
            routeCard.Background = Brush(10, 19, 26);
            routeCard.BorderBrush = Brush(28, 42, 51);
            routeCard.BorderThickness = new Thickness(1d);
            routeCard.CornerRadius = new CornerRadius(12d);
            routeCard.HorizontalAlignment = HorizontalAlignment.Stretch;
            routeCard.VerticalAlignment = VerticalAlignment.Stretch;
            EnsureScrollableRouteRail(routeCard);
        }

        // Copy the out value to a local before wiring callbacks. C# does not
        // allow ref/out parameters to be captured by local functions/lambdas.
        var rail = routeCard;
        void ApplySizing()
        {
            var viewport = navigationPage.ViewportHeight > 1d
                ? navigationPage.ViewportHeight
                : Math.Max(0d, window.ActualHeight - 150d);
            var targetHeight = Math.Clamp(viewport - 64d, 540d, 860d);

            var availableWidth = navigationPage.ViewportWidth > 1d
                ? navigationPage.ViewportWidth
                : Math.Max(0d, window.ActualWidth - 300d);
            var railWidth = availableWidth switch
            {
                < 860d => 290d,
                < 1040d => 320d,
                < 1280d => 340d,
                _ => 360d
            };
            contentGrid.ColumnDefinitions[2].Width = new GridLength(railWidth);

            navigationCard.Height = targetHeight;
            if (rail is not null)
            {
                rail.Height = targetHeight;
            }
        }

        ApplySizing();
        navigationPage.SizeChanged += (_, _) => ApplySizing();
    }

    private static void ConfigureMapChrome(MainWindow window)
    {
        // The page header now carries the live navigation state. Hiding the
        // legacy line inside the map card gives the roadmap the dominant visual
        // area intended by the approved Alpha.12/Alpha.13 layout.
        window.GpsStatusText.Visibility = Visibility.Collapsed;
        window.InstalledMapsText.Visibility = Visibility.Collapsed;

        window.RoadmapScrollViewer.Height = double.NaN;
        window.RoadmapScrollViewer.MinHeight = 420d;
        window.RoadmapScrollViewer.HorizontalAlignment = HorizontalAlignment.Stretch;
        window.RoadmapScrollViewer.VerticalAlignment = VerticalAlignment.Stretch;

        var mapFrame = FindAncestor<Border>(window.RoadmapScrollViewer);
        if (mapFrame is not null)
        {
            mapFrame.Margin = new Thickness(0d, 8d, 0d, 0d);
            mapFrame.Background = Brush(5, 12, 20);
            mapFrame.BorderBrush = Brush(28, 42, 51);
            mapFrame.BorderThickness = new Thickness(1d);
            mapFrame.CornerRadius = new CornerRadius(12d);
        }

        foreach (var control in new Control[]
                 {
                     window.ZoomOutButton,
                     window.ZoomInButton,
                     window.FitMapButton,
                     window.FollowButton,
                     window.TopmostButton
                 })
        {
            control.MinWidth = 36d;
            control.Height = 32d;
            control.Padding = new Thickness(10d, 4d, 10d, 4d);
            control.Background = Brush(13, 26, 36);
            control.Foreground = Brush(190, 205, 215);
            control.BorderBrush = Brush(28, 42, 51);
            control.BorderThickness = new Thickness(1d);
            control.FontSize = 11d;
            control.FontWeight = FontWeights.SemiBold;
        }
    }

    private static void EnsureScrollableRouteRail(Border routeCard)
    {
        if (routeCard.Child is ScrollViewer)
        {
            return;
        }

        if (routeCard.Child is not StackPanel body)
        {
            return;
        }

        routeCard.Child = null;
        routeCard.Child = new ScrollViewer
        {
            Tag = "figma-route-rail-scroll",
            Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            PanningMode = PanningMode.VerticalOnly,
            Padding = new Thickness(0d),
            Background = Brushes.Transparent
        };
    }

    private static StackPanel? FindRouteBody(Border? routeCard)
    {
        if (routeCard?.Child is StackPanel direct)
        {
            return direct;
        }

        return routeCard?.Child is ScrollViewer { Content: StackPanel scrollBody }
            ? scrollBody
            : null;
    }

    private static void InstallRouteIdentity(MainWindow window, Border? routeCard)
    {
        if (FindRouteBody(routeCard) is not { } body || !window.IsLoaded)
        {
            return;
        }

        if (body.Children.OfType<Grid>().Any(item => Equals(item.Tag, "figma-route-identity")))
        {
            return;
        }

        var lineValue = IdentityValue();
        var destinationValue = IdentityValue();

        var identity = new Grid
        {
            Tag = "figma-route-identity",
            Margin = new Thickness(0d, 14d, 0d, 18d)
        };
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112d) });
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var lineBlock = IdentityBlock("LINHA", lineValue);
        Grid.SetColumn(lineBlock, 0);
        identity.Children.Add(lineBlock);

        var destinationBlock = IdentityBlock("DESTINO", destinationValue);
        destinationBlock.Margin = new Thickness(14d, 0d, 0d, 0d);
        Grid.SetColumn(destinationBlock, 1);
        identity.Children.Add(destinationBlock);

        body.Children.Insert(Math.Min(1, body.Children.Count), identity);

        void Refresh()
        {
            var current = window.GetNavigationIdentityForAlpha12();
            lineValue.Text = Safe(current.Line);
            destinationValue.Text = Safe(current.DestinationName);
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750d) };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
        Refresh();
        window.Closed += (_, _) => timer.Stop();
    }

    private static StackPanel IdentityBlock(string label, TextBlock value)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9.5d,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(151, 171, 185)
        });
        value.Margin = new Thickness(0d, 5d, 0d, 0d);
        stack.Children.Add(value);
        return stack;
    }

    private static TextBlock IdentityValue() => new()
    {
        Text = "—",
        FontSize = 14d,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brush(218, 230, 238),
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextWrapping = TextWrapping.NoWrap
    };

    private static string Safe(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static Grid BuildHeader(MainWindow window)
    {
        var header = new Grid
        {
            MinHeight = 48d,
            Margin = new Thickness(0d, 0d, 0d, 16d)
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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

        var statusText = new TextBlock
        {
            FontSize = 10.5d,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(151, 171, 185),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 330d
        };
        statusText.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text))
        {
            Source = window.GpsStatusText,
            Mode = BindingMode.OneWay
        });

        var statusChip = new Border
        {
            Margin = new Thickness(16d, 0d, 12d, 0d),
            Padding = new Thickness(11d, 6d, 11d, 6d),
            Background = Brush(13, 26, 36),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(999d),
            VerticalAlignment = VerticalAlignment.Center,
            Child = statusText
        };
        Grid.SetColumn(statusChip, 1);
        header.Children.Add(statusChip);

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
        Grid.SetColumn(switcher, 2);
        header.Children.Add(switcher);

        return header;
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
            Cursor = System.Windows.Input.Cursors.Hand,
            Template = GetModeButtonTemplate()
        };
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
        presenter.SetBinding(System.Windows.Documents.TextElement.ForegroundProperty, new Binding(nameof(Control.Foreground))
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
