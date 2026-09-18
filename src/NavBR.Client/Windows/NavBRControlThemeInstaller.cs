using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace NavBR.Client.Windows;

/// <summary>
/// Enforces NavBR control styles after window composition. Several Alpha.14
/// surfaces create or move ComboBox controls from code, so relying only on an
/// implicit WPF style can let the Windows default light template leak back in.
/// </summary>
internal static class NavBRControlThemeInstaller
{
    private static readonly HashSet<Window> Attached = new();

    public static void Attach(Window window)
    {
        if (!Attached.Add(window))
        {
            Apply(window);
            return;
        }

        Apply(window);

        window.ContentRendered += Window_ContentRendered;
        window.Closed += Window_Closed;

        _ = window.Dispatcher.BeginInvoke(
            new Action(() => Apply(window)),
            DispatcherPriority.ContextIdle);
    }

    private static void Window_ContentRendered(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            Apply(window);
            _ = window.Dispatcher.BeginInvoke(
                new Action(() => Apply(window)),
                DispatcherPriority.ContextIdle);
        }
    }

    private static void Window_Closed(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.ContentRendered -= Window_ContentRendered;
        window.Closed -= Window_Closed;
        Attached.Remove(window);
    }

    private static void Apply(Window window)
    {
        if (Application.Current is null)
        {
            return;
        }

        var comboStyle = Application.Current.TryFindResource("NavComboBoxStyle") as Style;
        var itemStyle = Application.Current.TryFindResource("NavComboBoxItemStyle") as Style;

        foreach (var combo in Enumerate<ComboBox>(window))
        {
            if (comboStyle is not null && !ReferenceEquals(combo.Style, comboStyle))
            {
                combo.Style = comboStyle;
            }

            if (itemStyle is not null)
            {
                combo.ItemContainerStyle = itemStyle;
            }

            combo.Background = Brush(13, 26, 36);
            combo.Foreground = Brush(218, 230, 238);
            combo.BorderBrush = Brush(28, 42, 51);
            combo.BorderThickness = new Thickness(1d);
            combo.DropDownOpened -= Combo_DropDownOpened;
            combo.DropDownOpened += Combo_DropDownOpened;
        }
    }

    private static void Combo_DropDownOpened(object? sender, EventArgs e)
    {
        if (sender is not ComboBox combo)
        {
            return;
        }

        var itemStyle = Application.Current?.TryFindResource("NavComboBoxItemStyle") as Style;
        if (itemStyle is not null)
        {
            combo.ItemContainerStyle = itemStyle;
        }

        for (var index = 0; index < combo.Items.Count; index++)
        {
            if (combo.ItemContainerGenerator.ContainerFromIndex(index) is ComboBoxItem item)
            {
                if (itemStyle is not null)
                {
                    item.Style = itemStyle;
                }

                item.Background = Brush(10, 19, 26);
                item.Foreground = Brush(218, 230, 238);
            }
        }
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

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
