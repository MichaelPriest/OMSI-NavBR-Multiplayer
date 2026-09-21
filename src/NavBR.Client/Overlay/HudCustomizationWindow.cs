using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;
using NavBR.Client.Multiplayer;

namespace NavBR.Client.Overlay;

internal sealed class HudCustomizationWindow : Window
{
    private readonly ComboBox _preset = new();
    private readonly ComboBox _theme = new();
    private readonly ComboBox _anchor = new();
    private readonly Slider _scale = Slider(0.60d, 1.80d, 0.05d);
    private readonly Slider _width = Slider(280d, 960d, 10d);
    private readonly Slider _height = Slider(0d, 720d, 10d);
    private readonly Slider _opacity = Slider(0.35d, 1d, 0.05d);
    private readonly Slider _minimapScale = Slider(0.55d, 2d, 0.05d);
    private readonly Slider _multiplayerScale = Slider(0.55d, 2d, 0.05d);
    private readonly Slider _alertsScale = Slider(0.55d, 2d, 0.05d);
    private readonly Slider _indicatorsScale = Slider(0.55d, 2d, 0.05d);
    private readonly CheckBox _autoScale = new();
    private readonly CheckBox _fuel = new();
    private readonly CheckBox _pedals = new();
    private readonly CheckBox _status = new();
    private readonly CheckBox _minimap = new();
    private readonly CheckBox _multiplayer = new();
    private readonly CheckBox _alerts = new();
    private readonly CheckBox _sideIndicators = new();
    private readonly TextBlock _moduleHint = new();
    private readonly TextBlock _scaleValue = ValueText();
    private readonly TextBlock _widthValue = ValueText();
    private readonly TextBlock _heightValue = ValueText();
    private readonly TextBlock _opacityValue = ValueText();
    private readonly TextBlock _minimapScaleValue = ValueText();
    private readonly TextBlock _multiplayerScaleValue = ValueText();
    private readonly TextBlock _alertsScaleValue = ValueText();
    private readonly TextBlock _indicatorsScaleValue = ValueText();
    private bool _loading;

    public HudCustomizationWindow(Window? owner = null)
    {
        Owner = owner;
        WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        Title = T("Editor do HUD", "HUD Editor", "Editor del HUD", "HUD-Editor", "Éditeur HUD");
        Width = 820d;
        Height = 760d;
        MinWidth = 720d;
        MinHeight = 620d;
        ResizeMode = ResizeMode.CanResize;
        Background = Brush(4, 10, 16);
        Foreground = Brushes.White;
        Content = BuildContent();
        WireEvents();
        LoadSettings();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(24d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 18d) };
        title.Children.Add(new TextBlock
        {
            Text = T("Personalização do HUD", "HUD customization", "Personalización del HUD", "HUD-Anpassung", "Personnalisation du HUD"),
            Foreground = Brushes.White,
            FontSize = 24d,
            FontWeight = FontWeights.Bold
        });
        title.Children.Add(new TextBlock
        {
            Text = T(
                "Escolha o estilo e redimensione o painel inteiro ou cada módulo separadamente.",
                "Choose a style and resize the whole dashboard or each widget independently.",
                "Elige el estilo y cambia el tamaño del panel o de cada módulo por separado.",
                "Wähle einen Stil und ändere die Größe des gesamten Panels oder einzelner Module.",
                "Choisissez un style et redimensionnez le panneau entier ou chaque module séparément."),
            Foreground = Brush(128, 151, 168),
            FontSize = 11.5d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 5d, 0d, 0d)
        });
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var stack = new StackPanel();
        stack.Children.Add(BuildIdentityCard());
        stack.Children.Add(BuildSizeCard());
        stack.Children.Add(BuildModulesCard());
        stack.Children.Add(BuildWidgetScaleCard());

        var scroll = new ScrollViewer
        {
            Content = stack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var footer = new Grid { Margin = new Thickness(0d, 18d, 0d, 0d) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hint = new TextBlock
        {
            Text = T(
                "Dica: no modo Mover HUD, roda = escala • Shift+roda = largura • Ctrl+roda = opacidade.",
                "Tip: in Move HUD mode, wheel = scale • Shift+wheel = width • Ctrl+wheel = opacity.",
                "Consejo: en Mover HUD, rueda = escala • Shift+rueda = ancho • Ctrl+rueda = opacidad.",
                "Tipp: Im HUD-Verschiebemodus: Rad = Skalierung • Shift+Rad = Breite • Strg+Rad = Deckkraft.",
                "Astuce : en mode Déplacer HUD, molette = échelle • Maj+molette = largeur • Ctrl+molette = opacité."),
            Foreground = Brush(102, 128, 145),
            FontSize = 9.5d,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 15d, 0d)
        };
        Grid.SetColumn(hint, 0);
        footer.Children.Add(hint);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var reset = SecondaryButton(T("Restaurar padrão", "Reset", "Restablecer", "Zurücksetzen", "Réinitialiser"));
        reset.Click += (_, _) => ResetControls();
        buttons.Children.Add(reset);
        var cancel = SecondaryButton(T("Cancelar", "Cancel", "Cancelar", "Abbrechen", "Annuler"));
        cancel.Margin = new Thickness(8d, 0d, 0d, 0d);
        cancel.Click += (_, _) => Close();
        buttons.Children.Add(cancel);
        var save = PrimaryButton(T("Salvar HUD", "Save HUD", "Guardar HUD", "HUD speichern", "Enregistrer HUD"));
        save.Margin = new Thickness(8d, 0d, 0d, 0d);
        save.Click += (_, _) => SaveSettings();
        buttons.Children.Add(save);
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return root;
    }

    private Border BuildIdentityCard()
    {
        var grid = TwoColumnGrid();
        AddField(grid, 0, 0, T("ESTILO", "STYLE", "ESTILO", "STIL", "STYLE"), _preset);
        AddField(grid, 0, 1, T("TEMA", "THEME", "TEMA", "THEMA", "THÈME"), _theme);
        AddField(grid, 1, 0, T("ANCORAGEM", "ANCHOR", "ANCLAJE", "VERANKERUNG", "ANCRAGE"), _anchor);
        _autoScale.Content = T("Escala automática pela resolução", "Automatic resolution scaling", "Escala automática por resolución", "Automatische Auflösungsskalierung", "Échelle automatique selon la résolution");
        StyleCheck(_autoScale);
        Grid.SetRow(_autoScale, 1);
        Grid.SetColumn(_autoScale, 1);
        grid.Children.Add(_autoScale);
        return Card(T("Identidade e posição", "Identity and position", "Identidad y posición", "Identität und Position", "Identité et position"), grid);
    }

    private Border BuildSizeCard()
    {
        var stack = new StackPanel();
        var quick = new WrapPanel { Margin = new Thickness(0d, 0d, 0d, 12d) };
        quick.Children.Add(SizeButton(T("Pequeno", "Small", "Pequeño", "Klein", "Petit"), 0.72d));
        quick.Children.Add(SizeButton(T("Médio", "Medium", "Medio", "Mittel", "Moyen"), 1d));
        quick.Children.Add(SizeButton(T("Grande", "Large", "Grande", "Groß", "Grand"), 1.20d));
        quick.Children.Add(SizeButton("XL", 1.45d));
        stack.Children.Add(quick);
        stack.Children.Add(SliderField(T("Escala geral", "Overall scale", "Escala general", "Gesamtskalierung", "Échelle générale"), _scale, _scaleValue));
        stack.Children.Add(SliderField(T("Largura", "Width", "Ancho", "Breite", "Largeur"), _width, _widthValue));
        stack.Children.Add(SliderField(T("Altura (0 = automática)", "Height (0 = auto)", "Altura (0 = automática)", "Höhe (0 = automatisch)", "Hauteur (0 = auto)"), _height, _heightValue));
        stack.Children.Add(SliderField(T("Opacidade", "Opacity", "Opacidad", "Deckkraft", "Opacité"), _opacity, _opacityValue));
        return Card(T("Tamanho do painel", "Dashboard size", "Tamaño del panel", "Panelgröße", "Taille du panneau"), stack);
    }

    private Border BuildModulesCard()
    {
        var grid = TwoColumnGrid();
        ConfigureModuleCheck(_fuel, T("Combustível", "Fuel", "Combustible", "Kraftstoff", "Carburant"), grid, 0, 0);
        ConfigureModuleCheck(_pedals, T("Acelerador / freio", "Throttle / brake", "Acelerador / freno", "Gas / Bremse", "Accélérateur / frein"), grid, 0, 1);
        ConfigureModuleCheck(_status, T("Indicadores", "Indicators", "Indicadores", "Anzeigen", "Indicateurs"), grid, 1, 0);
        ConfigureModuleCheck(_minimap, T("Minimapa integrado", "Integrated minimap", "Minimapa integrado", "Integrierte Minikarte", "Mini-carte intégrée"), grid, 1, 1);
        ConfigureModuleCheck(_multiplayer, T("Multiplayer no painel", "Multiplayer panel", "Multijugador en el panel", "Mehrspieler im Panel", "Multijoueur dans le panneau"), grid, 2, 0);
        ConfigureModuleCheck(_alerts, T("Alertas discretos", "Discrete alerts", "Alertas discretas", "Diskrete Warnungen", "Alertes discrètes"), grid, 2, 1);
        ConfigureModuleCheck(_sideIndicators, T("Indicadores laterais", "Side indicators", "Indicadores laterales", "Seitliche Anzeigen", "Indicateurs latéraux"), grid, 3, 0);

        var stack = new StackPanel();
        stack.Children.Add(grid);
        _moduleHint.Margin = new Thickness(0d, 10d, 0d, 0d);
        _moduleHint.Foreground = Brush(112, 145, 165);
        _moduleHint.FontSize = 9.5d;
        _moduleHint.TextWrapping = TextWrapping.Wrap;
        _moduleHint.Visibility = Visibility.Collapsed;
        stack.Children.Add(_moduleHint);

        return Card(T("Módulos visíveis", "Visible widgets", "Módulos visibles", "Sichtbare Module", "Modules visibles"), stack);
    }

    private Border BuildWidgetScaleCard()
    {
        var stack = new StackPanel();
        stack.Children.Add(SliderField(T("Minimapa", "Minimap", "Minimapa", "Minikarte", "Mini-carte"), _minimapScale, _minimapScaleValue));
        stack.Children.Add(SliderField(T("Multiplayer", "Multiplayer", "Multijugador", "Mehrspieler", "Multijoueur"), _multiplayerScale, _multiplayerScaleValue));
        stack.Children.Add(SliderField(T("Alertas", "Alerts", "Alertas", "Warnungen", "Alertes"), _alertsScale, _alertsScaleValue));
        stack.Children.Add(SliderField(T("Indicadores laterais", "Side indicators", "Indicadores laterales", "Seitliche Anzeigen", "Indicateurs latéraux"), _indicatorsScale, _indicatorsScaleValue));
        return Card(T("Redimensionamento individual", "Individual resizing", "Cambio de tamaño individual", "Individuelle Größenänderung", "Redimensionnement individuel"), stack);
    }

    private void WireEvents()
    {
        _preset.SelectionChanged += (_, _) =>
        {
            if (_loading || _preset.SelectedValue is not string id)
            {
                return;
            }
            LoadPresetDefaults(HudProfileCatalog.ResolvePreset(id));
            UpdateModuleHint(id);
        };

        _scale.ValueChanged += (_, _) => RefreshValues();
        _width.ValueChanged += (_, _) => RefreshValues();
        _height.ValueChanged += (_, _) => RefreshValues();
        _opacity.ValueChanged += (_, _) => RefreshValues();
        _minimapScale.ValueChanged += (_, _) => RefreshValues();
        _multiplayerScale.ValueChanged += (_, _) => RefreshValues();
        _alertsScale.ValueChanged += (_, _) => RefreshValues();
        _indicatorsScale.ValueChanged += (_, _) => RefreshValues();
    }

    private void LoadSettings()
    {
        _loading = true;
        try
        {
            var settings = MultiplayerSettingsStore.Load();
            _preset.ItemsSource = HudProfileCatalog.Presets;
            _preset.DisplayMemberPath = nameof(HudPresetDefinition.DisplayName);
            _preset.SelectedValuePath = nameof(HudPresetDefinition.Id);
            _preset.SelectedValue = settings.DashboardPreset;

            _theme.ItemsSource = HudProfileCatalog.Themes;
            _theme.DisplayMemberPath = nameof(HudThemeDefinition.DisplayName);
            _theme.SelectedValuePath = nameof(HudThemeDefinition.Id);
            _theme.SelectedValue = settings.DashboardTheme;

            _anchor.ItemsSource = AnchorChoices();
            _anchor.DisplayMemberPath = nameof(Choice.Label);
            _anchor.SelectedValuePath = nameof(Choice.Id);
            _anchor.SelectedValue = settings.DashboardAnchor;

            SetControls(settings);
            UpdateModuleHint(settings.DashboardPreset);
        }
        finally
        {
            _loading = false;
            RefreshValues();
        }
    }

    private void SetControls(MultiplayerSettings settings)
    {
        _scale.Value = settings.DashboardScale;
        _width.Value = settings.DashboardWidth;
        _height.Value = settings.DashboardHeight;
        _opacity.Value = settings.DashboardOpacity;
        _autoScale.IsChecked = settings.DashboardAutoScale;
        _fuel.IsChecked = settings.DashboardShowFuel;
        _pedals.IsChecked = settings.DashboardShowPedals;
        _status.IsChecked = settings.DashboardShowStatus;
        _minimap.IsChecked = settings.DashboardShowMinimap;
        _multiplayer.IsChecked = settings.DashboardShowMultiplayer;
        _alerts.IsChecked = settings.DashboardShowAlerts;
        _sideIndicators.IsChecked = settings.DashboardShowSideIndicators;
        _minimapScale.Value = settings.DashboardMinimapScale;
        _multiplayerScale.Value = settings.DashboardMultiplayerScale;
        _alertsScale.Value = settings.DashboardAlertsScale;
        _indicatorsScale.Value = settings.DashboardSideIndicatorsScale;
    }

    private void LoadPresetDefaults(HudPresetDefinition preset)
    {
        _loading = true;
        try
        {
            _width.Value = preset.Width;
            _scale.Value = preset.Scale;
            _opacity.Value = preset.Opacity;
            _fuel.IsChecked = preset.ShowFuel;
            _pedals.IsChecked = preset.ShowPedals;
            _status.IsChecked = preset.ShowStatus;
            _minimap.IsChecked = preset.ShowMinimap;
            _multiplayer.IsChecked = preset.ShowMultiplayer;
            _alerts.IsChecked = preset.ShowAlerts;
            _sideIndicators.IsChecked = preset.ShowSideIndicators;
        }
        finally
        {
            _loading = false;
            RefreshValues();
        }
    }

    private void ResetControls()
    {
        var current = MultiplayerSettingsStore.Load();
        var preset = HudProfileCatalog.ResolvePreset(HudProfileCatalog.DefaultPreset);
        _preset.SelectedValue = preset.Id;
        _theme.SelectedValue = HudProfileCatalog.DefaultTheme;
        _anchor.SelectedValue = HudProfileCatalog.DefaultAnchor;
        _height.Value = 0d;
        _autoScale.IsChecked = true;
        _minimapScale.Value = 1d;
        _multiplayerScale.Value = 1d;
        _alertsScale.Value = 1d;
        _indicatorsScale.Value = 1d;
        LoadPresetDefaults(preset);
    }

    private void SaveSettings()
    {
        var current = MultiplayerSettingsStore.Load();
        var updated = current with
        {
            DashboardSettingsVersion = 3,
            DashboardPreset = _preset.SelectedValue as string ?? HudProfileCatalog.DefaultPreset,
            DashboardTheme = _theme.SelectedValue as string ?? HudProfileCatalog.DefaultTheme,
            DashboardAnchor = _anchor.SelectedValue as string ?? HudProfileCatalog.DefaultAnchor,
            DashboardScale = _scale.Value,
            DashboardWidth = _width.Value,
            DashboardHeight = _height.Value,
            DashboardOpacity = _opacity.Value,
            DashboardAutoScale = _autoScale.IsChecked == true,
            DashboardShowFuel = _fuel.IsChecked == true,
            DashboardShowPedals = _pedals.IsChecked == true,
            DashboardShowStatus = _status.IsChecked == true,
            DashboardShowMinimap = _minimap.IsChecked == true,
            DashboardShowMultiplayer = _multiplayer.IsChecked == true,
            DashboardShowAlerts = _alerts.IsChecked == true,
            DashboardShowSideIndicators = _sideIndicators.IsChecked == true,
            DashboardMinimapScale = _minimapScale.Value,
            DashboardMultiplayerScale = _multiplayerScale.Value,
            DashboardAlertsScale = _alertsScale.Value,
            DashboardSideIndicatorsScale = _indicatorsScale.Value
        };
        MultiplayerSettingsStore.Save(updated);
        DialogResult = true;
        Close();
    }

    private void UpdateModuleHint(string? presetId)
    {
        var isMinimal = string.Equals(
            HudProfileCatalog.NormalizePresetId(presetId),
            "minimal-driver",
            StringComparison.OrdinalIgnoreCase);

        _moduleHint.Visibility = isMinimal
            ? Visibility.Visible
            : Visibility.Collapsed;
        _moduleHint.Text = isMinimal
            ? T(
                "Minimapa contextual: fica oculto durante a operação normal e aparece automaticamente somente quando a rota está resolvida e o ônibus sai dela.",
                "Contextual minimap: stays hidden during normal operation and appears automatically only when the route is resolved and the bus goes off route.",
                "Minimapa contextual: permanece oculto durante la operación normal y aparece automáticamente solo cuando la ruta está resuelta y el autobús sale de ella.",
                "Kontext-Minimap: bleibt im Normalbetrieb verborgen und erscheint automatisch nur bei aufgelöster Route und Verlassen der Route.",
                "Mini-carte contextuelle : reste masquée en fonctionnement normal et apparaît automatiquement uniquement lorsque l’itinéraire est résolu et que le bus le quitte.")
            : string.Empty;
    }

    private void RefreshValues()
    {
        _scaleValue.Text = $"{_scale.Value * 100d:F0}%";
        _widthValue.Text = $"{_width.Value:F0} px";
        _heightValue.Text = _height.Value < 1d ? T("Automática", "Automatic", "Automática", "Automatisch", "Automatique") : $"{_height.Value:F0} px";
        _opacityValue.Text = $"{_opacity.Value * 100d:F0}%";
        _minimapScaleValue.Text = $"{_minimapScale.Value:F2}×";
        _multiplayerScaleValue.Text = $"{_multiplayerScale.Value:F2}×";
        _alertsScaleValue.Text = $"{_alertsScale.Value:F2}×";
        _indicatorsScaleValue.Text = $"{_indicatorsScale.Value:F2}×";
    }

    private Button SizeButton(string label, double scale)
    {
        var button = SecondaryButton(label);
        button.MinWidth = 82d;
        button.Margin = new Thickness(0d, 0d, 7d, 0d);
        button.Click += (_, _) => _scale.Value = scale;
        return button;
    }

    private static Grid TwoColumnGrid()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        for (var i = 0; i < 4; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        return grid;
    }

    private static void AddField(Grid grid, int row, int column, string label, Control control)
    {
        var stack = new StackPanel { Margin = new Thickness(5d, 5d, 8d, 10d) };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brush(143, 170, 188),
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 0d, 0d, 6d)
        });
        control.Height = 36d;
        stack.Children.Add(control);
        Grid.SetRow(stack, row);
        Grid.SetColumn(stack, column);
        grid.Children.Add(stack);
    }

    private static void ConfigureModuleCheck(CheckBox check, string label, Grid grid, int row, int column)
    {
        check.Content = label;
        StyleCheck(check);
        check.Margin = new Thickness(6d, 7d, 8d, 7d);
        Grid.SetRow(check, row);
        Grid.SetColumn(check, column);
        grid.Children.Add(check);
    }

    private static void StyleCheck(CheckBox check)
    {
        check.Foreground = Brushes.White;
        check.FontSize = 11d;
        check.VerticalAlignment = VerticalAlignment.Center;
    }

    private static Border SliderField(string label, Slider slider, TextBlock value)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82d) });
        grid.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brush(173, 193, 207),
            FontSize = 10.5d,
            VerticalAlignment = VerticalAlignment.Center
        });
        slider.Margin = new Thickness(10d, 0d, 10d, 0d);
        Grid.SetColumn(slider, 1);
        grid.Children.Add(slider);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        value.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(value, 2);
        grid.Children.Add(value);
        return new Border
        {
            Padding = new Thickness(2d, 7d, 2d, 7d),
            Child = grid
        };
    }

    private static Border Card(string title, UIElement content)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontSize = 14d,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0d, 0d, 0d, 11d)
        });
        stack.Children.Add(content);
        return new Border
        {
            Margin = new Thickness(0d, 0d, 0d, 12d),
            Padding = new Thickness(16d),
            Background = Brush(7, 18, 26),
            BorderBrush = Brush(25, 49, 64),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Child = stack
        };
    }

    private static Button PrimaryButton(string text) => new()
    {
        Content = text,
        MinWidth = 126d,
        Height = 40d,
        Padding = new Thickness(14d, 7d, 14d, 7d),
        Background = Brush(20, 111, 184),
        BorderBrush = Brush(58, 159, 225),
        BorderThickness = new Thickness(1d),
        Foreground = Brushes.White,
        FontWeight = FontWeights.SemiBold,
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static Button SecondaryButton(string text) => new()
    {
        Content = text,
        MinWidth = 104d,
        Height = 40d,
        Padding = new Thickness(12d, 7d, 12d, 7d),
        Background = Brush(9, 21, 30),
        BorderBrush = Brush(35, 61, 78),
        BorderThickness = new Thickness(1d),
        Foreground = Brush(220, 231, 237),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static Slider Slider(double min, double max, double tick) => new()
    {
        Minimum = min,
        Maximum = max,
        TickFrequency = tick,
        IsSnapToTickEnabled = true
    };

    private static TextBlock ValueText() => new()
    {
        Foreground = Brush(91, 190, 247),
        FontSize = 10d,
        FontWeight = FontWeights.SemiBold
    };

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

    private static IReadOnlyList<Choice> AnchorChoices() =>
    [
        new("free", T("Livre", "Free", "Libre", "Frei", "Libre")),
        new("custom", T("Personalizada (HUD composto)", "Custom (composed HUD)", "Personalizada (HUD compuesto)", "Benutzerdefiniert (HUD)", "Personnalisée (HUD composé)")),
        new("top-left", T("Superior esquerdo", "Top left", "Superior izquierda", "Oben links", "Haut gauche")),
        new("top-center", T("Superior central", "Top center", "Superior centro", "Oben mittig", "Haut centre")),
        new("top-right", T("Superior direito", "Top right", "Superior derecha", "Oben rechts", "Haut droite")),
        new("bottom-left", T("Inferior esquerdo", "Bottom left", "Inferior izquierda", "Unten links", "Bas gauche")),
        new("bottom-center", T("Inferior central", "Bottom center", "Inferior centro", "Unten mittig", "Bas centre")),
        new("bottom-right", T("Inferior direito", "Bottom right", "Inferior derecha", "Unten rechts", "Bas droite"))
    ];

    private static string T(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    private sealed record Choice(string Id, string Label);
}
