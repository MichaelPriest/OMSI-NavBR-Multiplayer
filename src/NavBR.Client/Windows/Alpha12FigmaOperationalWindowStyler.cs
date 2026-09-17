using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NavBR.Client.Windows;

/// <summary>
/// Applies the approved Figma visual language to the operational windows that
/// still live as focused dialogs. The existing business logic/layout remains
/// untouched; this layer only normalizes visual chrome and control styling.
/// </summary>
internal static class Alpha12FigmaOperationalWindowStyler
{
    private static readonly HashSet<Window> Applied = new();
    private static readonly HashSet<string> SupportedWindows = new(StringComparer.Ordinal)
    {
        "DispatcherWindow",
        "VirtualCompanyWindow",
        "CompanyNetworkWindow",
        "CompanyMembersWindow",
        "DriverProfileWindow"
    };

    public static void Apply(Window window)
    {
        if (!SupportedWindows.Contains(window.GetType().Name) || !Applied.Add(window))
        {
            return;
        }

        window.Background = Brush(6, 16, 26);
        window.Foreground = Brush(218, 230, 238);
        window.FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

        foreach (var element in Enumerate(window))
        {
            switch (element)
            {
                case TextBox textBox:
                    StyleTextBox(textBox);
                    break;
                case PasswordBox passwordBox:
                    StylePasswordBox(passwordBox);
                    break;
                case ComboBox comboBox:
                    StyleComboBox(comboBox);
                    break;
                case Button button:
                    StyleButton(button);
                    break;
                case ListBox listBox:
                    listBox.Background = Elevated();
                    listBox.Foreground = Text();
                    listBox.BorderBrush = Border();
                    listBox.BorderThickness = new Thickness(1d);
                    break;
                case CheckBox checkBox:
                    checkBox.Foreground = Text();
                    break;
            }
        }

        window.Closed += (_, _) => Applied.Remove(window);
    }

    private static void StyleTextBox(TextBox box)
    {
        box.MinHeight = Math.Max(36d, box.MinHeight);
        box.Padding = new Thickness(11d, 8d, 11d, 8d);
        box.Background = Elevated();
        box.Foreground = Text();
        box.BorderBrush = Border();
        box.BorderThickness = new Thickness(1d);
        box.CaretBrush = Accent();
        box.SelectionBrush = Brush(61, 137, 196);
    }

    private static void StylePasswordBox(PasswordBox box)
    {
        box.MinHeight = Math.Max(36d, box.MinHeight);
        box.Padding = new Thickness(11d, 8d, 11d, 8d);
        box.Background = Elevated();
        box.Foreground = Text();
        box.BorderBrush = Border();
        box.BorderThickness = new Thickness(1d);
        box.CaretBrush = Accent();
        box.SelectionBrush = Brush(61, 137, 196);
    }

    private static void StyleComboBox(ComboBox box)
    {
        box.MinHeight = Math.Max(36d, box.MinHeight);
        box.Padding = new Thickness(10d, 6d, 10d, 6d);
        box.Background = Elevated();
        box.Foreground = Text();
        box.BorderBrush = Border();
        box.BorderThickness = new Thickness(1d);
    }

    private static void StyleButton(Button button)
    {
        var label = button.Content as string ?? string.Empty;
        var danger = ContainsAny(label,
            "remover", "remove", "supprimer", "entfernen",
            "parar", "stop", "arrêter", "encerrar", "end room");
        var primary = !danger && ContainsAny(label,
            "salvar", "save", "guardar", "speichern", "enregistrer",
            "hospedar", "host company", "entrar", "join company",
            "aplicar", "apply", "criar", "create", "abrir", "open");

        button.MinHeight = Math.Max(38d, button.MinHeight);
        button.Padding = new Thickness(15d, 8d, 15d, 8d);
        button.Foreground = Brushes.White;
        button.BorderThickness = new Thickness(1d);
        button.BorderBrush = danger
            ? Brush(138, 55, 65)
            : primary
                ? Brush(113, 198, 255)
                : Border();
        button.Background = danger
            ? Brush(62, 23, 28)
            : primary
                ? Brush(61, 137, 196)
                : Elevated();
        button.FontWeight = FontWeights.SemiBold;
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (value.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<DependencyObject> Enumerate(DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in Enumerate(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }

    private static SolidColorBrush Elevated() => Brush(13, 26, 36);
    private static SolidColorBrush Border() => Brush(28, 42, 51);
    private static SolidColorBrush Text() => Brush(218, 230, 238);
    private static SolidColorBrush Accent() => Brush(113, 198, 255);
    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
