using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NavBR.Client.Omsi;

internal static class OmsiProfilesUiInstaller
{
    private const string ButtonName = "OmsiProfilesButton";

    public static void Install(MainWindow mainWindow)
    {
        if (mainWindow.FindName(ButtonName) is not null ||
            mainWindow.FindName("MultiplayerButton") is not Button multiplayerButton ||
            multiplayerButton.Parent is not StackPanel parent)
        {
            return;
        }

        var button = new Button
        {
            Name = ButtonName,
            Content = "Instalações OMSI",
            MinWidth = 138,
            Height = multiplayerButton.Height > 0 ? multiplayerButton.Height : double.NaN,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 7, 12, 7),
            Background = new SolidColorBrush(Color.FromRgb(18, 38, 56)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(55, 83, 106)),
            BorderThickness = new Thickness(1),
            ToolTip = "Detectar, escolher e iniciar instalações/perfis do OMSI"
        };

        button.Click += (_, _) =>
        {
            var window = new OmsiProfilesWindow
            {
                Owner = mainWindow
            };
            window.ShowDialog();
        };

        var index = parent.Children.IndexOf(multiplayerButton);
        parent.Children.Insert(Math.Max(0, index), button);
        mainWindow.RegisterName(ButtonName, button);
    }
}
