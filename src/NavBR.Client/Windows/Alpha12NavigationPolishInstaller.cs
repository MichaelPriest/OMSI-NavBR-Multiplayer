using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NavBR.Client.Windows;

internal static class Alpha12NavigationPolishInstaller
{
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        // The professional Alpha.12 shell already owns the information
        // architecture (DIRIGIR / OPERAÇÃO / SISTEMA / AVANÇADO). Do not
        // re-parent its buttons: older polish logic used to flatten CCO,
        // profile, company, hardware and settings beside Multiplayer, which
        // conflicts with the approved Figma navigation model.
        var hasProfessionalShell =
            window.FindName(Alpha12ProfessionalShellInstaller.OperationsPanelName) is Panel &&
            window.FindName(Alpha12ProfessionalShellInstaller.SystemPanelName) is Panel;

        if (hasProfessionalShell)
        {
            HideNextVersionLabel(window);
            Alpha12Navigation3DInstaller.Install(window);
            window.Closed += (_, _) => Installed.Remove(window);
            return;
        }

        // Legacy fallback kept for older/alternate shells.
        var buttons = Enumerate<Button>(window).ToArray();
        var home = FindByTagOrPrefix(buttons, "alpha12-text:Home", "⌂");
        var multiplayer = FindByTagOrPrefix(buttons, "alpha12-text:MultiplayerMenu", "●");
        var hardware = FindByTagOrPrefix(buttons, "alpha12-text:HardwareMenu", "▣");
        var driver = buttons.FirstOrDefault(button => Equals(button.Tag, "alpha12-driver-profile"));
        var company = buttons.FirstOrDefault(button => Equals(button.Tag, "alpha12-company-fleet"));
        var dispatcher = buttons.FirstOrDefault(button => Equals(button.Tag, "alpha12-dispatcher"));
        var settings = buttons.FirstOrDefault(button => Equals(button.Tag, "alpha12-text:SettingsButton"));
        var diagnostics = FindByTagOrPrefix(buttons, "alpha12-text:TechnicalDiagnostics", "◫");
        var features = FindByTagOrPrefix(buttons, "alpha12-text:FeaturesMenu", "✦");

        if (home?.Parent is not Panel mainPanel || multiplayer is null)
        {
            Alpha12Navigation3DInstaller.Install(window);
            window.Closed += (_, _) => Installed.Remove(window);
            return;
        }

        var advancedPanel = diagnostics?.Parent as Panel;

        var dailyButtons = new[]
        {
            dispatcher,
            driver,
            company,
            hardware,
            settings,
            diagnostics
        }.Where(button => button is not null).Cast<Button>().ToArray();

        foreach (var button in dailyButtons)
        {
            Detach(button);
        }

        var insertIndex = mainPanel.Children.IndexOf(multiplayer) + 1;
        foreach (var button in dailyButtons)
        {
            button.Margin = new Thickness(0d, 0d, 0d, 6d);
            mainPanel.Children.Insert(Math.Min(insertIndex++, mainPanel.Children.Count), button);
        }

        if (features is not null && advancedPanel is not null)
        {
            Detach(features);
            features.Margin = new Thickness(0d, 0d, 0d, 6d);
            advancedPanel.Children.Insert(0, features);
            HideNextVersionLabel(window);
        }

        Alpha12Navigation3DInstaller.Install(window);
        window.Closed += (_, _) => Installed.Remove(window);
    }

    private static Button? FindByTagOrPrefix(
        IEnumerable<Button> buttons,
        string tag,
        string prefix)
    {
        return buttons.FirstOrDefault(button => Equals(button.Tag, tag))
            ?? buttons.FirstOrDefault(button =>
                button.Content is string text && text.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static void HideNextVersionLabel(DependencyObject root)
    {
        foreach (var text in Enumerate<TextBlock>(root))
        {
            if (Equals(text.Tag, "alpha12-text:NextVersion") ||
                text.Text.Equals("PRÓXIMA VERSÃO", StringComparison.OrdinalIgnoreCase) ||
                text.Text.Equals("NEXT VERSION", StringComparison.OrdinalIgnoreCase))
            {
                text.Visibility = Visibility.Collapsed;
            }
        }
    }

    private static void Detach(UIElement element)
    {
        var parent = VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element);
        switch (parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, element):
                decorator.Child = null;
                break;
            case ContentControl content when ReferenceEquals(content.Content, element):
                content.Content = null;
                break;
        }
    }

    private static IEnumerable<T> Enumerate<T>(DependencyObject root)
        where T : DependencyObject
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
}
