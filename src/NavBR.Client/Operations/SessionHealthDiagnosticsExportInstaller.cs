using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using NavBR.Client.Localization;

namespace NavBR.Client.Operations;

internal sealed record SessionHealthSanitizedReport(
    string Schema,
    int Version,
    DateTimeOffset ExportedAtUtc,
    IReadOnlyDictionary<string, string> Metrics,
    string PrivacyNotice);

/// <summary>
/// Exports only the aggregate values already displayed by Session Health.
/// Room identifiers, passwords, tokens, PlayerIds, IP addresses and local paths
/// are deliberately outside this report schema.
/// </summary>
internal static class SessionHealthDiagnosticsExportInstaller
{
    private const string Schema = "navbr-session-health";
    private const int Version = 1;
    private static readonly HashSet<SessionHealthWindow> Installed = new();

    public static void Attach(SessionHealthWindow window)
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

    private static void Install(SessionHealthWindow window)
    {
        var scroller = Enumerate<ScrollViewer>(window).FirstOrDefault();
        if (scroller?.Content is not StackPanel body)
        {
            return;
        }

        var button = new Button
        {
            Content = Text("Export"),
            Height = 40d,
            MinWidth = 210d,
            Margin = new Thickness(0d, 12d, 0d, 0d),
            Padding = new Thickness(16d, 8d, 16d, 8d),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = Brush(13, 26, 36),
            Foreground = Brush(218, 230, 238),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += (_, _) => Export(window);
        body.Children.Add(button);
    }

    private static void Export(SessionHealthWindow window)
    {
        var report = Capture(window);
        var dialog = new SaveFileDialog
        {
            Title = Text("ExportTitle"),
            Filter = "NavBR Session Health (*.navbr-health.json)|*.navbr-health.json|JSON (*.json)|*.json",
            FileName = $"navbr-session-health-{DateTime.Now:yyyyMMdd-HHmmss}.navbr-health.json",
            AddExtension = true,
            DefaultExt = ".json",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(window) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(
                dialog.FileName,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            MessageBox.Show(window, Text("ExportSuccess"), Text("Title"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                window,
                $"{Text("ExportError")}\n\n{ex.Message}",
                Text("Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static SessionHealthSanitizedReport Capture(SessionHealthWindow window)
    {
        var metrics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stack in Enumerate<StackPanel>(window))
        {
            if (stack.Children.Count < 2 ||
                stack.Children[0] is not TextBlock label ||
                label.Tag is not string tag ||
                !tag.StartsWith("health-label:", StringComparison.Ordinal) ||
                stack.Children[1] is not TextBlock value)
            {
                continue;
            }

            var key = tag["health-label:".Length..];
            metrics[key] = string.IsNullOrWhiteSpace(value.Text) ? "—" : value.Text.Trim();
        }

        return new SessionHealthSanitizedReport(
            Schema,
            Version,
            DateTimeOffset.UtcNow,
            metrics,
            "Contains only aggregate Session Health values visible in NavBR. No room password, token, room id, PlayerId, IP address or local filesystem path is exported.");
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

    private static string Text(string key)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        var table = language switch
        {
            "pt" => Pt,
            "es" => Es,
            "de" => De,
            "fr" => Fr,
            _ => En
        };
        return table.TryGetValue(key, out var value) ? value : key;
    }

    private static readonly IReadOnlyDictionary<string, string> En = T(
        ("Title", "Session health"), ("Export", "Export sanitized report"),
        ("ExportTitle", "Export sanitized NavBR session health report"),
        ("ExportSuccess", "Sanitized session health report exported successfully."),
        ("ExportError", "The session health report could not be exported."));
    private static readonly IReadOnlyDictionary<string, string> Pt = T(
        ("Title", "Saúde da sessão"), ("Export", "Exportar relatório sanitizado"),
        ("ExportTitle", "Exportar relatório sanitizado de saúde da sessão NavBR"),
        ("ExportSuccess", "Relatório sanitizado da saúde da sessão exportado com sucesso."),
        ("ExportError", "Não foi possível exportar o relatório da saúde da sessão."));
    private static readonly IReadOnlyDictionary<string, string> Es = T(
        ("Title", "Salud de la sesión"), ("Export", "Exportar informe sanitizado"),
        ("ExportTitle", "Exportar informe sanitizado de salud de sesión NavBR"),
        ("ExportSuccess", "Informe sanitizado de salud de sesión exportado correctamente."),
        ("ExportError", "No se pudo exportar el informe de salud de sesión."));
    private static readonly IReadOnlyDictionary<string, string> De = T(
        ("Title", "Sitzungsstatus"), ("Export", "Bereinigten Bericht exportieren"),
        ("ExportTitle", "Bereinigten NavBR-Sitzungsbericht exportieren"),
        ("ExportSuccess", "Bereinigter Sitzungsbericht erfolgreich exportiert."),
        ("ExportError", "Der Sitzungsbericht konnte nicht exportiert werden."));
    private static readonly IReadOnlyDictionary<string, string> Fr = T(
        ("Title", "Santé de session"), ("Export", "Exporter le rapport assaini"),
        ("ExportTitle", "Exporter le rapport assaini de santé de session NavBR"),
        ("ExportSuccess", "Rapport assaini de santé de session exporté avec succès."),
        ("ExportError", "Impossible d'exporter le rapport de santé de session."));

    private static IReadOnlyDictionary<string, string> T(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
