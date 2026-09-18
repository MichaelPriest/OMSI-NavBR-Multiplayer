using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;

namespace NavBR.Client.Windows;

internal sealed class Alpha12SettingsWindow : Window
{
    private readonly MainWindow _ownerWindow;
    private readonly ComboBox _languageCombo = new();
    private readonly ComboBox _themeCombo = new();
    private readonly CheckBox _advancedCheck = new();
    private readonly CheckBox _tipsCheck = new();
    private readonly TextBlock _titleText = new();
    private readonly TextBlock _subtitleText = new();
    private readonly TextBlock _languageLabel = new();
    private readonly TextBlock _themeLabel = new();
    private readonly TextBlock _advancedBody = new();
    private readonly TextBlock _versionLabel = new();
    private readonly TextBlock _versionValue = new();
    private readonly Button _saveButton = new();
    private readonly Button _cancelButton = new();

    public Alpha12SettingsWindow(MainWindow owner)
    {
        _ownerWindow = owner;
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 920d;
        Height = 680d;
        MinWidth = 820d;
        MinHeight = 600d;
        ResizeMode = ResizeMode.CanResize;
        Background = Brush(4, 10, 16);

        Content = BuildContent();
        LoadValues();
        ApplyLocalization();
    }

    private UIElement BuildContent()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220d) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var sidebar = new Border
        {
            Background = Brush(6, 15, 22),
            BorderBrush = Brush(22, 40, 52),
            BorderThickness = new Thickness(0d, 0d, 1d, 0d),
            Padding = new Thickness(16d, 22d, 16d, 18d)
        };
        var sideStack = new StackPanel();
        sideStack.Children.Add(new TextBlock
        {
            Text = "NavBR",
            Foreground = Brushes.White,
            FontSize = 21d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(4d, 0d, 0d, 2d)
        });
        sideStack.Children.Add(new TextBlock
        {
            Text = Category("Settings").ToUpperInvariant(),
            Foreground = Brush(73, 170, 225),
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(4d, 0d, 0d, 22d)
        });

        var contentStack = new StackPanel();
        var generalSection = BuildGeneralSection();
        var appearanceSection = BuildAppearanceSection();
        var navigationSection = BuildModuleSection("Navigation", "navigation");
        var multiplayerSection = BuildModuleSection("Multiplayer", "multiplayer");
        var voiceSection = BuildModuleSection("Voice", "voice");
        var hardwareSection = BuildModuleSection("Hardware", "hardware");
        var advancedSection = BuildAdvancedSection();
        var versionSection = BuildVersionSection();

        sideStack.Children.Add(BuildCategoryButton(Category("General"), true, generalSection));
        sideStack.Children.Add(BuildCategoryButton(Category("Appearance"), false, appearanceSection));
        sideStack.Children.Add(BuildCategoryButton(Category("Hud"), false, appearanceSection));
        sideStack.Children.Add(BuildCategoryButton(Category("Navigation"), false, navigationSection));
        sideStack.Children.Add(BuildCategoryButton(Category("Multiplayer"), false, multiplayerSection));
        sideStack.Children.Add(BuildCategoryButton(Category("Voice"), false, voiceSection));
        sideStack.Children.Add(BuildCategoryButton(Category("Hardware"), false, hardwareSection));
        sideStack.Children.Add(BuildCategoryButton(Category("Advanced"), false, advancedSection));
        sidebar.Child = sideStack;
        Grid.SetColumn(sidebar, 0);
        root.Children.Add(sidebar);

        var main = new Grid { Margin = new Thickness(26d, 22d, 26d, 20d) };
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new Grid { Margin = new Thickness(0d, 0d, 0d, 18d) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headingText = new StackPanel();
        _titleText.Foreground = Brushes.White;
        _titleText.FontSize = 24d;
        _titleText.FontWeight = FontWeights.Bold;
        headingText.Children.Add(_titleText);
        _subtitleText.Foreground = Brush(128, 151, 168);
        _subtitleText.FontSize = 11.5d;
        _subtitleText.TextWrapping = TextWrapping.Wrap;
        _subtitleText.Margin = new Thickness(0d, 5d, 16d, 0d);
        headingText.Children.Add(_subtitleText);
        Grid.SetColumn(headingText, 0);
        heading.Children.Add(headingText);
        var badge = new Border
        {
            Padding = new Thickness(11d, 6d, 11d, 6d),
            Background = Brush(7, 34, 51),
            BorderBrush = Brush(24, 85, 117),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(999d),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = NavBRVersionInfo.Display,
                Foreground = Brush(84, 190, 255),
                FontSize = 9d,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 190d
            }
        };
        Grid.SetColumn(badge, 1);
        heading.Children.Add(badge);
        Grid.SetRow(heading, 0);
        main.Children.Add(heading);

        contentStack.Children.Add(generalSection);
        contentStack.Children.Add(appearanceSection);
        contentStack.Children.Add(navigationSection);
        contentStack.Children.Add(multiplayerSection);
        contentStack.Children.Add(voiceSection);
        contentStack.Children.Add(hardwareSection);
        contentStack.Children.Add(advancedSection);
        contentStack.Children.Add(versionSection);

        var scroller = new ScrollViewer
        {
            Content = contentStack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false
        };
        Grid.SetRow(scroller, 1);
        main.Children.Add(scroller);

        var footer = new Grid { Margin = new Thickness(0d, 18d, 0d, 0d) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock
        {
            Text = Category("AutoSaveNote"),
            Foreground = Brush(94, 121, 139),
            FontSize = 9.5d,
            VerticalAlignment = VerticalAlignment.Center
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        StyleSecondaryButton(_cancelButton);
        _cancelButton.Margin = new Thickness(0d, 0d, 10d, 0d);
        _cancelButton.Click += (_, _) => Close();
        buttons.Children.Add(_cancelButton);
        StylePrimaryButton(_saveButton);
        _saveButton.Click += SaveButton_Click;
        buttons.Children.Add(_saveButton);
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);
        Grid.SetRow(footer, 2);
        main.Children.Add(footer);

        Grid.SetColumn(main, 1);
        root.Children.Add(main);
        return root;
    }

    private FrameworkElement BuildGeneralSection()
    {
        var stack = NewSectionStack(Category("General"), Category("GeneralBody"));

        _languageCombo.ItemsSource = LocalizationService.SupportedLanguages;
        _languageCombo.DisplayMemberPath = nameof(SupportedLanguage.DisplayName);
        _languageCombo.SelectedValuePath = nameof(SupportedLanguage.CultureName);
        _languageCombo.SelectionChanged += LanguageCombo_SelectionChanged;
        stack.Children.Add(BuildField(_languageLabel, _languageCombo));

        var tipsCard = NewCard();
        _tipsCheck.Foreground = Brushes.White;
        _tipsCheck.FontSize = 12d;
        tipsCard.Child = _tipsCheck;
        stack.Children.Add(tipsCard);
        return WrapSection(stack);
    }

    private FrameworkElement BuildAppearanceSection()
    {
        var stack = NewSectionStack(Category("Appearance"), Category("AppearanceBody"));
        _themeCombo.DisplayMemberPath = nameof(ThemeChoice.DisplayName);
        _themeCombo.SelectedValuePath = nameof(ThemeChoice.Id);
        stack.Children.Add(BuildField(_themeLabel, _themeCombo));

        var preview = new Grid();
        preview.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        preview.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        var dark = BuildPreviewTile("NavBR Modern", Brush(8, 22, 32), Brush(52, 171, 235));
        var amber = BuildPreviewTile("Classic Amber", Brush(20, 17, 10), Brush(236, 164, 59));
        Grid.SetColumn(dark, 0);
        Grid.SetColumn(amber, 1);
        preview.Children.Add(dark);
        preview.Children.Add(amber);
        stack.Children.Add(NewCardWithChild(preview));
        return WrapSection(stack);
    }

    private FrameworkElement BuildAdvancedSection()
    {
        var stack = NewSectionStack(Category("Advanced"), Category("AdvancedBody"));
        var advancedCard = NewCard();
        var advancedStack = new StackPanel();
        _advancedCheck.Foreground = Brushes.White;
        _advancedCheck.FontSize = 12.5d;
        _advancedCheck.FontWeight = FontWeights.SemiBold;
        advancedStack.Children.Add(_advancedCheck);
        _advancedBody.Foreground = Brush(126, 149, 164);
        _advancedBody.FontSize = 10.5d;
        _advancedBody.TextWrapping = TextWrapping.Wrap;
        _advancedBody.Margin = new Thickness(22d, 6d, 0d, 0d);
        advancedStack.Children.Add(_advancedBody);
        advancedCard.Child = advancedStack;
        stack.Children.Add(advancedCard);
        return WrapSection(stack);
    }

    private FrameworkElement BuildVersionSection()
    {
        var stack = NewSectionStack(Category("About"), Category("AboutBody"));
        var versionCard = NewCard();
        var versionGrid = new Grid();
        versionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        versionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _versionLabel.Foreground = Brush(154, 178, 193);
        _versionLabel.FontSize = 9.5d;
        _versionLabel.FontWeight = FontWeights.Bold;
        _versionValue.Foreground = Brush(82, 196, 255);
        _versionValue.FontSize = 12d;
        _versionValue.FontWeight = FontWeights.Bold;
        _versionValue.Text = NavBRVersionInfo.Display;
        Grid.SetColumn(_versionLabel, 0);
        Grid.SetColumn(_versionValue, 1);
        versionGrid.Children.Add(_versionLabel);
        versionGrid.Children.Add(_versionValue);
        versionCard.Child = versionGrid;
        stack.Children.Add(versionCard);
        return WrapSection(stack);
    }

    private FrameworkElement BuildModuleSection(string categoryKey, string moduleKey)
    {
        var stack = NewSectionStack(Category(categoryKey), Category(categoryKey + "Body"));
        var card = NewCard();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        var icon = new Border
        {
            Width = 38d,
            Height = 38d,
            Margin = new Thickness(0d, 0d, 12d, 0d),
            Background = Brush(8, 39, 58),
            BorderBrush = Brush(27, 90, 124),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(9d),
            Child = new TextBlock
            {
                Text = moduleKey[..1].ToUpperInvariant(),
                Foreground = Brush(82, 196, 255),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = Category("ModuleSettingsTitle"),
            Foreground = Brushes.White,
            FontSize = 11.5d,
            FontWeight = FontWeights.SemiBold
        });
        text.Children.Add(new TextBlock
        {
            Text = Category("ModuleSettingsBody"),
            Foreground = Brush(119, 145, 160),
            FontSize = 9.8d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 4d, 0d, 0d)
        });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        card.Child = grid;
        stack.Children.Add(card);
        return WrapSection(stack);
    }

    private static StackPanel NewSectionStack(string title, string body)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontSize = 16d,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = body,
            Foreground = Brush(113, 139, 156),
            FontSize = 10.2d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 4d, 0d, 12d)
        });
        return stack;
    }

    private static FrameworkElement WrapSection(UIElement content) => new Border
    {
        Margin = new Thickness(0d, 0d, 0d, 20d),
        Child = content
    };

    private static Button BuildCategoryButton(string text, bool selected, FrameworkElement target)
    {
        var button = new Button
        {
            Content = text,
            Height = 40d,
            Margin = new Thickness(0d, 0d, 0d, 5d),
            Padding = new Thickness(12d, 8d, 12d, 8d),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = selected ? Brush(13, 55, 84) : Brushes.Transparent,
            Foreground = selected ? Brushes.White : Brush(156, 179, 194),
            BorderBrush = selected ? Brush(35, 112, 158) : Brushes.Transparent,
            BorderThickness = new Thickness(1d),
            FontSize = 11.5d,
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = "settings-category"
        };
        button.Click += (_, _) =>
        {
            ApplyCategorySelection(button);
            target.BringIntoView();
        };
        return button;
    }

    private static void ApplyCategorySelection(Button selected)
    {
        if (selected.Parent is not Panel panel)
        {
            return;
        }

        foreach (var button in panel.Children
                     .OfType<Button>()
                     .Where(item => string.Equals(item.Tag as string, "settings-category", StringComparison.Ordinal)))
        {
            var isSelected = ReferenceEquals(button, selected);
            button.Background = isSelected ? Brush(13, 55, 84) : Brushes.Transparent;
            button.Foreground = isSelected ? Brushes.White : Brush(156, 179, 194);
            button.BorderBrush = isSelected ? Brush(35, 112, 158) : Brushes.Transparent;
            button.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private static Border BuildPreviewTile(string title, Brush background, Brush accent)
    {
        var mini = new StackPanel();
        mini.Children.Add(new Border
        {
            Height = 34d,
            Background = accent,
            CornerRadius = new CornerRadius(6d),
            Opacity = 0.85
        });
        mini.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontSize = 9.5d,
            Margin = new Thickness(0d, 7d, 0d, 0d)
        });
        return new Border
        {
            Margin = new Thickness(4d),
            Padding = new Thickness(10d),
            Background = background,
            BorderBrush = Brush(36, 57, 70),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(9d),
            Child = mini
        };
    }

    private Border BuildField(TextBlock label, Control control)
    {
        label.Foreground = Brush(154, 178, 193);
        label.FontSize = 9.5d;
        label.FontWeight = FontWeights.Bold;
        label.Margin = new Thickness(0d, 0d, 0d, 7d);
        control.Height = 38d;
        control.HorizontalAlignment = HorizontalAlignment.Stretch;

        var stack = new StackPanel();
        stack.Children.Add(label);
        stack.Children.Add(control);
        return NewCardWithChild(stack);
    }

    private static Border NewCardWithChild(UIElement child)
    {
        var card = NewCard();
        card.Child = child;
        return card;
    }

    private static Border NewCard() => new()
    {
        Margin = new Thickness(0d, 0d, 0d, 10d),
        Padding = new Thickness(15d),
        CornerRadius = new CornerRadius(11d),
        Background = Brush(7, 18, 25),
        BorderBrush = Brush(25, 47, 60),
        BorderThickness = new Thickness(1d)
    };

    private void LoadValues()
    {
        var preferences = Alpha12PreferencesStore.Load();
        _languageCombo.SelectedValue = LocalizationService.CurrentCulture.Name;
        _advancedCheck.IsChecked = preferences.AdvancedModeEnabled;
        _tipsCheck.IsChecked = preferences.ShowDrivingTips;
        RebuildThemeChoices(preferences.HudTheme);
    }

    private void RebuildThemeChoices(string? selectedTheme = null)
    {
        selectedTheme ??= _themeCombo.SelectedValue as string ?? "classic";
        _themeCombo.ItemsSource = Alpha12Text.ThemeIds
            .Select(id => new ThemeChoice(id, Alpha12Text.ThemeDisplayName(id)))
            .ToArray();
        _themeCombo.SelectedValue = selectedTheme;
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_languageCombo.SelectedValue is not string cultureName ||
            string.Equals(cultureName, LocalizationService.CurrentCulture.Name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        LocalizationService.SetCulture(cultureName);
        _ownerWindow.LanguageComboBox.SelectedValue = cultureName;
        ApplyLocalization();
        RebuildThemeChoices();
    }

    private void ApplyLocalization()
    {
        Title = Alpha12Text.Get("SettingsTitle");
        _titleText.Text = Alpha12Text.Get("SettingsTitle");
        _subtitleText.Text = Alpha12Text.Get("SettingsSubtitle");
        _languageLabel.Text = Alpha12Text.Get("Language");
        _themeLabel.Text = Alpha12Text.Get("HudTheme");
        _advancedCheck.Content = Alpha12Text.Get("AdvancedMode");
        _advancedBody.Text = Alpha12Text.Get("AdvancedModeBody");
        _tipsCheck.Content = Alpha12Text.Get("DrivingTips");
        _versionLabel.Text = VersionLabel();
        _versionValue.Text = NavBRVersionInfo.Display;
        _saveButton.Content = Alpha12Text.Get("Save");
        _cancelButton.Content = Alpha12Text.Get("Cancel");
    }

    private static string VersionLabel() => LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
    {
        "pt" => "VERSÃO DO NAVBR",
        "es" => "VERSIÓN DE NAVBR",
        "de" => "NAVBR-VERSION",
        "fr" => "VERSION DE NAVBR",
        _ => "NAVBR VERSION"
    };

    private static string Category(string key)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        var pt = language == "pt";
        var es = language == "es";
        var de = language == "de";
        var fr = language == "fr";
        return key switch
        {
            "Settings" => pt ? "Configurações" : es ? "Configuración" : de ? "Einstellungen" : fr ? "Paramètres" : "Settings",
            "General" => pt ? "Geral" : es ? "General" : de ? "Allgemein" : fr ? "Général" : "General",
            "Appearance" => pt ? "Aparência" : es ? "Apariencia" : de ? "Darstellung" : fr ? "Apparence" : "Appearance",
            "Hud" => "HUD",
            "Navigation" => pt ? "Navegação" : es ? "Navegación" : de ? "Navigation" : fr ? "Navigation" : "Navigation",
            "Multiplayer" => pt ? "Multiplayer" : es ? "Multijugador" : de ? "Mehrspieler" : fr ? "Multijoueur" : "Multiplayer",
            "Voice" => pt ? "Voz" : es ? "Voz" : de ? "Sprache" : fr ? "Voix" : "Voice",
            "Hardware" => "Hardware",
            "Advanced" => pt ? "Avançado" : es ? "Avanzado" : de ? "Erweitert" : fr ? "Avancé" : "Advanced",
            "About" => pt ? "Sobre" : es ? "Acerca de" : de ? "Über" : fr ? "À propos" : "About",
            "GeneralBody" => pt ? "Idioma e comportamento geral do aplicativo." : "Language and general application behavior.",
            "AppearanceBody" => pt ? "Tema visual do HUD e identidade operacional do NavBR." : "HUD theme and NavBR operational appearance.",
            "NavigationBody" => pt ? "Preferências específicas de navegação serão centralizadas aqui nas próximas etapas da Alpha.12." : "Navigation-specific preferences will be centralized here during Alpha.12.",
            "MultiplayerBody" => pt ? "Opções de sala, rede e presença continuam disponíveis na Central Multiplayer durante a migração." : "Room, network and presence options remain available in Multiplayer Central during migration.",
            "VoiceBody" => pt ? "Canais, PTT e dispositivos de áudio continuam na Central Multiplayer enquanto esta seção é integrada." : "Channels, PTT and audio devices remain in Multiplayer Central while this section is integrated.",
            "HardwareBody" => pt ? "Configurações do cockpit físico permanecem no módulo Hardware até a centralização final." : "Physical cockpit settings remain in the Hardware module until final centralization.",
            "AdvancedBody" => pt ? "Ferramentas técnicas, diagnóstico e recursos experimentais." : "Technical tools, diagnostics and experimental features.",
            "AboutBody" => pt ? "Informações da compilação atual do NavBR." : "Information about the current NavBR build.",
            "ModuleSettingsTitle" => pt ? "Configuração disponível no módulo" : "Settings available in the module",
            "ModuleSettingsBody" => pt ? "A Alpha.12 está migrando essas opções para esta tela sem remover os controles que já funcionam." : "Alpha.12 is moving these options here without removing controls that already work.",
            "AutoSaveNote" => pt ? "As alterações só são aplicadas ao salvar." : "Changes are applied when you save.",
            _ => key
        };
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var current = Alpha12PreferencesStore.Load();
        var selectedTheme = _themeCombo.SelectedValue as string ?? "classic";
        Alpha12PreferencesStore.Save(current with
        {
            FirstRunCompleted = true,
            AdvancedModeEnabled = _advancedCheck.IsChecked == true,
            HudTheme = selectedTheme,
            ShowDrivingTips = _tipsCheck.IsChecked == true
        });
        DialogResult = true;
        Close();
    }

    private static void StylePrimaryButton(Button button)
    {
        button.MinWidth = 160d;
        button.Height = 40d;
        button.Padding = new Thickness(16d, 8d, 16d, 8d);
        button.Background = Brush(19, 103, 171);
        button.Foreground = Brushes.White;
        button.BorderBrush = Brush(55, 155, 221);
        button.BorderThickness = new Thickness(1d);
        button.FontWeight = FontWeights.SemiBold;
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static void StyleSecondaryButton(Button button)
    {
        button.MinWidth = 105d;
        button.Height = 40d;
        button.Padding = new Thickness(16d, 8d, 16d, 8d);
        button.Background = Brush(8, 19, 27);
        button.Foreground = Brush(213, 226, 234);
        button.BorderBrush = Brush(34, 56, 70);
        button.BorderThickness = new Thickness(1d);
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

    private sealed record ThemeChoice(string Id, string DisplayName);
}