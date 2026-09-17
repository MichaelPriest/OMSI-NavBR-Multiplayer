using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NavBR.Client.Windows;

internal static class Alpha12Navigation3DInstaller
{
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        if (window.GpsHeadingText.Parent is not StackPanel headingStack ||
            VisualTreeHelper.GetParent(headingStack) is not Grid headerGrid)
        {
            return;
        }

        var actions = headerGrid.Children.OfType<WrapPanel>().FirstOrDefault();
        if (actions is null)
        {
            return;
        }

        var button = new Button
        {
            Content = "3D",
            MinWidth = 54d,
            Margin = new Thickness(5d, 0d, 0d, 0d),
            Padding = new Thickness(10d, 5d, 10d, 5d),
            ToolTip = "Mapa 3D com rota ativa e ônibus dos jogadores online"
        };
        button.Click += (_, _) => window.OpenNavigation3D();
        actions.Children.Add(button);

        window.Closed += (_, _) => Installed.Remove(window);
    }
}
