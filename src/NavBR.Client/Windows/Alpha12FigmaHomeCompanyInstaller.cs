using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Driver;
using NavBR.Client.Network;
using NavBR.Shared.Network;

namespace NavBR.Client.Windows;

/// <summary>
/// Keeps the Figma Home company card connected to the real local/company-node
/// state. No member counts or online state are invented when no live snapshot
/// is available.
/// </summary>
internal static class Alpha12FigmaHomeCompanyInstaller
{
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window) || Application.Current is not App app)
        {
            Installed.Remove(window);
            return;
        }

        var body = Enumerate<StackPanel>(window)
            .FirstOrDefault(panel =>
                panel.Children.OfType<TextBlock>().FirstOrDefault()?.Text == "EMPRESA");
        if (body is null)
        {
            Installed.Remove(window);
            return;
        }

        var texts = body.Children.OfType<TextBlock>().ToArray();
        if (texts.Length < 3)
        {
            Installed.Remove(window);
            return;
        }

        var name = texts[1];
        var detail = texts[2];

        void Refresh()
        {
            var runtime = app.NetworkRuntime;
            var membership = runtime.Membership;
            var hosted = CompanyNodeStore.LoadCompany();
            var profile = DriverProfileStore.Load();

            if (membership is not null)
            {
                name.Text = membership.CompanyName;
                detail.Text = runtime.CompanyNode.IsRunning
                    ? $"{RoleText(membership.Role)} • Company Node online neste PC"
                    : $"{RoleText(membership.Role)} • vínculo Company Network";
                detail.Foreground = runtime.CompanyNode.IsRunning
                    ? Brush(56, 201, 140)
                    : Brush(151, 171, 185);
                return;
            }

            if (hosted is not null)
            {
                name.Text = hosted.Name;
                detail.Text = runtime.CompanyNode.IsRunning
                    ? "Presidente • Company Node online neste PC"
                    : "Presidente • Company Node offline";
                detail.Foreground = runtime.CompanyNode.IsRunning
                    ? Brush(56, 201, 140)
                    : Brush(242, 184, 75);
                return;
            }

            name.Text = string.IsNullOrWhiteSpace(profile.CompanyName)
                ? "Sem empresa"
                : profile.CompanyName;
            detail.Text = string.IsNullOrWhiteSpace(profile.CompanyName)
                ? "Nenhuma Company Network vinculada"
                : "Empresa do perfil local • sem Company Network";
            detail.Foreground = Brush(151, 171, 185);
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1d) };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
        Refresh();

        window.Closed += (_, _) =>
        {
            timer.Stop();
            Installed.Remove(window);
        };
    }

    private static string RoleText(CompanyRole role) => role switch
    {
        CompanyRole.President => "Presidente",
        CompanyRole.VicePresident => "Vice-Presidente",
        CompanyRole.Director => "Diretoria",
        CompanyRole.OperationsManager => "Gerente Operacional",
        CompanyRole.Dispatcher => "CCO / Dispatcher",
        CompanyRole.Supervisor => "Fiscal / Supervisor",
        CompanyRole.SeniorDriver => "Motorista Sênior",
        CompanyRole.Driver => "Motorista",
        _ => "Aprendiz"
    };

    private static IEnumerable<T> Enumerate<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in Enumerate<T>(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
