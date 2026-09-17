using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Path = System.Windows.Shapes.Path;

namespace NavBR.Client.Windows;

/// <summary>
/// Fidelity pass for the Figma shell. Keeps the original string Content on
/// navigation buttons (so legacy click/navigation lookup keeps working) while
/// rendering the sidebar with true vector icons through ContentTemplate.
/// Also applies the exact 1920x1080 Home proportions from the approved Figma
/// frame and a compact 72 px icon rail only at very narrow window widths.
/// </summary>
internal static class Alpha12FigmaResponsiveShellInstaller
{
    private const double ExpandedWidth = 252d;
    private const double CollapsedWidth = 72d;
    private const double CompactThreshold = 1120d;
    private static readonly HashSet<MainWindow> Installed = new();
    private static readonly NavLabelConverter LabelConverter = new();
    private static readonly Dictionary<string, DataTemplate> ExpandedTemplates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, DataTemplate> CollapsedTemplates = new(StringComparer.OrdinalIgnoreCase);

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

        var staticSidebarText = Enumerate<TextBlock>(sidebar).ToArray();

        ApplyFigmaSidebarMeasurements(root, sidebar, dock, body);

        void ApplyState()
        {
            var compact = window.ActualWidth > 0d && window.ActualWidth < CompactThreshold;
            root.ColumnDefinitions[0].Width = new GridLength(compact ? CollapsedWidth : ExpandedWidth);
            dock.Margin = compact
                ? new Thickness(12d, 22d, 12d, 14d)
                : new Thickness(24d, 22d, 24d, 14d);

            foreach (var button in Enumerate<Button>(sidebar))
            {
                ApplyVectorButton(button, compact);
            }

            foreach (var text in staticSidebarText)
            {
                text.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            }

            if (footer is not null)
            {
                footer.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            }

            ApplyFigmaHomeMeasurements(window);
        }

        SizeChangedEventHandler sizeChanged = (_, _) => ApplyState();
        window.SizeChanged += sizeChanged;
        ApplyState();

        window.Closed += (_, _) =>
        {
            window.SizeChanged -= sizeChanged;
            Installed.Remove(window);
        };
    }

    private static void ApplyFigmaSidebarMeasurements(
        Grid root,
        Border sidebar,
        DockPanel dock,
        StackPanel body)
    {
        root.ColumnDefinitions[0].Width = new GridLength(ExpandedWidth);
        sidebar.Background = Brush(7, 18, 27);
        sidebar.BorderBrush = Brush(28, 42, 51);
        sidebar.BorderThickness = new Thickness(0d, 0d, 1d, 0d);
        dock.Margin = new Thickness(24d, 22d, 24d, 14d);

        var navbr = Enumerate<TextBlock>(body)
            .FirstOrDefault(text => string.Equals(text.Text, "NAVBR", StringComparison.OrdinalIgnoreCase));
        if (navbr is not null && FindAncestor<StackPanel>(navbr) is { } brand)
        {
            brand.Margin = new Thickness(4d, 0d, 0d, 28d);
        }

        foreach (var text in Enumerate<TextBlock>(body))
        {
            if (text.FontSize <= 10d &&
                (text.Text is "DIRIGIR" or "OPERAÇÃO" or "SISTEMA" or "AJUDA"))
            {
                text.Margin = new Thickness(4d, 0d, 0d, 10d);
            }
        }
    }

    private static void ApplyFigmaHomeMeasurements(MainWindow window)
    {
        var omsi = FindCard(window, "OMSI");
        var operationCurrent = FindCard(window, "OPERAÇÃO ATUAL");
        var multiplayer = FindCard(window, "MULTIPLAYER");
        var company = FindCard(window, "EMPRESA");

        if (omsi is not null && operationCurrent is not null && multiplayer is not null && company is not null &&
            FindParent<Grid>(omsi) is { } topGrid && topGrid.ColumnDefinitions.Count >= 7)
        {
            var compactWidth = window.ActualWidth > 0d && window.ActualWidth < 1320d;
            var compactHeight = window.ActualHeight > 0d && window.ActualHeight < 820d;

            topGrid.Margin = new Thickness(0d);
            topGrid.ColumnDefinitions[0].Width = compactWidth
                ? new GridLength(1d, GridUnitType.Star)
                : new GridLength(374d, GridUnitType.Star);
            topGrid.ColumnDefinitions[1].Width = new GridLength(compactWidth ? 12d : 16d);
            topGrid.ColumnDefinitions[2].Width = compactWidth
                ? new GridLength(1.08d, GridUnitType.Star)
                : new GridLength(430d, GridUnitType.Star);
            topGrid.ColumnDefinitions[3].Width = new GridLength(compactWidth ? 12d : 16d);
            topGrid.ColumnDefinitions[4].Width = compactWidth
                ? new GridLength(0.92d, GridUnitType.Star)
                : new GridLength(330d, GridUnitType.Star);
            topGrid.ColumnDefinitions[5].Width = new GridLength(compactWidth ? 12d : 16d);
            topGrid.ColumnDefinitions[6].Width = compactWidth
                ? new GridLength(1d, GridUnitType.Star)
                : new GridLength(406d, GridUnitType.Star);

            foreach (var card in new[] { omsi, operationCurrent, multiplayer, company })
            {
                card.Margin = new Thickness(0d);
                card.Height = compactHeight ? 154d : 174d;
            }
        }

        var operation = FindCard(window, "OPERAÇÃO");
        var speed = FindCard(window, "VELOCIDADE");
        var schedule = FindCard(window, "HORÁRIO");
        var status = FindCard(window, "STATUS OPERACIONAL");
        if (operation is not null && speed is not null && schedule is not null && status is not null &&
            FindParent<Grid>(operation) is { } operationRow && operationRow.ColumnDefinitions.Count >= 3 &&
            FindParent<Grid>(speed) is { } metrics && metrics.ColumnDefinitions.Count >= 3 && metrics.RowDefinitions.Count >= 3)
        {
            var compactWidth = window.ActualWidth > 0d && window.ActualWidth < 1320d;
            var compactHeight = window.ActualHeight > 0d && window.ActualHeight < 820d;
            var operationHeight = compactHeight ? 270d : 300d;
            var metricHeight = compactHeight ? 127d : 142d;

            operationRow.Height = operationHeight;
            operationRow.ColumnDefinitions[0].Width = new GridLength(compactWidth ? 1.75d : 1010d, GridUnitType.Star);
            operationRow.ColumnDefinitions[1].Width = new GridLength(compactWidth ? 12d : 16d);
            operationRow.ColumnDefinitions[2].Width = new GridLength(compactWidth ? 1d : 562d, GridUnitType.Star);
            operation.Height = operationHeight;

            metrics.ColumnDefinitions[0].Width = new GridLength(1d, GridUnitType.Star);
            metrics.ColumnDefinitions[1].Width = new GridLength(compactWidth ? 12d : 16d);
            metrics.ColumnDefinitions[2].Width = new GridLength(1d, GridUnitType.Star);
            metrics.RowDefinitions[0].Height = new GridLength(metricHeight);
            metrics.RowDefinitions[1].Height = new GridLength(compactHeight ? 12d : 16d);
            metrics.RowDefinitions[2].Height = new GridLength(metricHeight);
            speed.Height = metricHeight;
            schedule.Height = metricHeight;
            status.Height = metricHeight;
        }

        var quick = FindCard(window, "AÇÕES RÁPIDAS");
        if (quick is not null)
        {
            quick.Height = window.ActualHeight > 0d && window.ActualHeight < 820d ? 146d : 166d;
        }
    }

    private static void ApplyVectorButton(Button button, bool compact)
    {
        var iconKey = ResolveIconKey(button);
        if (iconKey is null)
        {
            return;
        }

        var selected = button.BorderThickness.Left >= 2d && button.BorderThickness.Right < 1d;
        var isNavAction = !IsFooterAction(button);

        button.Height = isNavAction ? 40d : Math.Max(36d, button.Height);
        button.Margin = isNavAction ? new Thickness(0d, 0d, 0d, 6d) : button.Margin;
        button.Padding = compact ? new Thickness(0d) : new Thickness(12d, 8d, 12d, 8d);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        button.Background = selected ? Brush(16, 38, 56) : Brushes.Transparent;
        button.Foreground = selected ? Brush(218, 230, 238) : Brush(151, 171, 185);
        button.BorderBrush = selected ? Brush(113, 198, 255) : Brushes.Transparent;
        button.BorderThickness = selected ? new Thickness(3d, 0d, 0d, 0d) : new Thickness(0d);
        button.FontSize = 12.5d;
        button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Medium;
        button.ContentTemplate = GetTemplate(iconKey, compact);
        button.ToolTip = compact ? CleanLabel(button.Content?.ToString()) : null;
    }

    private static bool IsFooterAction(Button button)
    {
        var label = Normalize(CleanLabel(button.Content?.ToString()));
        return label.Contains("minimizar", StringComparison.Ordinal) ||
               label.Contains("tray", StringComparison.Ordinal) ||
               label.Contains("bandeja", StringComparison.Ordinal);
    }

    private static DataTemplate GetTemplate(string iconKey, bool compact)
    {
        var cache = compact ? CollapsedTemplates : ExpandedTemplates;
        if (cache.TryGetValue(iconKey, out var cached))
        {
            return cached;
        }

        var template = compact ? BuildIconOnlyTemplate(iconKey) : BuildIconLabelTemplate(iconKey);
        cache[iconKey] = template;
        return template;
    }

    private static DataTemplate BuildIconLabelTemplate(string iconKey)
    {
        var row = new FrameworkElementFactory(typeof(StackPanel));
        row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        row.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        row.AppendChild(BuildIconFactory(iconKey));

        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetValue(FrameworkElement.MarginProperty, new Thickness(12d, 0d, 0d, 0d));
        label.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        label.SetValue(TextBlock.FontSizeProperty, 12.5d);
        label.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
        label.SetBinding(TextBlock.ForegroundProperty, AncestorButtonBinding(Control.ForegroundProperty));
        label.SetBinding(TextBlock.TextProperty, new Binding(".") { Converter = LabelConverter });
        row.AppendChild(label);

        return new DataTemplate { VisualTree = row };
    }

    private static DataTemplate BuildIconOnlyTemplate(string iconKey) =>
        new() { VisualTree = BuildIconFactory(iconKey) };

    private static FrameworkElementFactory BuildIconFactory(string iconKey)
    {
        var viewbox = new FrameworkElementFactory(typeof(Viewbox));
        viewbox.SetValue(FrameworkElement.WidthProperty, 18d);
        viewbox.SetValue(FrameworkElement.HeightProperty, 18d);
        viewbox.SetValue(Viewbox.StretchProperty, Stretch.Uniform);
        viewbox.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        var path = new FrameworkElementFactory(typeof(Path));
        path.SetValue(Path.DataProperty, Geometry.Parse(GeometryFor(iconKey)));
        path.SetValue(Path.FillProperty, Brushes.Transparent);
        path.SetBinding(Path.StrokeProperty, AncestorButtonBinding(Control.ForegroundProperty));
        path.SetValue(Path.StrokeThicknessProperty, 1.7d);
        path.SetValue(Path.StrokeLineJoinProperty, PenLineJoin.Round);
        path.SetValue(Path.StrokeStartLineCapProperty, PenLineCap.Round);
        path.SetValue(Path.StrokeEndLineCapProperty, PenLineCap.Round);
        viewbox.AppendChild(path);
        return viewbox;
    }

    private static Binding AncestorButtonBinding(DependencyProperty property) => new(property.Name)
    {
        RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1)
    };

    private static string? ResolveIconKey(Button button)
    {
        var tag = button.Tag?.ToString() ?? string.Empty;
        if (tag.Contains("dispatcher", StringComparison.OrdinalIgnoreCase)) return "cco";
        if (tag.Contains("company-fleet", StringComparison.OrdinalIgnoreCase)) return "company";
        if (tag.Contains("company-network", StringComparison.OrdinalIgnoreCase)) return "network";
        if (tag.Contains("company-members", StringComparison.OrdinalIgnoreCase)) return "team";
        if (tag.Contains("driver-profile", StringComparison.OrdinalIgnoreCase)) return "profile";
        if (tag.Contains("hud-shortcut", StringComparison.OrdinalIgnoreCase)) return "hud";
        if (tag.Contains("SettingsButton", StringComparison.OrdinalIgnoreCase)) return "settings";
        if (tag.Contains("session", StringComparison.OrdinalIgnoreCase)) return "health";

        var label = Normalize(CleanLabel(button.Content?.ToString()));
        if (label.Length == 0) return null;
        if (label.Contains("inicio", StringComparison.Ordinal) || label == "home") return "home";
        if (label.Contains("naveg", StringComparison.Ordinal) || label.Contains("navigation", StringComparison.Ordinal)) return "navigation";
        if (label.Contains("multiplayer", StringComparison.Ordinal)) return "multiplayer";
        if (label == "cco" || label.Contains("despach", StringComparison.Ordinal) || label.Contains("dispatcher", StringComparison.Ordinal)) return "cco";
        if (label.Contains("rede da empresa", StringComparison.Ordinal) || label.Contains("company network", StringComparison.Ordinal) || label.Contains("unternehmensnetzwerk", StringComparison.Ordinal)) return "network";
        if (label.Contains("equipe", StringComparison.Ordinal) || label.Contains("team", StringComparison.Ordinal) || label.Contains("miembros", StringComparison.Ordinal)) return "team";
        if (label.Contains("perfil", StringComparison.Ordinal) || label.Contains("profile", StringComparison.Ordinal)) return "profile";
        if (label.Contains("empresa", StringComparison.Ordinal) || label.Contains("company", StringComparison.Ordinal)) return "company";
        if (label.Contains("hardware", StringComparison.Ordinal)) return "hardware";
        if (label == "hud" || label.Contains("head up", StringComparison.Ordinal)) return "hud";
        if (label.Contains("config", StringComparison.Ordinal) || label.Contains("settings", StringComparison.Ordinal)) return "settings";
        if (label.Contains("saude", StringComparison.Ordinal) || label.Contains("health", StringComparison.Ordinal) || label.Contains("session", StringComparison.Ordinal)) return "health";
        if (label.Contains("diagnost", StringComparison.Ordinal) || label.Contains("diagnostic", StringComparison.Ordinal)) return "diagnostics";
        if (label.Contains("manual", StringComparison.Ordinal) || label.Contains("handbuch", StringComparison.Ordinal)) return "manual";
        if (label.Contains("minimizar", StringComparison.Ordinal) || label.Contains("bandeja", StringComparison.Ordinal) || label.Contains("tray", StringComparison.Ordinal)) return "tray";
        return "grid";
    }

    private static string GeometryFor(string key) => key switch
    {
        "home" => "M3,10.5 L12,3 L21,10.5 M5.5,9.5 V21 H18.5 V9.5 M9.5,21 V14 H14.5 V21",
        "navigation" => "M12,2.5 L20,21 L12,17 L4,21 Z M12,2.5 V17",
        "multiplayer" => "M8,11 C9.657,11 11,9.657 11,8 C11,6.343 9.657,5 8,5 C6.343,5 5,6.343 5,8 C5,9.657 6.343,11 8,11 Z M2.5,21 C2.5,16.5 13.5,16.5 13.5,21 M17,11 C18.381,11 19.5,9.881 19.5,8.5 C19.5,7.119 18.381,6 17,6 M15.5,16 C19,15.5 21.5,17 21.5,21",
        "cco" => "M3,4 H21 V16 H3 Z M8,20 H16 M12,16 V20 M7,9 H10 L12,7 L14,12 L17,8",
        "company" => "M4,21 V5 H14 V21 M14,9 H20 V21 M7,8 H11 M7,12 H11 M7,16 H11 M17,12 H18 M17,16 H18",
        "network" => "M6,8 A2.5,2.5 0 1 1 6,3 A2.5,2.5 0 0 1 6,8 Z M18,8 A2.5,2.5 0 1 1 18,3 A2.5,2.5 0 0 1 18,8 Z M12,21 A2.5,2.5 0 1 1 12,16 A2.5,2.5 0 0 1 12,21 Z M8,7 L11,16 M16,7 L13,16 M8.5,5.5 H15.5",
        "team" => "M8,11 A3,3 0 1 1 8,5 A3,3 0 0 1 8,11 Z M2.5,21 C2.5,16.5 13.5,16.5 13.5,21 M17,11 A2.5,2.5 0 1 1 17,6 A2.5,2.5 0 0 1 17,11 Z M15,16 C19,15.5 21.5,17 21.5,21",
        "profile" => "M12,11 A4,4 0 1 1 12,3 A4,4 0 0 1 12,11 Z M5,21 C5,15.5 19,15.5 19,21",
        "hardware" => "M7,7 H17 V17 H7 Z M9,2 V7 M13,2 V7 M17,2 V7 M9,17 V22 M13,17 V22 M17,17 V22 M2,9 H7 M2,13 H7 M2,17 H7 M17,9 H22 M17,13 H22 M17,17 H22",
        "hud" => "M4,5 H20 V17 H4 Z M8,21 H16 M12,17 V21 M7,12 H9 L11,8 L13,14 L15,10 H17",
        "settings" => "M12,8 A4,4 0 1 1 12,16 A4,4 0 0 1 12,8 Z M12,2 V5 M12,19 V22 M2,12 H5 M19,12 H22 M4.9,4.9 L7,7 M17,17 L19.1,19.1 M19.1,4.9 L17,7 M7,17 L4.9,19.1",
        "health" => "M3,13 H7 L9,8 L12,17 L15,11 L17,13 H21 M5,5 C8,3 11,5 12,8 C13,5 16,3 19,5 C23,9 18,15 12,20 C6,15 1,9 5,5 Z",
        "diagnostics" => "M4,5 H20 V19 H4 Z M7,9 L10,12 L7,15 M12,15 H17",
        "manual" => "M4,4 C8,3 10,4 12,6 V21 C10,19 8,18 4,19 Z M20,4 C16,3 14,4 12,6 V21 C14,19 16,18 20,19 Z",
        "tray" => "M4,5 H20 V17 H4 Z M8,21 H16 M12,8 V15 M9,12 L12,15 L15,12",
        _ => "M4,4 H10 V10 H4 Z M14,4 H20 V10 H14 Z M4,14 H10 V20 H4 Z M14,14 H20 V20 H14 Z"
    };

    private static string CleanLabel(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        var split = text.IndexOf("  ", StringComparison.Ordinal);
        if (split > 0 && split <= 4 && split + 2 < text.Length)
        {
            return text[(split + 2)..].Trim();
        }
        return text;
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        return new string(decomposed
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static Border? FindCard(DependencyObject root, string title) =>
        Enumerate<Border>(root).FirstOrDefault(border =>
            border.Child is StackPanel stack &&
            stack.Children.OfType<TextBlock>().FirstOrDefault() is { } heading &&
            string.Equals(heading.Text, title, StringComparison.Ordinal));

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
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

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject => FindParent<T>(child);

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

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

    private sealed class NavLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            CleanLabel(value?.ToString());

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}