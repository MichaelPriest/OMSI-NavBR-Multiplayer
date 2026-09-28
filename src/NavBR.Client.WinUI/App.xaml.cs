using Microsoft.UI.Xaml;

namespace NavBR.Client.WinUI;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var smokeOnly = Environment
            .GetCommandLineArgs()
            .Any(argument => string.Equals(
                argument,
                "--xaml-smoke",
                StringComparison.OrdinalIgnoreCase));

        try
        {
            _window = new MainWindow(smokeOnly);
            if (smokeOnly)
            {
                Environment.Exit(0);
                return;
            }

            _window.Activate();
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex);
            if (smokeOnly)
            {
                Environment.Exit(86);
                return;
            }

            throw;
        }
    }
}

internal static class StartupLog
{
    public static void Write(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OMSI NavBR Multiplayer");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "navbr-winui.log"),
                $"[{DateTimeOffset.Now:O}] {exception}\r\n\r\n");
        }
        catch
        {
        }
    }
}
