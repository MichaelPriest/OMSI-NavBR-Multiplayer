using System.Windows;
using NavBR.Client.Omsi;

namespace NavBR.Client;

public partial class MainWindow
{
    internal void LaunchOmsiForShell()
    {
        try
        {
            var detectedPath = _currentOmsi?.InstallDirectory;
            var profiles = OmsiInstallationProfileStore.DiscoverAndMerge(detectedPath);

            var profile = profiles
                .Where(candidate => File.Exists(candidate.ExecutablePath))
                .OrderByDescending(candidate => candidate.IsPreferred)
                .ThenByDescending(candidate => candidate.LastUsedAtUtc ?? DateTimeOffset.MinValue)
                .FirstOrDefault();

            if (profile is null)
            {
                OpenOmsiProfilesForShell(
                    "Nenhuma instalação válida do OMSI foi encontrada. Selecione ou adicione a pasta que contém Omsi.exe.");
                return;
            }

            var result = OmsiLauncherService.Launch(profile);
            StatusText.Text = result.AlreadyRunning
                ? $"OMSI já está em execução • {profile.Name}"
                : $"OMSI iniciado • {profile.Name}";
        }
        catch (Exception ex)
        {
            OpenOmsiProfilesForShell(
                $"Não foi possível iniciar o OMSI automaticamente.\n\n{ex.Message}");
        }
    }

    internal void OpenOmsiProfilesForShell(string? message = null)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            MessageBox.Show(
                this,
                message,
                "OMSI NavBR Multiplayer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        new OmsiProfilesWindow
        {
            Owner = this
        }.ShowDialog();
    }
}
