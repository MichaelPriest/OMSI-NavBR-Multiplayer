using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Operations;

internal static class DispatcherOperationalReportsPanel
{
    private const string PanelTag = "alpha12-dispatcher-operational-reports";

    public static void Attach(DispatcherWindow window)
    {
        var body = FindBody(window);
        if (body is null || body.Children.OfType<FrameworkElement>().Any(item => item.Tag as string == PanelTag))
        {
            return;
        }

        var title = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 14d,
            FontWeight = FontWeights.Bold
        };
        var status = new TextBlock
        {
            Foreground = Brush(128, 151, 166),
            FontSize = 10d,
            Margin = new Thickness(0d, 5d, 0d, 12d),
            TextWrapping = TextWrapping.Wrap
        };
        var rows = new StackPanel();

        var content = new StackPanel();
        content.Children.Add(title);
        content.Children.Add(status);
        content.Children.Add(rows);

        var panel = new Border
        {
            Tag = PanelTag,
            Margin = new Thickness(0d, 0d, 0d, 12d),
            Padding = new Thickness(16d),
            Background = Brush(10, 19, 25),
            BorderBrush = Brush(31, 47, 57),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Child = content
        };

        var insertIndex = Math.Max(0, body.Children.Count - 1);
        body.Children.Insert(insertIndex, panel);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500d) };
        string? lastSignature = null;

        void Refresh()
        {
            var reports = DispatcherOperationalFeed.Snapshot();
            title.Text = Text("Title");
            var activeCount = reports.Count(report => report.Status != OperationalReportStatus.Resolved);
            status.Text = activeCount == 0
                ? Text("NoActive")
                : string.Format(Text("ActiveCount"), activeCount) +
                  (DispatcherOperationalFeed.CanManageReports ? string.Empty : $" • {Text("ReadOnly")}");

            var signature = string.Join('|', reports.Select(report =>
                $"{report.ReportId}:{report.Status}:{report.Severity}:{report.UpdatedAtUtc.UtcTicks}"));
            signature = $"{DispatcherOperationalFeed.CanManageReports}:{LocalizationService.CurrentCulture.Name}:{signature}";
            if (string.Equals(signature, lastSignature, StringComparison.Ordinal))
            {
                return;
            }

            lastSignature = signature;
            rows.Children.Clear();
            if (reports.Count == 0)
            {
                rows.Children.Add(new TextBlock
                {
                    Text = Text("Empty"),
                    Foreground = Brush(139, 158, 171),
                    FontSize = 10.5d,
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            foreach (var report in reports.Take(12))
            {
                rows.Children.Add(BuildReportRow(report, status));
            }
        }

        timer.Tick += (_, _) => Refresh();
        window.Closed += (_, _) => timer.Stop();
        Refresh();
        timer.Start();
    }

    private static Border BuildReportRow(OperationalReport report, TextBlock statusText)
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel();
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock
        {
            Text = report.DisplayName,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 10.8d,
            Margin = new Thickness(0d, 0d, 8d, 0d)
        });
        header.Children.Add(BuildBadge(report));
        info.Children.Add(header);
        info.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(report.Message) ? Text("DefaultMessage") : report.Message,
            Foreground = Brush(166, 188, 202),
            FontSize = 9.5d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 4d, 12d, 0d)
        });
        info.Children.Add(new TextBlock
        {
            Text = $"{StatusLabel(report.Status)} • {FormatAge(report.UpdatedAtUtc)}",
            Foreground = Brush(102, 129, 146),
            FontSize = 8.6d,
            Margin = new Thickness(0d, 3d, 0d, 0d)
        });
        Grid.SetColumn(info, 0);
        root.Children.Add(info);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (report.Status != OperationalReportStatus.Resolved && DispatcherOperationalFeed.CanManageReports)
        {
            if (report.Status == OperationalReportStatus.Open)
            {
                actions.Children.Add(BuildActionButton(Text("Acknowledge"), async () =>
                {
                    try
                    {
                        var updated = await DispatcherOperationalFeed.AcknowledgeAsync(report.ReportId);
                        if (updated is not null)
                        {
                            DispatcherOperationalFeed.Update(updated);
                        }
                    }
                    catch (Exception ex)
                    {
                        statusText.Text = ex.Message;
                    }
                }));
            }

            actions.Children.Add(BuildActionButton(Text("Resolve"), async () =>
            {
                try
                {
                    var updated = await DispatcherOperationalFeed.ResolveAsync(report.ReportId);
                    if (updated is not null)
                    {
                        DispatcherOperationalFeed.Update(updated);
                    }
                }
                catch (Exception ex)
                {
                    statusText.Text = ex.Message;
                }
            }));
        }
        Grid.SetColumn(actions, 1);
        root.Children.Add(actions);

        return new Border
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
            Padding = new Thickness(12d, 10d, 12d, 10d),
            Background = Brush(7, 15, 20),
            BorderBrush = BorderFor(report),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(9d),
            Child = root
        };
    }

    private static Border BuildBadge(OperationalReport report)
    {
        var (label, accent) = report.Kind switch
        {
            OperationalReportKind.Incident => (Text("Incident"), Brush(231, 101, 82)),
            _ => (Text("Support"), Brush(232, 181, 70))
        };
        if (report.Status == OperationalReportStatus.Resolved)
        {
            label = Text("Resolved");
            accent = Brush(82, 188, 132);
        }
        else if (report.Status == OperationalReportStatus.Acknowledged)
        {
            label = Text("Acknowledged");
            accent = Brush(81, 165, 219);
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(32, accent.Color.R, accent.Color.G, accent.Color.B)),
            BorderBrush = accent,
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(7d),
            Padding = new Thickness(6d, 2d, 6d, 2d),
            Child = new TextBlock
            {
                Text = label,
                Foreground = accent,
                FontSize = 8d,
                FontWeight = FontWeights.Bold
            }
        };
    }

    private static Button BuildActionButton(string text, Func<Task> action)
    {
        var button = new Button
        {
            Content = text,
            Height = 28d,
            MinWidth = 78d,
            Margin = new Thickness(6d, 0d, 0d, 0d),
            Padding = new Thickness(8d, 3d, 8d, 3d),
            Background = Brush(13, 52, 72),
            Foreground = Brushes.White,
            BorderBrush = Brush(44, 90, 116),
            BorderThickness = new Thickness(1d),
            FontSize = 8.5d,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += async (_, _) => await action();
        return button;
    }

    private static SolidColorBrush BorderFor(OperationalReport report) =>
        report.Status == OperationalReportStatus.Resolved
            ? Brush(35, 73, 58)
            : report.Kind == OperationalReportKind.Incident
                ? Brush(105, 50, 43)
                : Brush(91, 76, 37);

    private static StackPanel? FindBody(DependencyObject root)
    {
        if (root is ScrollViewer { Content: StackPanel panel })
        {
            return panel;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindBody(VisualTreeHelper.GetChild(root, i)) is { } result)
            {
                return result;
            }
        }
        return null;
    }

    private static string FormatAge(DateTimeOffset timestamp)
    {
        var seconds = Math.Max(0, (int)(DateTimeOffset.UtcNow - timestamp).TotalSeconds);
        return seconds < 60
            ? string.Format(Text("SecondsAgo"), seconds)
            : string.Format(Text("MinutesAgo"), Math.Max(1, seconds / 60));
    }

    private static string StatusLabel(OperationalReportStatus status) => status switch
    {
        OperationalReportStatus.Acknowledged => Text("Acknowledged"),
        OperationalReportStatus.Resolved => Text("Resolved"),
        _ => Text("Open")
    };

    private static string Text(string key)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        var values = language switch
        {
            "pt" => Pt,
            "es" => Es,
            "de" => De,
            "fr" => Fr,
            _ => En
        };
        return values.TryGetValue(key, out var value) ? value : key;
    }

    private static readonly IReadOnlyDictionary<string, string> En = T(
        ("Title", "Operational requests and incidents"), ("NoActive", "No active operational request."),
        ("ActiveCount", "{0} active request(s)"), ("ReadOnly", "read-only: session authority handles actions"),
        ("Empty", "No requests have been received in this room."), ("DefaultMessage", "Operational request from driver."),
        ("Support", "SUPPORT"), ("Incident", "INCIDENT"), ("Acknowledged", "ACKNOWLEDGED"), ("Resolved", "RESOLVED"),
        ("Open", "OPEN"), ("Acknowledge", "Acknowledge"), ("Resolve", "Resolve"),
        ("SecondsAgo", "{0}s ago"), ("MinutesAgo", "{0} min ago"));
    private static readonly IReadOnlyDictionary<string, string> Pt = T(
        ("Title", "Chamados e incidentes operacionais"), ("NoActive", "Nenhum chamado operacional ativo."),
        ("ActiveCount", "{0} chamado(s) ativo(s)"), ("ReadOnly", "somente leitura: a autoridade da sessão atende os chamados"),
        ("Empty", "Nenhum chamado recebido nesta sala."), ("DefaultMessage", "Chamado operacional do motorista."),
        ("Support", "APOIO"), ("Incident", "INCIDENTE"), ("Acknowledged", "ATENDIDO"), ("Resolved", "RESOLVIDO"),
        ("Open", "ABERTO"), ("Acknowledge", "Reconhecer"), ("Resolve", "Resolver"),
        ("SecondsAgo", "há {0}s"), ("MinutesAgo", "há {0} min"));
    private static readonly IReadOnlyDictionary<string, string> Es = T(
        ("Title", "Solicitudes e incidentes operativos"), ("NoActive", "Ninguna solicitud operativa activa."),
        ("ActiveCount", "{0} solicitud(es) activa(s)"), ("ReadOnly", "solo lectura: la autoridad de sesión gestiona solicitudes"),
        ("Empty", "No se recibieron solicitudes en esta sala."), ("DefaultMessage", "Solicitud operativa del conductor."),
        ("Support", "APOYO"), ("Incident", "INCIDENTE"), ("Acknowledged", "ATENDIDO"), ("Resolved", "RESUELTO"),
        ("Open", "ABIERTO"), ("Acknowledge", "Reconocer"), ("Resolve", "Resolver"),
        ("SecondsAgo", "hace {0}s"), ("MinutesAgo", "hace {0} min"));
    private static readonly IReadOnlyDictionary<string, string> De = T(
        ("Title", "Betriebsmeldungen und Vorfälle"), ("NoActive", "Keine aktive Betriebsmeldung."),
        ("ActiveCount", "{0} aktive Meldung(en)"), ("ReadOnly", "nur lesen: Sitzungsautorität bearbeitet Meldungen"),
        ("Empty", "Keine Meldungen in diesem Raum."), ("DefaultMessage", "Betriebsmeldung des Fahrers."),
        ("Support", "HILFE"), ("Incident", "VORFALL"), ("Acknowledged", "BESTÄTIGT"), ("Resolved", "ERLEDIGT"),
        ("Open", "OFFEN"), ("Acknowledge", "Bestätigen"), ("Resolve", "Erledigen"),
        ("SecondsAgo", "vor {0}s"), ("MinutesAgo", "vor {0} Min"));
    private static readonly IReadOnlyDictionary<string, string> Fr = T(
        ("Title", "Demandes et incidents opérationnels"), ("NoActive", "Aucune demande opérationnelle active."),
        ("ActiveCount", "{0} demande(s) active(s)"), ("ReadOnly", "lecture seule : l’autorité de session gère les demandes"),
        ("Empty", "Aucune demande reçue dans cette salle."), ("DefaultMessage", "Demande opérationnelle du conducteur."),
        ("Support", "ASSISTANCE"), ("Incident", "INCIDENT"), ("Acknowledged", "PRIS EN CHARGE"), ("Resolved", "RÉSOLU"),
        ("Open", "OUVERT"), ("Acknowledge", "Prendre en charge"), ("Resolve", "Résoudre"),
        ("SecondsAgo", "il y a {0}s"), ("MinutesAgo", "il y a {0} min"));

    private static IReadOnlyDictionary<string, string> T(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
