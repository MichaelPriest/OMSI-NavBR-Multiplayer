using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NavBR.Client.Maps;

internal sealed class RoadmapStudioWindow : Window
{
    private readonly Func<IReadOnlyList<OmsiMapInfo>> _mapProvider;
    private readonly OmsiRoadmapGeneratorService _generator = new();

    private readonly ComboBox _mapCombo = new();
    private readonly TextBlock _analysisText = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _analyzeButton = new();
    private readonly Button _generateButton = new();
    private readonly Button _openFolderButton = new();
    private readonly Image _preview = new();

    private OmsiMapInfo? _selectedMap;
    private OmsiRoadmapAnalysis? _analysis;

    public RoadmapStudioWindow(Func<IReadOnlyList<OmsiMapInfo>> mapProvider)
    {
        _mapProvider = mapProvider;
        Title = "NavBR Roadmap Studio • Alpha.11";
        Width = 980;
        Height = 720;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(5, 9, 13));
        Foreground = Brushes.White;
        Content = BuildUi();
        Loaded += (_, _) => ReloadMaps();
    }

    private UIElement BuildUi()
    {
        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var title = new StackPanel();
        title.Children.Add(new TextBlock
        {
            Text = "Roadmap Studio",
            FontSize = 28,
            FontWeight = FontWeights.Bold
        });
        title.Children.Add(new TextBlock
        {
            Text = "Monte whole.roadmap.bmp sem abrir o OMSI Editor. O primeiro modo combina automaticamente roadmaps por tile e cria backup do arquivo anterior.",
            Margin = new Thickness(0, 6, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(155, 173, 187)),
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var selector = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        selector.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        selector.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _mapCombo.DisplayMemberPath = nameof(OmsiMapInfo.DisplayName);
        _mapCombo.MinHeight = 34;
        _mapCombo.SelectionChanged += (_, _) => SelectCurrentMap();
        selector.Children.Add(_mapCombo);

        var reload = NewButton("↻ Atualizar mapas");
        reload.Margin = new Thickness(10, 0, 0, 0);
        reload.Click += (_, _) => ReloadMaps();
        Grid.SetColumn(reload, 1);
        selector.Children.Add(reload);
        Grid.SetRow(selector, 1);
        root.Children.Add(selector);

        var actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        _analyzeButton.Content = "Analisar tiles";
        ConfigureButton(_analyzeButton);
        _analyzeButton.Click += (_, _) => AnalyzeSelectedMap();
        actions.Children.Add(_analyzeButton);

        _generateButton.Content = "Gerar whole.roadmap.bmp";
        ConfigureButton(_generateButton, primary: true);
        _generateButton.Margin = new Thickness(8, 0, 0, 0);
        _generateButton.Click += async (_, _) => await GenerateAsync();
        actions.Children.Add(_generateButton);

        _openFolderButton.Content = "Abrir pasta do roadmap";
        ConfigureButton(_openFolderButton);
        _openFolderButton.Margin = new Thickness(8, 0, 0, 0);
        _openFolderButton.Click += (_, _) => OpenRoadmapFolder();
        actions.Children.Add(_openFolderButton);
        Grid.SetRow(actions, 2);
        root.Children.Add(actions);

        var info = new Grid { Margin = new Thickness(0, 14, 0, 12) };
        info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _analysisText.Foreground = new SolidColorBrush(Color.FromRgb(183, 205, 220));
        _analysisText.FontFamily = new FontFamily("Consolas");
        _analysisText.FontSize = 11;
        _analysisText.TextWrapping = TextWrapping.Wrap;
        info.Children.Add(_analysisText);
        _progress.Height = 6;
        _progress.Minimum = 0;
        _progress.Maximum = 100;
        _progress.Visibility = Visibility.Collapsed;
        _progress.Margin = new Thickness(0, 10, 0, 0);
        Grid.SetRow(_progress, 1);
        info.Children.Add(_progress);
        Grid.SetRow(info, 3);
        root.Children.Add(info);

        var previewBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(3, 8, 12)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(33, 49, 61)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10),
            ClipToBounds = true
        };
        var previewGrid = new Grid();
        _preview.Stretch = Stretch.Uniform;
        previewGrid.Children.Add(_preview);
        previewGrid.Children.Add(new TextBlock
        {
            Text = "PREVIEW DO ROADMAP",
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Foreground = new SolidColorBrush(Color.FromArgb(130, 255, 255, 255)),
            FontSize = 10,
            FontWeight = FontWeights.Bold
        });
        previewBorder.Child = previewGrid;
        Grid.SetRow(previewBorder, 4);
        root.Children.Add(previewBorder);

        return root;
    }

    private void ReloadMaps()
    {
        var maps = _mapProvider()
            .OrderBy(map => map.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        _mapCombo.ItemsSource = maps;
        _mapCombo.SelectedIndex = maps.Length > 0 ? 0 : -1;
        _analysisText.Text = maps.Length > 0
            ? $"{maps.Length} mapa(s) disponível(is). Selecione um mapa e clique em Analisar tiles."
            : "Nenhum mapa foi catalogado ainda. Inicie/detecte o OMSI e tente novamente.";
        _generateButton.IsEnabled = false;
        _openFolderButton.IsEnabled = false;
    }

    private void SelectCurrentMap()
    {
        _selectedMap = _mapCombo.SelectedItem as OmsiMapInfo;
        _analysis = null;
        _generateButton.IsEnabled = false;
        _openFolderButton.IsEnabled = _selectedMap is not null;
        _preview.Source = null;
        if (_selectedMap is not null)
        {
            _analysisText.Text = $"Mapa: {_selectedMap.DisplayName}\nPasta: {_selectedMap.DirectoryPath}";
        }
    }

    private void AnalyzeSelectedMap()
    {
        if (_selectedMap is null)
        {
            return;
        }

        try
        {
            _analysis = _generator.Analyze(_selectedMap);
            if (!_analysis.CanBuild)
            {
                _analysisText.Text =
                    $"Mapa: {_selectedMap.DisplayName}\n" +
                    "Nenhum arquivo tile_X_Y.map.roadmap.bmp foi encontrado.\n" +
                    "O modo Vetorial (geração direta a partir de splines/paths) será o segundo modo do Roadmap Studio.";
                _generateButton.IsEnabled = false;
                TryLoadPreview(_analysis.OutputPath);
                return;
            }

            _analysisText.Text =
                $"Mapa: {_selectedMap.DisplayName}\n" +
                $"Tiles com roadmap: {_analysis.TileImageCount}\n" +
                $"Grade: X {_analysis.MinGridX}..{_analysis.MaxGridX} | Y {_analysis.MinGridY}..{_analysis.MaxGridY}\n" +
                $"Tile: {_analysis.TilePixelWidth}x{_analysis.TilePixelHeight} px\n" +
                $"Saída: {_analysis.OutputPixelWidth}x{_analysis.OutputPixelHeight} px • {OmsiRoadmapGeneratorService.FormatBytes(_analysis.EstimatedBytes)}\n" +
                $"Posições sem imagem: {_analysis.MissingTileImages}\n" +
                $"whole.roadmap.bmp existente: {(_analysis.ExistingWholeRoadmap ? "SIM — será feito backup" : "NÃO")}";
            _generateButton.IsEnabled = true;
            TryLoadPreview(_analysis.OutputPath);
        }
        catch (Exception ex)
        {
            _analysisText.Text = $"Falha na análise: {ex.Message}";
            _generateButton.IsEnabled = false;
        }
    }

    private async Task GenerateAsync()
    {
        if (_selectedMap is null)
        {
            return;
        }

        _generateButton.IsEnabled = false;
        _analyzeButton.IsEnabled = false;
        _progress.Visibility = Visibility.Visible;
        _progress.Value = 0;

        try
        {
            var progress = new Progress<double>(value =>
            {
                _progress.Value = Math.Clamp(value * 100d, 0d, 100d);
            });
            var result = await _generator.BuildFromTileRoadmapsAsync(_selectedMap, progress);
            _analysisText.Text =
                $"GERADO COM SUCESSO\n" +
                $"Arquivo: {result.OutputPath}\n" +
                $"Dimensão: {result.PixelWidth}x{result.PixelHeight} px\n" +
                $"Tiles usados: {result.TileImagesUsed} | vazios: {result.MissingTileImages}\n" +
                $"Tamanho: {OmsiRoadmapGeneratorService.FormatBytes(result.FileSizeBytes)}\n" +
                $"Tempo: {result.Elapsed.TotalSeconds:F1}s\n" +
                (result.BackupPath is null ? "Backup: não necessário" : $"Backup: {result.BackupPath}");
            TryLoadPreview(result.OutputPath);
            _analysis = _generator.Analyze(_selectedMap);
        }
        catch (Exception ex)
        {
            _analysisText.Text = $"Falha ao gerar roadmap: {ex.Message}";
        }
        finally
        {
            _progress.Visibility = Visibility.Collapsed;
            _analyzeButton.IsEnabled = true;
            _generateButton.IsEnabled = _analysis?.CanBuild == true;
        }
    }

    private void OpenRoadmapFolder()
    {
        if (_selectedMap is null)
        {
            return;
        }

        var folder = Path.Combine(_selectedMap.DirectoryPath, "texture", "map");
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    private void TryLoadPreview(string path)
    {
        if (!File.Exists(path))
        {
            _preview.Source = null;
            return;
        }

        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 1200;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            _preview.Source = image;
        }
        catch
        {
            _preview.Source = null;
        }
    }

    private static Button NewButton(string text)
    {
        var button = new Button { Content = text };
        ConfigureButton(button);
        return button;
    }

    private static void ConfigureButton(Button button, bool primary = false)
    {
        button.MinHeight = 34;
        button.Padding = new Thickness(13, 7, 13, 7);
        button.Background = new SolidColorBrush(primary
            ? Color.FromRgb(244, 122, 24)
            : Color.FromRgb(15, 28, 38));
        button.Foreground = primary ? Brushes.Black : Brushes.White;
        button.BorderBrush = new SolidColorBrush(primary
            ? Color.FromRgb(255, 158, 77)
            : Color.FromRgb(39, 59, 74));
        button.BorderThickness = new Thickness(1);
    }
}
