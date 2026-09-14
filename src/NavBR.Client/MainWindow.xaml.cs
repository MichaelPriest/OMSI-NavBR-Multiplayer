using System.Windows;
using NavBR.Client.Omsi;

namespace NavBR.Client;

public partial class MainWindow : Window
{
    private readonly OmsiProcessDetector _detector = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshOmsiStatus();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshOmsiStatus();
    }

    private void RefreshOmsiStatus()
    {
        var instances = _detector.FindRunningInstances();

        if (instances.Count == 0)
        {
            StatusText.Text = "OMSI não está em execução.";
            InstallPathText.Text = "Abra o OMSI 2 e clique em Detectar novamente.";
            ProcessDetailsText.Text = "Nenhum processo Omsi.exe encontrado.";
            return;
        }

        var omsi = instances[0];
        StatusText.Text = "OMSI detectado";
        InstallPathText.Text = omsi.InstallDirectory;
        ProcessDetailsText.Text =
            $"PID: {omsi.ProcessId}\n" +
            $"Versão: {omsi.FileVersion}\n" +
            $"Executável: {omsi.ExecutablePath}";
    }
}
