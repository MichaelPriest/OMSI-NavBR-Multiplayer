using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;

namespace NavBR.Client.Operations;

internal static class DispatcherInstaller
{
    private const string ButtonTag = "alpha12-dispatcher";
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        var button = CreateButton(window);
        ApplyLocalization(button);
        SelectionChangedEventHandler languageChanged = (_, _) => ApplyLocalization(button);
        window.LanguageComboBox.SelectionChanged += languageChanged;
        window.Closed += (_, _) =>
        {
            window.LanguageComboBox.SelectionChanged -= languageChanged;
            Installed.Remove(window);
        };
    }

    private static Button CreateButton(MainWindow window)
    {
        var existing = FindButtons(window).FirstOrDefault(button => button.Tag as string == ButtonTag);
        if (existing is not null)
        {
            return existing;
        }

        var button = new Button { Tag = ButtonTag };
        StyleButton(button);
        button.Click += (_, _) =>
        {
            var dispatcher = new DispatcherWindow(window, window.GetCurrentTelemetryForAlpha11);
            DispatcherRemoteDriversPanel.Attach(dispatcher);
            dispatcher.ShowDialog();
        };

        var companyButton = FindButtons(window).FirstOrDefault(candidate =>
            candidate.Tag as string == "alpha12-company-fleet");
        if (companyButton?.Parent is Panel panel)
        {
            var index = panel.Children.IndexOf(companyButton);
            panel.Children.Insert(Math.Min(panel.Children.Count, index + 1), button);
        }
        else if (window.LanguageLabelText.Parent is Panel footer)
        {
            footer.Children.Insert(0, button);
        }

        return button;
    }

    private static void ApplyLocalization(Button button) =>
        button.Content = DispatcherWindow.MenuText();

    private static IEnumerable<Button> FindButtons(DependencyObject root)
    {
        if (root is Button button)
        {
            yield return button;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in FindButtons(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }

    private static void StyleButton(Button button)
    {
        button.Height = 44d;
        button.MinWidth = 0d;
        button.Margin = new Thickness(0d, 0d, 0d, 6d);
        button.Padding = new Thickness(13d, 9d, 13d, 9d);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.VerticalAlignment = VerticalAlignment.Center;
        button.Background = new SolidColorBrush(Color.FromRgb(10, 19, 25));
        button.Foreground = new SolidColorBrush(Color.FromRgb(218, 230, 238));
        button.BorderBrush = new SolidColorBrush(Color.FromRgb(28, 42, 51));
        button.BorderThickness = new Thickness(1d);
        button.FontSize = 12.5d;
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }
}
