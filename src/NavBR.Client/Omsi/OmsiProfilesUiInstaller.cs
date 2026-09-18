using System.Windows.Controls;
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
            if (mainWindow.FindName("MultiplayerButton") is not Button multiplayerButton || multiplayerButton.Parent is not StackPanel fallback)
            {
                return;
            }
            toolsPanel = fallback;
        }

        if (mainWindow.FindName(RoadmapButtonName) is null)
        {
            var roadmapButton = Alpha11ShellUiInstaller.CreateToolButton("▦  Roadmap Studio", "Gerar whole.roadmap.bmp sem depender do OMSI Editor");
            roadmapButton.Name = RoadmapButtonName;
            roadmapButton.Click += (_, _) => mainWindow.NavigatePrimaryWebShell("settings-roadmap");
            toolsPanel.Children.Add(roadmapButton);
            mainWindow.RegisterName(RoadmapButtonName, roadmapButton);
        }

        if (mainWindow.FindName(ProfilesButtonName) is null)
        {
            var profilesButton = Alpha11ShellUiInstaller.CreateToolButton("▤  Instalações OMSI", "Detectar, escolher e iniciar instalações/perfis do OMSI");
            profilesButton.Name = ProfilesButtonName;
            profilesButton.Click += (_, _) => mainWindow.NavigatePrimaryWebShell("settings-installations");
            toolsPanel.Children.Add(profilesButton);
            mainWindow.RegisterName(ProfilesButtonName, profilesButton);
        }

        if (mainWindow.FindName(GhostButtonName) is null)
        {
            var ghostButton = Alpha11ShellUiInstaller.CreateToolButton("◈  Ghost 3D", "Gravar/reproduzir viagens e validar o futuro ônibus remoto físico");
            ghostButton.Name = GhostButtonName;
            ghostButton.Click += (_, _) => mainWindow.NavigatePrimaryWebShell("ghost");
            toolsPanel.Children.Add(ghostButton);
            mainWindow.RegisterName(GhostButtonName, ghostButton);
        }
    }
}
