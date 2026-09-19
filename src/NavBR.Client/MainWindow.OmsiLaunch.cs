using NavBR.Client.Omsi;
using NavBR.Client.PluginInstaller;
using NavBR.Client.Multiplayer;

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

            if (!EnsurePluginBeforeOmsiLaunch(
                    profile.InstallDirectory,
                    out var pluginNotice))
            {
                _webOmsiLaunchNotice = pluginNotice;
                NavigatePrimaryWebShell("settings-installations");
                return;
            }

            _webOmsiLaunchNotice = pluginNotice;
            _ = OmsiLauncherService.Launch(profile);
        }
        catch (Exception ex)
        {
            _webOmsiLaunchNotice =
                $"Não foi possível iniciar o OMSI automaticamente. {ex.Message}";
            NavigatePrimaryWebShell("settings-installations");
        }
    }

    private static bool EnsurePluginBeforeOmsiLaunch(
        string installDirectory,
        out string? notice)
    {
        notice = null;

        var result = OmsiPluginInstallationService.EnsureInstalledAtStartup(
            installDirectory);
        if (result.Status is "installed")
        {
            notice = "Plugin NavBR atualizado automaticamente antes de iniciar o OMSI.";
            return true;
        }

        if (result.Status is "ready" or "untracked")
        {
            return true;
        }

        var settings = MultiplayerSettingsStore.Load();
        var nativeIntegrationRequested =
            settings.ExperimentalPhysicalVehiclesEnabled ||
            settings.ExperimentalRoleplayCharacterEnabled;

        if (!nativeIntegrationRequested)
        {
            notice = result.Message;
            return true;
        }

        notice =
            $"O plugin NavBR não pôde ser preparado para ônibus físicos/RP: {result.Message ?? result.Status}. " +
            "Corrija o plugin antes de iniciar o OMSI.";
        return false;
    }
}
