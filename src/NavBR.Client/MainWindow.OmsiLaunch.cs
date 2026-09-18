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
                _webOmsiLaunchNotice =
                    "Nenhuma instalação válida do OMSI foi encontrada. Selecione ou adicione a pasta que contém Omsi.exe.";
                NavigatePrimaryWebShell("settings-installations");
                return;
            }

            _webOmsiLaunchNotice = null;
            var result = OmsiLauncherService.Launch(profile);
            StatusText.Text = result.AlreadyRunning
                ? $"OMSI já está em execução • {profile.Name}"
                : $"OMSI iniciado • {profile.Name}";
        }
        catch (Exception ex)
        {
            _webOmsiLaunchNotice =
                $"Não foi possível iniciar o OMSI automaticamente. {ex.Message}";
            NavigatePrimaryWebShell("settings-installations");
        }
    }
}
