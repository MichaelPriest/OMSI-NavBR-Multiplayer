using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NavBR.Client.Windows;

/// <summary>
/// Applies the approved Figma visual language to focused operational/system
/// windows without replacing their business logic. The styler is deliberately
/// conservative: status colors stay semantic while neutral cards and controls
/// converge on the shared Alpha.12 design tokens.
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
        "DriverProfileWindow",
        "Alpha12SettingsWindow",
        "SessionHealthWindow",
        "HudCustomizationWindow",
        "VoiceOptionsWindow",
        "OmsiProfilesWindow",
        "NatDiagnosticsWindow",
        "Alpha12ConnectivityWindow",
        "PublicRoomBrowserWindow",
        "NavBRManualWindow"
    };

    public static void Apply(Window window)
    {
        if (!SupportedWindows.Contains(window.GetType().Name) || !Applied.Add(window))
        {
            return;
        }

        ApplyWindowMetrics(window);
        window.Background = Background();
        window.Foreground = Text();
        window.FontFamily = new FontFamily("Inter, Segoe UI Variable Text, Segoe UI");

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
                    StyleListBox(listBox);
                    break;
                case CheckBox checkBox:
                    checkBox.Foreground = Text();
                    break;
                case RadioButton radioButton:
                    radioButton.Foreground = Text();
                    break;
                case Border border:
                    StyleNeutralCard(border);
                    break;
                case ProgressBar progressBar:
                    progressBar.Foreground = Accent();
                    progressBar.Background = BorderColor();
                    progressBar.BorderThickness = new Thickness(0d);
                    break;
                case TabControl tabControl:
                    tabControl.Background = Brushes.Transparent;
                    tabControl.Foreground = Text();
                    tabControl.BorderThickness = new Thickness(0d);
                    break;
                case TabItem tabItem:
                    tabItem.Foreground = Text();
                    break;
                case Expander expander:
                    expander.Foreground = Text();
                    break;
            }
        }

        window.Closed += (_, _) => Applied.Remove(window);
    }

    private static void ApplyWindowMetrics(Window window)
    {
        var target = window.GetType().Name switch
        {
            "DispatcherWindow" => (1240d, 820d),
            "VirtualCompanyWindow" => (1160d, 790d),
            "CompanyNetworkWindow" => (1160d, 790d),
            "CompanyMembersWindow" => (1080d, 760d),
            "DriverProfileWindow" => (1060d, 760d),
            "Alpha12SettingsWindow" => (1040d, 760d),
            "SessionHealthWindow" => (980d, 720d),
            "HudCustomizationWindow" => (1100d, 800d),
            "VoiceOptionsWindow" => (980d, 720d),
            "OmsiProfilesWindow" => (980d, 720d),
            "NatDiagnosticsWindow" => (1100d, 760d),
            "Alpha12ConnectivityWindow" => (1100d, 760d),
            "PublicRoomBrowserWindow" => (1180d, 800d),
            "NavBRManualWindow" => (1100d, 780d),
            _ => (window.Width, window.Height)
        };

        var workArea = SystemParameters.WorkArea;
        var maxWidth = Math.Max(720d, workArea.Width - 48d);
        var maxHeight = Math.Max(560d, workArea.Height - 48d);
        window.Width = Math.Min(target.Item1, maxWidth);
        window.Height = Math.Min(target.Item2, maxHeight);
    }

    private static void StyleTextBox(TextBox box)
    {
        box.MinHeight = Math.Max(36d, box.MinHeight);
        box.Padding = new Thickness(11d, 8d, 11d, 8d);
        box.Background = Elevated();
        box.Foreground = Text();
        box.BorderBrush = BorderColor();
        box.BorderThickness = new Thickness(1d);
        box.CaretBrush = Interaction();
        box.SelectionBrush = Accent();
    }

    private static void StylePasswordBox(PasswordBox box)
    {
        box.MinHeight = Math.Max(36d, box.MinHeight);
        box.Padding = new Thickness(11d, 8d, 11d, 8d);
        box.Background = Elevated();
        box.Foreground = Text();
        box.BorderBrush = BorderColor();
        box.BorderThickness = new Thickness(1d);
        box.CaretBrush = Interaction();
        box.SelectionBrush = Accent();
    }

    private static void StyleComboBox(ComboBox box)
    {
        box.MinHeight = Math.Max(36d, box.MinHeight);
        box.Padding = new Thickness(10d, 6d, 10d, 6d);
        box.Background = Elevated();
        box.Foreground = Text();
        box.BorderBrush = BorderColor();
        box.BorderThickness = new Thickness(1d);
    }

    private static void StyleListBox(ListBox listBox)
    {
        listBox.Background = Card();
        listBox.Foreground = Text();
        listBox.BorderBrush = BorderColor();
        listBox.BorderThickness = new Thickness(1d);
        listBox.Padding = new Thickness(4d);
    }

    private static void StyleButton(Button button)
    {
        var label = ButtonLabel(button);
        var danger = ContainsAny(label,
            "remover", "remove", "supprimer", "entfernen",
            "parar", "stop", "arrêter", "encerrar", "end room",
            "desconectar", "disconnect");
        var primary = !danger && ContainsAny(label,
            "salvar", "save", "guardar", "speichern", "enregistrer",
            "hospedar", "host company", "entrar", "join company",
            "aplicar", "apply", "criar", "create", "abrir", "open",
            "conectar", "connect", "continuar", "continue");

        button.MinHeight = Math.Max(38d, button.MinHeight);
        button.Padding = new Thickness(15d, 8d, 15d, 8d);
        button.Foreground = Brushes.White;
        button.BorderThickness = new Thickness(1d);
        button.BorderBrush = danger
            ? Error()
            : primary
                ? Interaction()
                : BorderColor();
        button.Background = danger
            ? Brush(61, 27, 32)
            : primary
                ? Accent()
                : Elevated();
        button.FontWeight = FontWeights.SemiBold;
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static void StyleNeutralCard(Border border)
    {
        if (border.Background is not SolidColorBrush background || !IsNeutralDark(background))
        {
            return;
        }

        var rounded = MaxRadius(border.CornerRadius) > 0d;
        var uniformBorder = IsUniformPositive(border.BorderThickness);
        if (!rounded && !uniformBorder)
        {
            return;
        }

        border.Background = Card();
        border.BorderBrush = BorderColor();
        border.BorderThickness = new Thickness(1d);
        border.CornerRadius = new CornerRadius(Math.Max(10d, MaxRadius(border.CornerRadius)));
    }

    private static bool IsNeutralDark(SolidColorBrush brush)
    {
        var color = brush.Color;
        var max = Math.Max(color.R, Math.Max(color.G, color.B));
        var min = Math.Min(color.R, Math.Min(color.G, color.B));
        return max <= 72 && max - min <= 34;
    }

    private static bool IsUniformPositive(Thickness thickness) =>
        thickness.Left > 0d &&
        Math.Abs(thickness.Left - thickness.Top) < 0.01d &&
        Math.Abs(thickness.Left - thickness.Right) < 0.01d &&
        Math.Abs(thickness.Left - thickness.Bottom) < 0.01d;

    private static double MaxRadius(CornerRadius radius) =>
        Math.Max(Math.Max(radius.TopLeft, radius.TopRight), Math.Max(radius.BottomLeft, radius.BottomRight));

    private static string ButtonLabel(Button button) => button.Content switch
    {
        string text => text,
        TextBlock textBlock => textBlock.Text ?? string.Empty,
        _ => button.Content?.ToString() ?? string.Empty
    };

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

    private static SolidColorBrush Background() => Brush(6, 16, 26);
    private static SolidColorBrush Card() => Brush(10, 19, 26);
    private static SolidColorBrush Elevated() => Brush(13, 26, 36);
    private static SolidColorBrush BorderColor() => Brush(28, 42, 51);
    private static SolidColorBrush Text() => Brush(218, 230, 238);
    private static SolidColorBrush Accent() => Brush(61, 137, 196);
    private static SolidColorBrush Interaction() => Brush(113, 198, 255);
    private static SolidColorBrush Error() => Brush(239, 91, 100);
    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
