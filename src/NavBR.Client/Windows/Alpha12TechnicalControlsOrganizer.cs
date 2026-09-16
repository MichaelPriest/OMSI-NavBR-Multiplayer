using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NavBR.Client.Windows;

internal static class Alpha12TechnicalControlsOrganizer
{
    private const string ToolsPanelName = "Alpha11ToolsPanel";
    private static readonly HashSet<MainWindow> Attached = new();

    public static void Attach(MainWindow window)
    {
        if (!Attached.Add(window))
        {
            return;
        }

        window.LayoutUpdated += (_, _) => MoveInjectedTechnicalButtons(window);
        MoveInjectedTechnicalButtons(window);
    }

    private static void MoveInjectedTechnicalButtons(MainWindow window)
    {
        if (window.FindName(ToolsPanelName) is not Panel toolsPanel ||
            window.MultiplayerButton.Parent is not Panel multiplayerHost)
        {
            return;
        }

        // The legacy HUD code still injects its settings buttons next to the
        // Multiplayer button. Alpha.12 keeps that compatibility, but moves any
        // injected siblings into the opt-in advanced tools area so beginners do
        // not see maintenance controls in the primary navigation flow.
        var technicalButtons = multiplayerHost.Children
            .OfType<Button>()
            .Where(button => !ReferenceEquals(button, window.MultiplayerButton))
            .ToArray();

        foreach (var button in technicalButtons)
        {
            multiplayerHost.Children.Remove(button);
            StyleAsAdvancedTool(button);
            toolsPanel.Children.Add(button);
        }
    }

    private static void StyleAsAdvancedTool(Button button)
    {
        button.Height = 42d;
        button.MinWidth = 0d;
        button.Margin = new Thickness(0d, 0d, 0d, 6d);
        button.Padding = new Thickness(12d, 8d, 12d, 8d);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.VerticalAlignment = VerticalAlignment.Center;
        button.Background = Brush(10, 19, 25);
        button.Foreground = Brush(205, 219, 228);
        button.BorderBrush = Brush(35, 49, 58);
        button.BorderThickness = new Thickness(1d);
        button.FontSize = 11.5d;
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
