using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Driver;
using NavBR.Client.Localization;
using NavBR.Client.Network;
using NavBR.Shared.Network;

namespace NavBR.Client.Windows;

/// <summary>
/// Keeps the Figma Home company card connected to the real local/company-node
/// state. The original shell TextBlocks are detached so its generic refresh
/// cannot race this dedicated Company Network view.
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

        var oldTexts = body.Children.OfType<TextBlock>().Skip(1).ToArray();
        foreach (var text in oldTexts)
        {
            body.Children.Remove(text);
        }

        var name = new TextBlock
        {
            Text = T("Sem empresa", "No company", "Sin empresa", "Kein Unternehmen", "Aucune entreprise"),
            Foreground = Brushes.White,
            FontSize = 17d,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0d, 9d, 0d, 0d),
            TextWrapping = TextWrapping.Wrap
        };
        var detail = new TextBlock
        {
            Text = T("Nenhuma Company Network vinculada", "No Company Network linked", "Ninguna Company Network vinculada", "Kein Company Network verknüpft", "Aucun Company Network lié"),
            Foreground = Brush(151, 171, 185),
            FontSize = 12d,
            Margin = new Thickness(0d, 9d, 0d, 0d),
            TextWrapping = TextWrapping.Wrap
        };
        body.Children.Add(name);
        body.Children.Add(detail);

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
                    ? $"{RoleText(membership.Role)} • {T("Company Node online neste PC", "Company Node online on this PC", "Company Node online en este PC", "Company Node auf diesem PC online", "Company Node en ligne sur ce PC")}"
                    : $"{RoleText(membership.Role)} • {T("vínculo Company Network", "Company Network membership", "vínculo Company Network", "Company-Network-Verknüpfung", "liaison Company Network")}";
                detail.Foreground = runtime.CompanyNode.IsRunning
                    ? Brush(56, 201, 140)
                    : Brush(151, 171, 185);
                return;
            }

            if (hosted is not null)
            {
                name.Text = hosted.Name;
                detail.Text = runtime.CompanyNode.IsRunning
                    ? $"{T("Presidente", "President", "Presidente", "Präsident", "Président")} • {T("Company Node online neste PC", "Company Node online on this PC", "Company Node online en este PC", "Company Node auf diesem PC online", "Company Node en ligne sur ce PC")}"
                    : $"{T("Presidente", "President", "Presidente", "Präsident", "Président")} • Company Node offline";
                detail.Foreground = runtime.CompanyNode.IsRunning
                    ? Brush(56, 201, 140)
                    : Brush(242, 184, 75);
                return;
            }

            name.Text = string.IsNullOrWhiteSpace(profile.CompanyName)
                ? T("Sem empresa", "No company", "Sin empresa", "Kein Unternehmen", "Aucune entreprise")
                : profile.CompanyName;
            detail.Text = string.IsNullOrWhiteSpace(profile.CompanyName)
                ? T("Nenhuma Company Network vinculada", "No Company Network linked", "Ninguna Company Network vinculada", "Kein Company Network verknüpft", "Aucun Company Network lié")
                : T("Empresa do perfil local • sem Company Network", "Local profile company • no Company Network", "Empresa del perfil local • sin Company Network", "Unternehmen aus lokalem Profil • kein Company Network", "Entreprise du profil local • sans Company Network");
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
        CompanyRole.President => T("Presidente", "President", "Presidente", "Präsident", "Président"),
        CompanyRole.VicePresident => T("Vice-Presidente", "Vice President", "Vicepresidente", "Vizepräsident", "Vice-président"),
        CompanyRole.Director => T("Diretoria", "Director", "Dirección", "Direktion", "Direction"),
        CompanyRole.OperationsManager => T("Gerente Operacional", "Operations Manager", "Gerente Operacional", "Betriebsleiter", "Responsable des opérations"),
        CompanyRole.Dispatcher => "CCO / Dispatcher",
        CompanyRole.Supervisor => T("Fiscal / Supervisor", "Supervisor", "Fiscal / Supervisor", "Aufsicht", "Superviseur"),
        CompanyRole.SeniorDriver => T("Motorista Sênior", "Senior Driver", "Conductor Sénior", "Senior-Fahrer", "Conducteur senior"),
        CompanyRole.Driver => T("Motorista", "Driver", "Conductor", "Fahrer", "Conducteur"),
        _ => T("Aprendiz", "Trainee", "Aprendiz", "Auszubildender", "Apprenti")
    };

    private static string T(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
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
