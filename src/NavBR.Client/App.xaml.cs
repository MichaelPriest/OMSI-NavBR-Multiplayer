using System.Windows;
using NavBR.Client.Localization;

namespace NavBR.Client;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        LocalizationService.Initialize();
        base.OnStartup(e);
    }
}
