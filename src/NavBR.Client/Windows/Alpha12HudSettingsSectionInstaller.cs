using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Multiplayer;
using NavBR.Client.Overlay;

namespace NavBR.Client.Windows;

internal static class Alpha12HudSettingsSectionBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(Alpha12SettingsWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnSettingsLoaded));
    }

    private static void OnSettingsLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Alpha12SettingsWindow window)
        {
            return;
        }

        _ = window.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            () => Alpha12HudSettingsSectionInstaller.Install(window));
    }
}

internal static class Alpha12HudSettingsSectionInstaller
{
    private static readonly HashSet<Alpha12SettingsWindow> Installed = new();

    public static void Install(Alpha12SettingsWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        window.Closed += (_, _) => Installed.Remove(window);

        var scroller = FindVisualChildren<ScrollViewer>(window)
            .FirstOrDefault(item => item.Content is StackPanel);
        if (scroller?.Content is not StackPanel contentStack)
        {
            return;
        }

        var section = BuildHudSection();
        contentStack.Children.Add(section);

        var hudButton = FindVisualChildren<Button>(window)
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "HUD", StringComparison.OrdinalIgnoreCase));
        if (hudButton is not null)
        {
            hudButton.Click += (_, _) =>
                _ = window.Dispatcher.BeginInvoke(
                    DispatcherPriority.ContextIdle,
                    section.BringIntoView);
        }
    }

    private static FrameworkElement BuildHudSection()
    {
        var settings = MultiplayerSettingsStore.Load();
        var loading = true;

        var presetCombo = NewCombo();
        presetCombo.ItemsSource = HudProfileCatalog.Presets;
        presetCombo.DisplayMemberPath = nameof(HudPresetDefinition.DisplayName);
        presetCombo.SelectedValuePath = nameof(HudPresetDefinition.Id);
        presetCombo.SelectedValue = settings.DashboardPreset;

        var themeCombo = NewCombo();
        themeCombo.ItemsSource = HudProfileCatalog.Themes;
        themeCombo.DisplayMemberPath = nameof(HudThemeDefinition.DisplayName);
        themeCombo.SelectedValuePath = nameof(HudThemeDefinition.Id);
        themeCombo.SelectedValue = settings.DashboardTheme;

        var anchorChoices = new[]
        {
            new Choice("free", T("Livre", "Free", "Libre", "Frei", "Libre")),
            new Choice("top-left", T("Superior esquerdo", "Top left", "Superior izquierda", "Oben links", "Haut gauche")),
            new Choice("top-center", T("Superior centro", "Top center", "Superior centro", "Oben Mitte", "Haut centre")),
            new Choice("top-right", T("Superior direito", "Top right", "Superior derecha", "Oben rechts", "Haut droite")),
            new Choice("bottom-left", T("Inferior esquerdo", "Bottom left", "Inferior izquierda", "Unten links", "Bas gauche")),
            new Choice("bottom-center", T("Inferior centro", "Bottom center", "Inferior centro", "Unten Mitte", "Bas centre")),
            new Choice("bottom-right", T("Inferior direito", "Bottom right", "Inferior derecha", "Unten rechts", "Bas droite"))
        };
        var anchorCombo = NewCombo();
        anchorCombo.ItemsSource = anchorChoices;
        anchorCombo.DisplayMemberPath = nameof(Choice.Label);
        anchorCombo.SelectedValuePath = nameof(Choice.Id);
        anchorCombo.SelectedValue = settings.DashboardAnchor;

        var scaleSlider = NewSlider(0.60d, 1.80d, settings.DashboardScale, 0.05d);
        var widthSlider = NewSlider(280d, 960d, settings.DashboardWidth, 10d);
        var heightSlider = NewSlider(160d, 720d, settings.DashboardHeight > 0d ? settings.DashboardHeight : 360d, 10d);
        var opacitySlider = NewSlider(0.35d, 1d, settings.DashboardOpacity, 0.05d);
        var minimapScaleSlider = NewSlider(0.55d, 2d, settings.DashboardMinimapScale, 0.05d);
        var multiplayerScaleSlider = NewSlider(0.55d, 2d, settings.DashboardMultiplayerScale, 0.05d);
        var alertsScaleSlider = NewSlider(0.55d, 2d, settings.DashboardAlertsScale, 0.05d);
        var sideScaleSlider = NewSlider(0.55d, 2d, settings.DashboardSideIndicatorsScale, 0.05d);

        var scaleValue = NewValueText();
        var widthValue = NewValueText();
        var heightValue = NewValueText();
        var opacityValue = NewValueText();
        var minimapScaleValue = NewValueText();
        var multiplayerScaleValue = NewValueText();
        var alertsScaleValue = NewValueText();
        var sideScaleValue = NewValueText();

        void RefreshValues()
        {
            scaleValue.Text = $"{scaleSlider.Value * 100d:0}%";
            widthValue.Text = $"{widthSlider.Value:0} px";
            heightValue.Text = $"{heightSlider.Value:0} px";
            opacityValue.Text = $"{opacitySlider.Value * 100d:0}%";
            minimapScaleValue.Text = $"{minimapScaleSlider.Value * 100d:0}%";
            multiplayerScaleValue.Text = $"{multiplayerScaleSlider.Value * 100d:0}%";
            alertsScaleValue.Text = $"{alertsScaleSlider.Value * 100d:0}%";
            sideScaleValue.Text = $"{sideScaleSlider.Value * 100d:0}%";
        }

        foreach (var slider in new[]
                 {
                     scaleSlider, widthSlider, heightSlider, opacitySlider,
                     minimapScaleSlider, multiplayerScaleSlider, alertsScaleSlider, sideScaleSlider
                 })
        {
            slider.ValueChanged += (_, _) => RefreshValues();
        }
        RefreshValues();

        var automaticHeightCheck = NewCheck(
            T("Altura automática", "Automatic height", "Altura automática", "Automatische Höhe", "Hauteur automatique"),
            settings.DashboardHeight <= 0d);
        heightSlider.IsEnabled = automaticHeightCheck.IsChecked != true;
        automaticHeightCheck.Checked += (_, _) => heightSlider.IsEnabled = false;
        automaticHeightCheck.Unchecked += (_, _) => heightSlider.IsEnabled = true;

        var hudEnabledCheck = NewCheck(
            T("Exibir HUD completo", "Show complete HUD", "Mostrar HUD completo", "Komplettes HUD anzeigen", "Afficher le HUD complet"),
            settings.HudEnabled);
        hudEnabledCheck.ToolTip = T(
            "Liga/desliga toda a sobreposição do NavBR sem desconectar o multiplayer.",
            "Turns the entire NavBR overlay on/off without disconnecting multiplayer.",
            "Activa/desactiva toda la superposición de NavBR sin desconectar el multijugador.",
            "Schaltet das gesamte NavBR-Overlay ein/aus, ohne den Multiplayer zu trennen.",
            "Active/désactive toute la superposition NavBR sans déconnecter le multijoueur.");

        var autoScaleCheck = NewCheck(
            T("Adaptar escala à resolução", "Adapt scale to resolution", "Adaptar escala a la resolución", "Skalierung an Auflösung anpassen", "Adapter l’échelle à la résolution"),
            settings.DashboardAutoScale);
        var enabledCheck = NewCheck(
            T("Mostrar painel do ônibus", "Show bus dashboard", "Mostrar panel del autobús", "Bus-Dashboard anzeigen", "Afficher le tableau de bord"),
            settings.DashboardEnabled);
        var fuelCheck = NewCheck(T("Combustível", "Fuel", "Combustible", "Kraftstoff", "Carburant"), settings.DashboardShowFuel);
        var pedalsCheck = NewCheck(T("Acelerador e freio", "Throttle and brake", "Acelerador y freno", "Gas und Bremse", "Accélérateur et frein"), settings.DashboardShowPedals);
        var statusCheck = NewCheck(T("Indicadores do veículo", "Vehicle indicators", "Indicadores del vehículo", "Fahrzeuganzeigen", "Indicateurs véhicule"), settings.DashboardShowStatus);
        var minimapCheck = NewCheck(T("Minimapa integrado", "Integrated minimap", "Minimapa integrado", "Integrierte Minikarte", "Minicarte intégrée"), settings.DashboardShowMinimap);
        var multiplayerCheck = NewCheck(T("Multiplayer no painel", "Multiplayer panel", "Multijugador en panel", "Mehrspieler im Dashboard", "Multijoueur dans le tableau"), settings.DashboardShowMultiplayer);
        var alertsCheck = NewCheck(T("Alertas discretos", "Discrete alerts", "Alertas discretas", "Dezente Warnungen", "Alertes discrètes"), settings.DashboardShowAlerts);
        var sideIndicatorsCheck = NewCheck(T("Indicadores laterais", "Side indicators", "Indicadores laterales", "Seitenanzeigen", "Indicateurs latéraux"), settings.DashboardShowSideIndicators);

        presetCombo.SelectionChanged += (_, _) =>
        {
            if (loading || presetCombo.SelectedValue is not string presetId)
            {
                return;
            }

            var preset = HudProfileCatalog.ResolvePreset(presetId);
            widthSlider.Value = preset.Width;
            scaleSlider.Value = preset.Scale;
            opacitySlider.Value = preset.Opacity;
            fuelCheck.IsChecked = preset.ShowFuel;
            pedalsCheck.IsChecked = preset.ShowPedals;
            statusCheck.IsChecked = preset.ShowStatus;
            minimapCheck.IsChecked = preset.ShowMinimap;
            multiplayerCheck.IsChecked = preset.ShowMultiplayer;
            alertsCheck.IsChecked = preset.ShowAlerts;
            sideIndicatorsCheck.IsChecked = preset.ShowSideIndicators;
            RefreshValues();
        };
        loading = false;

        var preview = BuildPreview(
            presetCombo,
            themeCombo,
            scaleSlider,
            widthSlider,
            opacitySlider,
            enabledCheck,
            minimapCheck,
            multiplayerCheck,
            alertsCheck,
            sideIndicatorsCheck);

        var root = new StackPanel();
        root.Children.Add(new TextBlock
        {
            Text = "HUD",
            Foreground = Brushes.White,
            FontSize = 16d,
            FontWeight = FontWeights.Bold
        });
        root.Children.Add(new TextBlock
        {
            Text = T(
                "Personalize o painel do ônibus sem sair de Configurações. As alterações usam o mesmo perfil do editor visual sobre o OMSI.",
                "Customize the bus dashboard from Settings. Changes use the same profile as the visual editor over OMSI.",
                "Personaliza el panel del autobús desde Configuración. Los cambios usan el mismo perfil del editor visual sobre OMSI.",
                "Passe das Bus-Dashboard in den Einstellungen an. Änderungen verwenden dasselbe Profil wie der visuelle Editor über OMSI.",
                "Personnalisez le tableau de bord depuis les paramètres. Les changements utilisent le même profil que l’éditeur visuel sur OMSI."),
            Foreground = Brush(113, 139, 156),
            FontSize = 10.2d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 4d, 0d, 12d)
        });

        root.Children.Add(NewCard(BuildTwoColumn(
            BuildField(T("Estilo do HUD", "HUD style", "Estilo del HUD", "HUD-Stil", "Style du HUD"), presetCombo),
            BuildField(T("Tema", "Theme", "Tema", "Design", "Thème"), themeCombo))));
        root.Children.Add(NewCard(BuildTwoColumn(
            BuildField(T("Ancoragem", "Anchor", "Anclaje", "Verankerung", "Ancrage"), anchorCombo),
            BuildCheckGroup(hudEnabledCheck, enabledCheck, autoScaleCheck, automaticHeightCheck))));

        root.Children.Add(NewCard(BuildSliderGrid(new[]
        {
            (T("Escala geral", "Overall scale", "Escala general", "Gesamtskalierung", "Échelle générale"), scaleSlider, scaleValue),
            (T("Largura", "Width", "Anchura", "Breite", "Largeur"), widthSlider, widthValue),
            (T("Altura manual", "Manual height", "Altura manual", "Manuelle Höhe", "Hauteur manuelle"), heightSlider, heightValue),
            (T("Opacidade", "Opacity", "Opacidad", "Deckkraft", "Opacité"), opacitySlider, opacityValue)
        })));

        root.Children.Add(NewCard(BuildWidgetGrid(new[]
        {
            fuelCheck, pedalsCheck, statusCheck, minimapCheck,
            multiplayerCheck, alertsCheck, sideIndicatorsCheck
        })));

        root.Children.Add(NewCard(BuildSliderGrid(new[]
        {
            (T("Tamanho do minimapa", "Minimap size", "Tamaño del minimapa", "Minikarten-Größe", "Taille de la minicarte"), minimapScaleSlider, minimapScaleValue),
            (T("Tamanho multiplayer", "Multiplayer size", "Tamaño multijugador", "Mehrspieler-Größe", "Taille multijoueur"), multiplayerScaleSlider, multiplayerScaleValue),
            (T("Tamanho dos alertas", "Alert size", "Tamaño de alertas", "Warnungsgröße", "Taille des alertes"), alertsScaleSlider, alertsScaleValue),
            (T("Tamanho dos indicadores", "Indicator size", "Tamaño de indicadores", "Anzeigegröße", "Taille des indicateurs"), sideScaleSlider, sideScaleValue)
        })));

        root.Children.Add(preview);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0d, 4d, 0d, 0d)
        };

        var resetButton = NewSecondaryButton(T("Restaurar preset", "Reset preset", "Restaurar preset", "Preset zurücksetzen", "Réinitialiser le preset"));
        resetButton.Margin = new Thickness(0d, 0d, 10d, 0d);
        resetButton.Click += (_, _) =>
        {
            var preset = HudProfileCatalog.ResolvePreset(presetCombo.SelectedValue as string);
            widthSlider.Value = preset.Width;
            scaleSlider.Value = preset.Scale;
            opacitySlider.Value = preset.Opacity;
            automaticHeightCheck.IsChecked = true;
            fuelCheck.IsChecked = preset.ShowFuel;
            pedalsCheck.IsChecked = preset.ShowPedals;
            statusCheck.IsChecked = preset.ShowStatus;
            minimapCheck.IsChecked = preset.ShowMinimap;
            multiplayerCheck.IsChecked = preset.ShowMultiplayer;
            alertsCheck.IsChecked = preset.ShowAlerts;
            sideIndicatorsCheck.IsChecked = preset.ShowSideIndicators;
            RefreshValues();
        };
        buttons.Children.Add(resetButton);

        var applyButton = NewPrimaryButton(T("Aplicar HUD", "Apply HUD", "Aplicar HUD", "HUD anwenden", "Appliquer le HUD"));
        applyButton.Click += (_, _) =>
        {
            var current = MultiplayerSettingsStore.Load();
            MultiplayerSettingsStore.Save(current with
            {
                DashboardSettingsVersion = 3,
                HudVisibilitySettingsVersion = 1,
                HudEnabled = hudEnabledCheck.IsChecked == true,
                DashboardEnabled = enabledCheck.IsChecked == true,
                DashboardPreset = presetCombo.SelectedValue as string ?? HudProfileCatalog.DefaultPreset,
                DashboardTheme = themeCombo.SelectedValue as string ?? HudProfileCatalog.DefaultTheme,
                DashboardAnchor = anchorCombo.SelectedValue as string ?? HudProfileCatalog.DefaultAnchor,
                DashboardScale = scaleSlider.Value,
                DashboardWidth = widthSlider.Value,
                DashboardHeight = automaticHeightCheck.IsChecked == true ? 0d : heightSlider.Value,
                DashboardOpacity = opacitySlider.Value,
                DashboardAutoScale = autoScaleCheck.IsChecked == true,
                DashboardShowFuel = fuelCheck.IsChecked == true,
                DashboardShowPedals = pedalsCheck.IsChecked == true,
                DashboardShowStatus = statusCheck.IsChecked == true,
                DashboardShowMinimap = minimapCheck.IsChecked == true,
                DashboardShowMultiplayer = multiplayerCheck.IsChecked == true,
                DashboardShowAlerts = alertsCheck.IsChecked == true,
                DashboardShowSideIndicators = sideIndicatorsCheck.IsChecked == true,
                DashboardMinimapScale = minimapScaleSlider.Value,
                DashboardMultiplayerScale = multiplayerScaleSlider.Value,
                DashboardAlertsScale = alertsScaleSlider.Value,
                DashboardSideIndicatorsScale = sideScaleSlider.Value
            });

            applyButton.Content = T("Aplicado ✓", "Applied ✓", "Aplicado ✓", "Angewendet ✓", "Appliqué ✓");
        };
        buttons.Children.Add(applyButton);
        root.Children.Add(buttons);

        return new Border
        {
            Name = "Alpha12HudSettingsSection",
            Margin = new Thickness(0d, 0d, 0d, 20d),
            Child = root
        };
    }

    private static Border BuildPreview(
        ComboBox presetCombo,
        ComboBox themeCombo,
        Slider scaleSlider,
        Slider widthSlider,
        Slider opacitySlider,
        CheckBox enabledCheck,
        CheckBox minimapCheck,
        CheckBox multiplayerCheck,
        CheckBox alertsCheck,
        CheckBox sideIndicatorsCheck)
    {
        var title = new TextBlock
        {
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = 12d
        };
        var detail = new TextBlock
        {
            Foreground = Brush(129, 153, 168),
            FontSize = 10d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 5d, 0d, 0d)
        };

        void RefreshPreview()
        {
            var preset = HudProfileCatalog.ResolvePreset(presetCombo.SelectedValue as string);
            var theme = HudProfileCatalog.ResolveTheme(themeCombo.SelectedValue as string);
            title.Text = $"{preset.DisplayName} • {theme.DisplayName}";

            var modules = new List<string>();
            if (minimapCheck.IsChecked == true) modules.Add(T("minimapa", "minimap", "minimapa", "Minikarte", "minicarte"));
            if (multiplayerCheck.IsChecked == true) modules.Add("multiplayer");
            if (alertsCheck.IsChecked == true) modules.Add(T("alertas", "alerts", "alertas", "Warnungen", "alertes"));
            if (sideIndicatorsCheck.IsChecked == true) modules.Add(T("indicadores", "indicators", "indicadores", "Anzeigen", "indicateurs"));

            var moduleText = modules.Count == 0 ? T("sem módulos extras", "no extra modules", "sin módulos extra", "keine Zusatzmodule", "aucun module supplémentaire") : string.Join(" • ", modules);
            detail.Text = $"{(enabledCheck.IsChecked == true ? "ON" : "OFF")} • {widthSlider.Value:0}px • {scaleSlider.Value * 100d:0}% • {opacitySlider.Value * 100d:0}% • {moduleText}";
        }

        presetCombo.SelectionChanged += (_, _) => RefreshPreview();
        themeCombo.SelectionChanged += (_, _) => RefreshPreview();
        scaleSlider.ValueChanged += (_, _) => RefreshPreview();
        widthSlider.ValueChanged += (_, _) => RefreshPreview();
        opacitySlider.ValueChanged += (_, _) => RefreshPreview();
        enabledCheck.Checked += (_, _) => RefreshPreview();
        enabledCheck.Unchecked += (_, _) => RefreshPreview();
        foreach (var check in new[] { minimapCheck, multiplayerCheck, alertsCheck, sideIndicatorsCheck })
        {
            check.Checked += (_, _) => RefreshPreview();
            check.Unchecked += (_, _) => RefreshPreview();
        }
        RefreshPreview();

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = T("Prévia da configuração", "Configuration preview", "Vista previa", "Konfigurationsvorschau", "Aperçu de la configuration"),
            Foreground = Brush(82, 196, 255),
            FontSize = 9d,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(title);
        stack.Children.Add(detail);

        return NewCard(stack);
    }

    private static Grid BuildTwoColumn(UIElement left, UIElement right)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private static FrameworkElement BuildField(string label, Control control)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brush(154, 178, 193),
            FontSize = 9.5d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 0d, 0d, 7d)
        });
        stack.Children.Add(control);
        return stack;
    }

    private static FrameworkElement BuildCheckGroup(params CheckBox[] checks)
    {
        var stack = new StackPanel();
        foreach (var check in checks)
        {
            stack.Children.Add(check);
        }
        return stack;
    }

    private static Grid BuildWidgetGrid(IEnumerable<CheckBox> checks)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var index = 0;
        foreach (var check in checks)
        {
            var row = index / 2;
            while (grid.RowDefinitions.Count <= row)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            check.Margin = new Thickness(0d, 5d, 10d, 5d);
            Grid.SetRow(check, row);
            Grid.SetColumn(check, index % 2);
            grid.Children.Add(check);
            index++;
        }
        return grid;
    }

    private static Grid BuildSliderGrid(IEnumerable<(string Label, Slider Slider, TextBlock Value)> rows)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72d) });

        var rowIndex = 0;
        foreach (var row in rows)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = row.Label,
                Foreground = Brush(178, 197, 209),
                FontSize = 10.5d,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0d, 6d, 12d, 6d)
            };
            Grid.SetRow(label, rowIndex);
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            row.Slider.Margin = new Thickness(0d, 3d, 14d, 3d);
            Grid.SetRow(row.Slider, rowIndex);
            Grid.SetColumn(row.Slider, 1);
            grid.Children.Add(row.Slider);

            Grid.SetRow(row.Value, rowIndex);
            Grid.SetColumn(row.Value, 2);
            grid.Children.Add(row.Value);
            rowIndex++;
        }

        return grid;
    }

    private static ComboBox NewCombo() => new()
    {
        Height = 38d,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private static Slider NewSlider(double minimum, double maximum, double value, double tick) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        Value = Math.Clamp(value, minimum, maximum),
        TickFrequency = tick,
        IsSnapToTickEnabled = true,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static CheckBox NewCheck(string label, bool value) => new()
    {
        Content = label,
        IsChecked = value,
        Foreground = Brushes.White,
        FontSize = 10.5d,
        Margin = new Thickness(0d, 5d, 0d, 5d)
    };

    private static TextBlock NewValueText() => new()
    {
        Foreground = Brush(82, 196, 255),
        FontWeight = FontWeights.Bold,
        FontSize = 10d,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static Border NewCard(UIElement child) => new()
    {
        Margin = new Thickness(0d, 0d, 0d, 10d),
        Padding = new Thickness(15d),
        CornerRadius = new CornerRadius(11d),
        Background = Brush(7, 18, 25),
        BorderBrush = Brush(25, 47, 60),
        BorderThickness = new Thickness(1d),
        Child = child
    };

    private static Button NewPrimaryButton(string text) => new()
    {
        Content = text,
        MinWidth = 145d,
        Height = 40d,
        Padding = new Thickness(16d, 8d, 16d, 8d),
        Background = Brush(19, 103, 171),
        Foreground = Brushes.White,
        BorderBrush = Brush(55, 155, 221),
        BorderThickness = new Thickness(1d),
        FontWeight = FontWeights.SemiBold,
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static Button NewSecondaryButton(string text) => new()
    {
        Content = text,
        MinWidth = 130d,
        Height = 40d,
        Padding = new Thickness(16d, 8d, 16d, 8d),
        Background = Brush(8, 19, 27),
        Foreground = Brush(213, 226, 234),
        BorderBrush = Brush(34, 56, 70),
        BorderThickness = new Thickness(1d),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

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
