using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NavBR.Client.Windows;

/// <summary>
/// Applies the approved Figma proportions to the Multiplayer page without
/// inventing room/session data. Alpha12FigmaLiveDataInstaller remains the
/// source of public-room cards and compatibility state.
/// </summary>
internal static class Alpha12FigmaMultiplayerFidelityInstaller
{
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        var roomsBody = FindCardBody(window, "SALAS");
        if (roomsBody is null || FindAncestor<Border>(roomsBody) is not Border roomsCard)
        {
            Installed.Remove(window);
            return;
        }

        if (roomsCard.Parent is Grid bodyGrid && bodyGrid.ColumnDefinitions.Count >= 3)
        {
            bodyGrid.ColumnDefinitions[0].Width = new GridLength(1d, GridUnitType.Star);
            bodyGrid.ColumnDefinitions[1].Width = new GridLength(20d);
            bodyGrid.ColumnDefinitions[2].Width = new GridLength(500d);
            bodyGrid.Margin = new Thickness(0d, 16d, 0d, 0d);

            StyleCard(roomsCard);
            var detailCard = bodyGrid.Children
                .OfType<Border>()
                .FirstOrDefault(border => !ReferenceEquals(border, roomsCard));
            if (detailCard is not null)
            {
                StyleCard(detailCard);
            }
        }

        var page = FindAncestor<ScrollViewer>(roomsBody);
        if (page?.Content is StackPanel pageStack)
        {
            StyleTabs(pageStack);
            page.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }

        // Keep the real action visually aligned with the primary button from
        // the design system. No extra/fake actions are introduced here.
        window.MultiplayerButton.MinWidth = 220d;
        window.MultiplayerButton.Height = 42d;
        window.MultiplayerButton.Padding = new Thickness(18d, 9d, 18d, 9d);
        window.MultiplayerButton.Background = Brush(61, 137, 196);
        window.MultiplayerButton.Foreground = Brushes.White;
        window.MultiplayerButton.BorderBrush = Brush(113, 198, 255);
        window.MultiplayerButton.BorderThickness = new Thickness(1d);
        window.MultiplayerButton.FontWeight = FontWeights.SemiBold;

        window.Closed += (_, _) => Installed.Remove(window);
    }

    private static void StyleTabs(StackPanel pageStack)
    {
        var tabs = pageStack.Children
            .OfType<Border>()
            .FirstOrDefault(border =>
                border.Child is StackPanel tabStack &&
                tabStack.Children.OfType<Border>().Any(tab =>
                    tab.Child is TextBlock text &&
                    string.Equals(text.Text, "Salas", StringComparison.OrdinalIgnoreCase)));

        if (tabs?.Child is not StackPanel stack)
        {
            return;
        }

        tabs.Height = 46d;
        tabs.Padding = new Thickness(6d);
        tabs.Background = Brush(13, 26, 36);
        tabs.BorderBrush = Brush(28, 42, 51);
        tabs.BorderThickness = new Thickness(1d);
        tabs.CornerRadius = new CornerRadius(10d);

        foreach (var tab in stack.Children.OfType<Border>())
        {
            tab.Padding = new Thickness(20d, 7d, 20d, 7d);
            tab.Margin = new Thickness(0d, 0d, 4d, 0d);
            tab.CornerRadius = new CornerRadius(7d);
            if (tab.Child is TextBlock text)
            {
                var selected = string.Equals(text.Text, "Salas", StringComparison.OrdinalIgnoreCase);
                tab.Background = selected ? Brush(16, 38, 56) : Brushes.Transparent;
                text.Foreground = selected ? Brush(218, 230, 238) : Brush(151, 171, 185);
                text.FontSize = 12d;
                text.FontWeight = FontWeights.SemiBold;
            }
        }
    }

    private static void StyleCard(Border card)
    {
        card.Margin = new Thickness(0d);
        card.Background = Brush(10, 19, 26);
        card.BorderBrush = Brush(28, 42, 51);
        card.BorderThickness = new Thickness(1d);
        card.CornerRadius = new CornerRadius(12d);
        card.VerticalAlignment = VerticalAlignment.Stretch;
    }

    private static StackPanel? FindCardBody(DependencyObject root, string title)
    {
        return Enumerate<Border>(root)
            .Select(border => border.Child as StackPanel)
            .FirstOrDefault(stack => stack is not null &&
                stack.Children.OfType<TextBlock>().FirstOrDefault()?.Text.Equals(title, StringComparison.OrdinalIgnoreCase) == true);
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
