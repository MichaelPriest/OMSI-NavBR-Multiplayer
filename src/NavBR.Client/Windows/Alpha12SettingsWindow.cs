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
    private readonly Button _saveButton = new();
    private readonly Button _cancelButton = new();

    public Alpha12SettingsWindow(MainWindow owner)
    {
        _ownerWindow = owner;
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 650d;
        Height = 560d;
        MinWidth = 560d;
        MinHeight = 500d;
        ResizeMode = ResizeMode.CanResize;
        Background = Brush(7, 12, 17);

        Content = BuildContent();
        LoadValues();
        ApplyLocalization();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(26d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 20d) };
        _titleText.Foreground = Brushes.White;
        _titleText.FontSize = 24d;
        _titleText.FontWeight = FontWeights.Bold;
        heading.Children.Add(_titleText);
        _subtitleText.Foreground = Brush(145, 163, 176);
        _subtitleText.FontSize = 11.5d;
        _subtitleText.TextWrapping = TextWrapping.Wrap;
        _subtitleText.Margin = new Thickness(0d, 6d, 0d, 0d);
        heading.Children.Add(_subtitleText);
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var body = new StackPanel();
        scroller.Content = body;

        body.Children.Add(BuildField(_languageLabel, _languageCombo));
        _languageCombo.ItemsSource = LocalizationService.SupportedLanguages;
        _languageCombo.DisplayMemberPath = nameof(SupportedLanguage.DisplayName);
        _languageCombo.SelectedValuePath = nameof(SupportedLanguage.CultureName);
        _languageCombo.SelectionChanged += LanguageCombo_SelectionChanged;

        body.Children.Add(BuildField(_themeLabel, _themeCombo));
        _themeCombo.DisplayMemberPath = nameof(ThemeChoice.DisplayName);
        _themeCombo.SelectedValuePath = nameof(ThemeChoice.Id);

        var advancedCard = NewCard();
        var advancedStack = new StackPanel();
        _advancedCheck.Foreground = Brushes.White;
        _advancedCheck.FontSize = 12.5d;
        _advancedCheck.FontWeight = FontWeights.SemiBold;
        advancedStack.Children.Add(_advancedCheck);
        _advancedBody.Foreground = Brush(139, 158, 171);
        _advancedBody.FontSize = 10.5d;
        _advancedBody.TextWrapping = TextWrapping.Wrap;
        _advancedBody.Margin = new Thickness(22d, 6d, 0d, 0d);
        advancedStack.Children.Add(_advancedBody);
        advancedCard.Child = advancedStack;
        body.Children.Add(advancedCard);

        var tipsCard = NewCard();
        _tipsCheck.Foreground = Brushes.White;
        _tipsCheck.FontSize = 12.5d;
        tipsCard.Child = _tipsCheck;
        body.Children.Add(tipsCard);

        Grid.SetRow(scroller, 1);
        root.Children.Add(scroller);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0d, 20d, 0d, 0d)
        };
        StyleSecondaryButton(_cancelButton);
        _cancelButton.Margin = new Thickness(0d, 0d, 10d, 0d);
        _cancelButton.Click += (_, _) => Close();
        buttons.Children.Add(_cancelButton);
        StylePrimaryButton(_saveButton);
        _saveButton.Click += SaveButton_Click;
        buttons.Children.Add(_saveButton);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);

        return root;
    }

    private Border BuildField(TextBlock label, Control control)
    {
        label.Foreground = Brush(174, 190, 201);
        label.FontSize = 10d;
        label.FontWeight = FontWeights.Bold;
        label.Margin = new Thickness(0d, 0d, 0d, 7d);
        control.Height = 38d;
        control.HorizontalAlignment = HorizontalAlignment.Stretch;

        var stack = new StackPanel();
        stack.Children.Add(label);
        stack.Children.Add(control);
        var card = NewCard();
        card.Child = stack;
        return card;
    }

    private static Border NewCard() => new()
    {
        Margin = new Thickness(0d, 0d, 0d, 12d),
        Padding = new Thickness(16d),
        CornerRadius = new CornerRadius(12d),
        Background = Brush(11, 20, 26),
        BorderBrush = Brush(31, 47, 57),
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
        _saveButton.Content = Alpha12Text.Get("Save");
        _cancelButton.Content = Alpha12Text.Get("Cancel");
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
        button.Background = Brush(205, 88, 17);
        button.Foreground = Brushes.White;
        button.BorderBrush = Brush(255, 139, 48);
        button.BorderThickness = new Thickness(1d);
        button.FontWeight = FontWeights.SemiBold;
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static void StyleSecondaryButton(Button button)
    {
        button.MinWidth = 105d;
        button.Height = 40d;
        button.Padding = new Thickness(16d, 8d, 16d, 8d);
        button.Background = Brush(12, 21, 27);
        button.Foreground = Brush(213, 226, 234);
        button.BorderBrush = Brush(39, 56, 67);
        button.BorderThickness = new Thickness(1d);
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

    private sealed record ThemeChoice(string Id, string DisplayName);
}
