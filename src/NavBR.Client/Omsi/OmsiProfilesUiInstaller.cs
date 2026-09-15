using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Ghost;

namespace NavBR.Client.Omsi;

internal static class OmsiProfilesUiInstaller
{
    private const string ProfilesButtonName = "OmsiProfilesButton";
    private const string GhostButtonName = "GhostToolsButton";

    public static void Install(MainWindow mainWindow)
    {
        if (mainWindow.FindName("MultiplayerButton") is not Button multiplayerButton ||
            multiplayerButton.Parent is not StackPanel parent)
        {
            return;
        }

        var insertIndex = Math.Max(0, parent.Children.IndexOf(multiplayerButton));

        if (mainWindow.FindName(ProfilesButtonName) is null)
        {
            var profilesButton = CreateHeaderButton(
                ProfilesButtonName,
                "Instalações OMSI",
                "Detectar, escolher e iniciar instalações/perfis do OMSI",
                multiplayerButton);
            profilesButton.Click += (_, _) =>
            {
                var window = new OmsiProfilesWindow { Owner = mainWindow };
                window.ShowDialog();
            };
            parent.Children.Insert(insertIndex++, profilesButton);
            mainWindow.RegisterName(ProfilesButtonName, profilesButton);
        }

        if (mainWindow.FindName(GhostButtonName) is null)
        {
            var ghostButton = CreateHeaderButton(
                GhostButtonName,
                "Ghost 3D",
                "Gravar/reproduzir viagens e validar o futuro ônibus remoto físico",
                multiplayerButton);
            ghostButton.Click += (_, _) =>
            {
                var window = new GhostToolsWindow(mainWindow.GetCurrentTelemetryForAlpha11)
                {
                    Owner = mainWindow
                };
                window.Show();
            };
            parent.Children.Insert(insertIndex, ghostButton);
            mainWindow.RegisterName(GhostButtonName, ghostButton);
        }
    }

    private static Button CreateHeaderButton(
        string name,
        string text,
        string tooltip,
        Button reference)
    {
        return new Button
        {
            Name = name,
            Content = text,
            MinWidth = 118,
            Height = reference.Height > 0 ? reference.Height : double.NaN,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 7, 12, 7),
            Background = new SolidColorBrush(Color.FromRgb(18, 38, 56)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(55, 83, 106)),
            BorderThickness = new Thickness(1),
            ToolTip = tooltip
        };
    }
}
