using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NavBR.Client.Ghost;

internal sealed class GhostReplayLibraryWindow : Window
{
    private readonly GhostReplayPlayer _loader = new();
    private readonly ListBox _list = new();
    private readonly TextBlock _status = new();
    private readonly Button _previewButton;
    private readonly Button _playbackButton;
    private readonly Button _compareButton;

    public GhostReplayLibraryWindow()
    {
        Title = "NavBR Ghost / Replay — Biblioteca";
        Width = 980d;
        Height = 720d;
        MinWidth = 800d;
        MinHeight = 600d;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(6, 16, 26);
        Foreground = Brush(218, 230, 238);
        FontFamily = new FontFamily("Segoe UI");
        Icon = Application.Current?.MainWindow?.Icon;

        _previewButton = Button("Visualizar trajeto", PreviewSelected_Click, primary: true);
        _playbackButton = Button("Reproduzir no mapa", PlaybackSelected_Click, primary: false);
        _compareButton = Button("Comparar selecionados", CompareSelected_Click, primary: false);

        Content = BuildContent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(28d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 18d) };
        heading.Children.Add(new TextBlock
        {
            Text = "Biblioteca de Replays",
            Foreground = Brush(218, 230, 238),
            FontSize = 27d,
            FontWeight = FontWeights.Bold
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Arquivos locais gravados pelo NavBR. Selecione um replay para visualizar/reproduzir ou dois para comparar.",
            Foreground = Brush(151, 171, 185),
            FontSize = 11.5d,
            Margin = new Thickness(0d, 6d, 0d, 0d),
            TextWrapping = TextWrapping.Wrap
        });
        root.Children.Add(heading);

        _list.Background = Brush(10, 19, 26);
        _list.Foreground = Brush(218, 230, 238);
        _list.BorderBrush = Brush(28, 42, 51);
        _list.BorderThickness = new Thickness(1d);
        _list.Padding = new Thickness(6d);
        _list.SelectionMode = SelectionMode.Extended;
        _list.DisplayMemberPath = nameof(GhostReplayLibraryItem.DisplayText);
        _list.MouseDoubleClick += PreviewSelected_Click;
        _list.SelectionChanged += (_, _) => RefreshButtons();
        Grid.SetRow(_list, 1);
        root.Children.Add(_list);

        var footer = new Grid { Margin = new Thickness(0d, 16d, 0d, 0d) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _status.Foreground = Brush(151, 171, 185);
        _status.FontSize = 10.5d;
        _status.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(_status);

        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(Button("Atualizar", async (_, _) => await ReloadAsync(), primary: false));
        actions.Children.Add(_previewButton);
        actions.Children.Add(_playbackButton);
        actions.Children.Add(_compareButton);
        actions.Children.Add(Button("Abrir pasta", OpenFolder_Click, primary: false));
        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        RefreshButtons();
        return root;
    }

    private async Task ReloadAsync()
    {
        var directory = GhostRecorder.GetGhostDirectory();
        Directory.CreateDirectory(directory);
        _status.Text = "Carregando biblioteca…";
        _list.ItemsSource = null;

        var items = new List<GhostReplayLibraryItem>();
        var invalidCount = 0;
        foreach (var path in Directory.EnumerateFiles(directory, $"*{GhostReplayFormat.Extension}")
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                var document = await _loader.LoadAsync(path);
                var analytics = GhostReplayAnalyticsCalculator.Analyze(document);
                items.Add(new GhostReplayLibraryItem(path, document, analytics));
            }
            catch
            {
                invalidCount++;
            }
        }

        _list.ItemsSource = items;
        _status.Text = items.Count == 0
            ? invalidCount > 0
                ? $"Nenhum replay válido. {invalidCount} arquivo(s) incompatível(is) ignorado(s)."
                : "Nenhum replay gravado nesta pasta."
            : invalidCount > 0
                ? $"{items.Count} replay(s) válido(s) • {invalidCount} incompatível(is) ignorado(s)."
                : $"{items.Count} replay(s) local(is).";
        RefreshButtons();
    }

    private void PreviewSelected_Click(object? sender, RoutedEventArgs e)
    {
        var item = SelectedSingle();
        if (item is null)
        {
            return;
        }

        new GhostReplayPreviewWindow(item.Document, item.Path)
        {
            Owner = this
        }.ShowDialog();
    }

    private void PlaybackSelected_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectedSingle();
        if (item is null)
        {
            return;
        }

        new GhostReplayMapPlaybackWindow(item.Document)
        {
            Owner = this
        }.ShowDialog();
    }

    private void CompareSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = _list.SelectedItems.OfType<GhostReplayLibraryItem>().Take(3).ToArray();
        if (selected.Length != 2)
        {
            return;
        }

        new GhostReplayComparisonWindow(
            selected[0].Document,
            selected[0].Path,
            selected[1].Document,
            selected[1].Path)
        {
            Owner = this
        }.ShowDialog();
    }

    private GhostReplayLibraryItem? SelectedSingle() =>
        _list.SelectedItems.Count == 1
            ? _list.SelectedItem as GhostReplayLibraryItem
            : null;

    private void RefreshButtons()
    {
        var count = _list.SelectedItems.Count;
        _previewButton.IsEnabled = count == 1;
        _playbackButton.IsEnabled = count == 1;
        _compareButton.IsEnabled = count == 2;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var directory = GhostRecorder.GetGhostDirectory();
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{directory}\"",
            UseShellExecute = true
        });
    }

    private static Button Button(string text, RoutedEventHandler handler, bool primary)
    {
        var button = new Button
        {
            Content = text,
            Height = 36d,
            MinWidth = 110d,
            Margin = new Thickness(8d, 0d, 0d, 0d),
            Padding = new Thickness(13d, 6d, 13d, 6d),
            Background = primary ? Brush(61, 137, 196) : Brush(13, 26, 36),
            Foreground = Brushes.White,
            BorderBrush = primary ? Brush(113, 198, 255) : Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand
        };
        button.Click += handler;
        return button;
    }

    private static string Safe(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string FormatDuration(double seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0d, seconds));
        return duration.TotalHours >= 1d
            ? duration.ToString("hh\\:mm\\:ss")
            : duration.ToString("mm\\:ss");
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));

    private sealed record GhostReplayLibraryItem(
        string Path,
        GhostReplayDocument Document,
        GhostReplayAnalytics Analytics)
    {
        public string DisplayText =>
            $"{Document.Metadata.Name}    •    {Safe(Document.Metadata.MapName)}    •    {Safe(Document.Metadata.VehicleName)}    •    {FormatDuration(Analytics.DurationSeconds)}    •    {Analytics.EstimatedDistanceKm:0.00} km";
    }
}
