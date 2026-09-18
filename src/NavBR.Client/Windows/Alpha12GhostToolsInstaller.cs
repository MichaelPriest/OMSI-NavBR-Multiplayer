using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Ghost;

namespace NavBR.Client.Windows;

internal static class Alpha12GhostToolsInstaller
{
    private const string ButtonTag = "alpha12-ghost-replay";
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window) ||
            window.FindName(Alpha12ProfessionalShellInstaller.ToolsPanelName) is not Panel tools)
        {
            return;
        }

        if (tools.Children.OfType<Button>().Any(button =>
                Equals(button.Tag, ButtonTag) ||
                string.Equals(button.Name, "GhostToolsButton", StringComparison.Ordinal)))
        {
            return;
        }

        var button = new Button
        {
            Tag = ButtonTag,
            Content = GhostToolsWindow.MenuText(),
            Height = 42d,
            MinWidth = 0d,
            Margin = new Thickness(0d, 0d, 0d, 6d),
            Padding = new Thickness(12d, 8d, 12d, 8d),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brush(10, 19, 26),
            Foreground = Brush(218, 230, 238),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            FontSize = 11.5d,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = GhostToolsWindow.MenuToolTip()
        };
        button.Click += (_, _) => Open(window);
        tools.Children.Add(button);
    }

    private static void Open(MainWindow owner) =>
        owner.NavigatePrimaryWebShell("ghost");

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
