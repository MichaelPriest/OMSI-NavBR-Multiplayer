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
                    "Nenhuma instalação válida do OMSI foi encontrada. Selecione a pasta, Omsi.exe ou um atalho válido do OMSI 2.";
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

        if (result.Status is "ready")
        {
            return true;
        }

        var settings = MultiplayerSettingsStore.Load();
        var nativeIntegrationRequested =
            settings.ExperimentalPhysicalVehiclesEnabled ||
            settings.ExperimentalRoleplayCharacterEnabled;

        if (!nativeIntegrationRequested)
        {
            // Basic telemetry/UI may continue with an untracked/conflicting
            // plugin because no write-side feature will depend on it. Preserve
            // the warning so the user can repair it later.
            notice = result.Message;
            return true;
        }

        if (result.Status is "untracked")
        {
            notice =
                "Há arquivos do plugin NavBR sem manifesto confiável. " +
                "Atualize/reinstale o plugin com o OMSI fechado antes de usar ônibus físicos ou RP.";
            return false;
        }

        notice =
            $"O plugin NavBR não pôde ser preparado para ônibus físicos/RP: {result.Message ?? result.Status}. " +
            "Corrija o plugin antes de iniciar o OMSI.";
        return false;
    }
}
