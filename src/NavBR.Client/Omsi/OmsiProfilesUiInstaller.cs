using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Ghost;
using NavBR.Client.Maps;
using NavBR.Client.Windows;

namespace NavBR.Client.Omsi;

internal static class OmsiProfilesUiInstaller
{
    private const string ProfilesButtonName = "OmsiProfilesButton";
    private const string GhostButtonName = "GhostToolsButton";
    private const string RoadmapButtonName = "RoadmapStudioButton";

    public static void Install(MainWindow mainWindow)
    {
        var toolsPanel = mainWindow.FindName("Alpha11ToolsPanel") as StackPanel;
        if (toolsPanel is null)
        {
            if (mainWindow.FindName("MultiplayerButton") is not Button multiplayerButton ||
                multiplayerButton.Parent is not StackPanel fallback)
            {
                return;
            }

            toolsPanel = fallback;
        }

        if (mainWindow.FindName(RoadmapButtonName) is null)
        {
            var roadmapButton = CreateToolButton(
                "▦  Roadmap Studio",
                "Gerar whole.roadmap.bmp sem depender do OMSI Editor");
            roadmapButton.Name = RoadmapButtonName;
            roadmapButton.Click += (_, _) =>
            {
                var window = new RoadmapStudioWindow(mainWindow.GetMapsForAlpha11Tools())
                {
                    Owner = mainWindow
                };
                window.Show();
            };
            toolsPanel.Children.Add(roadmapButton);
            mainWindow.RegisterName(RoadmapButtonName, roadmapButton);
        }

        if (mainWindow.FindName(ProfilesButtonName) is null)
        {
            var profilesButton = CreateToolButton(
                "▤  Instalações OMSI",
                "Detectar, escolher e iniciar instalações/perfis do OMSI");
            profilesButton.Name = ProfilesButtonName;
            profilesButton.Click += (_, _) =>
            {
                var window = new OmsiProfilesWindow { Owner = mainWindow };
                window.ShowDialog();
            };
            toolsPanel.Children.Add(profilesButton);
            mainWindow.RegisterName(ProfilesButtonName, profilesButton);
        }

        if (mainWindow.FindName(GhostButtonName) is null)
        {
            var ghostButton = CreateToolButton(
                "◈  Ghost 3D",
                "Gravar/reproduzir viagens e validar o futuro ônibus remoto físico");
            ghostButton.Name = GhostButtonName;
            ghostButton.Click += (_, _) =>
            {
                var window = new GhostToolsWindow(mainWindow.GetCurrentTelemetryForAlpha11)
                {
                    Owner = mainWindow
                };
                window.Show();
            };
            toolsPanel.Children.Add(ghostButton);
            mainWindow.RegisterName(GhostButtonName, ghostButton);
        }
    }

    private static Button CreateToolButton(string text, string tooltip)
    {
        var shellButton = Alpha11ShellUiInstaller.CreateToolButton(text, tooltip);
        if (shellButton is not null)
        {
            return shellButton;
        }

        return new Button
        {
            Content = text,
            MinWidth = 118,
            Margin = new Thickness(0, 0, 8, 7),
            Padding = new Thickness(12, 7, 12, 7),
            Background = new SolidColorBrush(Color.FromRgb(18, 38, 56)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(55, 83, 106)),
            BorderThickness = new Thickness(1),
            ToolTip = tooltip
        };
    }
}
