using System.Diagnostics;
using Microsoft.Win32;
using NavBR.Client.PluginInstaller;
using NavBR.Shared.OpenOmsi;

namespace NavBR.Client;

public partial class MainWindow
{
    private void SelectOpenOmsiExecutableFromWeb()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecione o openomsi.exe",
            Filter = "openOMSI (openomsi.exe)|openomsi.exe|Executáveis (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        var owner = GetPrimaryWebDialogOwner();
        if ((owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) != true)
        {
            return;
        }

        if (!string.Equals(
                Path.GetFileName(dialog.FileName),
                "openomsi.exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Selecione o executável openomsi.exe.");
        }

        OpenOmsiPluginInstallationService.SavePreferredExecutable(
            dialog.FileName);
        RefreshOpenOmsiVehicleIdentityRoots();
        var verification =
            OpenOmsiPluginInstallationService.Verify(dialog.FileName);
        _webOpenOmsiNotice =
            $"openOMSI reconhecido. Content root: {verification.ContentRoot ?? "-"}";
    }

    private void LaunchOpenOmsiWithNavBrGatewayFromWeb()
    {
        if (OpenOmsiPluginInstallationService.IsOpenOmsiRunning())
        {
            throw new InvalidOperationException(
                "O openOMSI já está em execução. Feche-o antes de iniciar uma nova sessão ligada ao gateway NavBR.");
        }

        RefreshOpenOmsiVehicleIdentityRoots();
        OpenOmsiLanGateway.Shared.Start();
        var status = OpenOmsiLanGateway.Shared.GetStatus();
        if (status.Port is not int port)
        {
            throw new InvalidOperationException(
                "O gateway LAN v6 do NavBR não conseguiu abrir uma porta local.");
        }

        var verification =
            OpenOmsiPluginInstallationService.Verify();
        if (string.IsNullOrWhiteSpace(verification.ExecutablePath) ||
            !File.Exists(verification.ExecutablePath))
        {
            throw new FileNotFoundException(
                "openomsi.exe não foi localizado. Selecione o executável do openOMSI primeiro.");
        }

        var executable = verification.ExecutablePath;
        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory =
                Path.GetDirectoryName(executable) ??
                Environment.CurrentDirectory,
            UseShellExecute = true
        };
        start.ArgumentList.Add("--lan-join");
        start.ArgumentList.Add($"127.0.0.1:{port}");

        _ = Process.Start(start)
            ?? throw new InvalidOperationException(
                "O Windows não iniciou o openOMSI.");

        _webOpenOmsiNotice =
            $"openOMSI iniciado e apontado ao gateway NavBR em 127.0.0.1:{port}.";
    }

    private void VerifyOpenOmsiPluginFromWeb()
    {
        var verification =
            OpenOmsiPluginInstallationService.Verify();
        _webOpenOmsiNotice = verification.Status == "ready"
            ? "NavBR for openOMSI verificado: plugin x64 e manifesto estão em dia."
            : verification.Message ??
              $"Verificação openOMSI: {verification.Status}.";
    }

    private void InstallOpenOmsiPluginFromWeb()
    {
        var result =
            OpenOmsiPluginInstallationService.InstallOrUpdate();
        var verification =
            OpenOmsiPluginInstallationService.Verify(
                result.ExecutablePath);
        if (verification.Status != "ready")
        {
            throw new InvalidOperationException(
                $"O plugin foi gravado, mas a verificação final retornou '{verification.Status}'.");
        }

        _webOpenOmsiNotice =
            $"NavBR for openOMSI instalado/atualizado em {result.PluginDirectory}. Reinicie o openOMSI para carregar o plugin.";
    }

    private void RemoveOpenOmsiPluginFromWeb()
    {
        var removed =
            OpenOmsiPluginInstallationService.Remove();
        _webOpenOmsiNotice =
            $"NavBR for openOMSI removido ({removed} arquivo(s)).";
    }
}
