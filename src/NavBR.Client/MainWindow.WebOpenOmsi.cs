using Microsoft.Win32;
using NavBR.Client.PluginInstaller;

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
        var verification =
            OpenOmsiPluginInstallationService.Verify(dialog.FileName);
        _webOpenOmsiNotice =
            $"openOMSI reconhecido. Content root: {verification.ContentRoot ?? "-"}";
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
