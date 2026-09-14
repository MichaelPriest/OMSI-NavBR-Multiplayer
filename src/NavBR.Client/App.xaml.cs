using System.IO;
using System.Windows;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Windows;

namespace NavBR.Client;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        LocalizationService.Initialize();
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(Window_Loaded));
        base.OnStartup(e);
    }

    private static void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window)
        {
            WindowsThemeService.ApplyDarkTitleBar(window);
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OMSI NavBR Multiplayer");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "navbr-error.log"),
                $"[{DateTimeOffset.Now:O}] {e.Exception}\n\n");
        }
        catch
        {
            // Logging must never replace the original failure.
        }

        MessageBox.Show(
            $"O NavBR encontrou um erro e evitou o fechamento completo do aplicativo.\n\n{e.Exception.Message}\n\nDetalhes foram registrados em %LOCALAPPDATA%\\OMSI NavBR Multiplayer\\navbr-error.log.",
            "OMSI NavBR Multiplayer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
