using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;

namespace NavBR.Client.Driver;

internal static class DriverTripHistoryInstaller
{
    private const string ButtonTag = "alpha12-driver-trip-history";
    private static readonly HashSet<DriverProfileWindow> Installed = new();

    public static void Attach(DriverProfileWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        window.Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => Install(window)));
        window.Closed += (_, _) => Installed.Remove(window);
    }

    private static void Install(DriverProfileWindow window)
    {
        if (Enumerate<Button>(window).Any(button => Equals(button.Tag, ButtonTag)))
        {
            return;
        }

        var saveButton = Enumerate<Button>(window)
            .FirstOrDefault(button => button.Height == 42d && button.MinWidth >= 170d);
        if (saveButton?.Parent is not StackPanel actions || actions.Orientation != Orientation.Horizontal)
        {
            return;
        }

        var historyButton = new Button
        {
            Tag = ButtonTag,
            Content = T("Histórico", "History", "Historial", "Verlauf", "Historique"),
            Height = 42d,
            MinWidth = 112d,
            Margin = new Thickness(0d, 0d, 8d, 0d),
            Padding = new Thickness(15d, 8d, 15d, 8d),
            Background = Brush(13, 26, 36),
            Foreground = Brush(218, 230, 238),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = T(
                "Abrir o histórico local de viagens reais detectadas pelo NavBR.",
                "Open the local history of real trips detected by NavBR.",
                "Abrir el historial local de viajes reales detectados por NavBR.",
                "Lokalen Verlauf der von NavBR erkannten echten Fahrten öffnen.",
                "Ouvrir l’historique local des trajets réels détectés par NavBR.")
        };
        historyButton.Click += (_, _) =>
        {
            if (window.Owner is MainWindow owner)
            {
                window.Close();
                owner.NavigatePrimaryWebShell("operations");
            }
        };
        actions.Children.Insert(0, historyButton);
    }

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

    private static string T(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
