using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;

namespace NavBR.Client.Windows;

internal sealed class Alpha12FirstRunWindow : Window
{
    private readonly MainWindow _ownerWindow;
    private readonly ComboBox _languageCombo = new();
    private readonly TextBlock _titleText = new();
    private readonly TextBlock _subtitleText = new();
    private readonly TextBlock _step1Text = new();
    private readonly TextBlock _step2Text = new();
    private readonly TextBlock _step2BodyText = new();
    private readonly TextBlock _step3Text = new();
    private readonly TextBlock _step3BodyText = new();
    private readonly Button _continueButton = new();

    public Alpha12FirstRunWindow(MainWindow owner)
    {
        _ownerWindow = owner;
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 680d;
        Height = 580d;
        MinWidth = 620d;
        MinHeight = 540d;
        ResizeMode = ResizeMode.NoResize;
        Background = Brush(6, 11, 16);
        Content = BuildContent();
        ApplyLocalization();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(28d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 22d) };
        _titleText.Foreground = Brushes.White;
        _titleText.FontSize = 27d;
        _titleText.FontWeight = FontWeights.Bold;
        heading.Children.Add(_titleText);
        _subtitleText.Foreground = Brush(155, 173, 186);
        _subtitleText.FontSize = 12d;
        _subtitleText.TextWrapping = TextWrapping.Wrap;
        _subtitleText.Margin = new Thickness(0d, 7d, 0d, 0d);
        heading.Children.Add(_subtitleText);
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var body = new StackPanel();
        body.Children.Add(BuildLanguageCard());
        body.Children.Add(BuildInfoCard(_step2Text, _step2BodyText, Brush(103, 188, 255)));
        body.Children.Add(BuildInfoCard(_step3Text, _step3BodyText, Brush(255, 164, 75)));
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        _continueButton.Height = 44d;
        _continueButton.MinWidth = 210d;
        _continueButton.HorizontalAlignment = HorizontalAlignment.Right;
        _continueButton.Padding = new Thickness(18d, 9d, 18d, 9d);
        _continueButton.Background = Brush(205, 88, 17);
        _continueButton.Foreground = Brushes.White;
        _continueButton.BorderBrush = Brush(255, 139, 48);
        _continueButton.BorderThickness = new Thickness(1d);
        _continueButton.FontWeight = FontWeights.SemiBold;
        _continueButton.Cursor = System.Windows.Input.Cursors.Hand;
        _continueButton.Click += ContinueButton_Click;
        Grid.SetRow(_continueButton, 2);
        root.Children.Add(_continueButton);

        return root;
    }

    private Border BuildLanguageCard()
    {
        var stack = new StackPanel();
        _step1Text.Foreground = Brushes.White;
        _step1Text.FontSize = 14d;
        _step1Text.FontWeight = FontWeights.SemiBold;
        stack.Children.Add(_step1Text);

        _languageCombo.ItemsSource = LocalizationService.SupportedLanguages;
        _languageCombo.DisplayMemberPath = nameof(SupportedLanguage.DisplayName);
        _languageCombo.SelectedValuePath = nameof(SupportedLanguage.CultureName);
        _languageCombo.SelectedValue = LocalizationService.CurrentCulture.Name;
        _languageCombo.Height = 38d;
        _languageCombo.Margin = new Thickness(0d, 12d, 0d, 0d);
        _languageCombo.SelectionChanged += LanguageCombo_SelectionChanged;
        stack.Children.Add(_languageCombo);

        return NewCard(stack, Brush(110, 216, 153));
    }

    private static Border BuildInfoCard(TextBlock title, TextBlock body, Brush accent)
    {
        var stack = new StackPanel();
        title.Foreground = Brushes.White;
        title.FontSize = 14d;
        title.FontWeight = FontWeights.SemiBold;
        stack.Children.Add(title);
        body.Foreground = Brush(148, 166, 179);
        body.FontSize = 11d;
        body.TextWrapping = TextWrapping.Wrap;
        body.Margin = new Thickness(0d, 7d, 0d, 0d);
        stack.Children.Add(body);
        return NewCard(stack, accent);
    }

    private static Border NewCard(UIElement child, Brush accent) => new()
    {
        Margin = new Thickness(0d, 0d, 0d, 13d),
        Padding = new Thickness(18d),
        Background = Brush(10, 19, 25),
        BorderBrush = accent,
        BorderThickness = new Thickness(3d, 0d, 0d, 0d),
        CornerRadius = new CornerRadius(11d),
        Child = child
    };

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
    }

    private void ApplyLocalization()
    {
        Title = Alpha12Text.Get("FirstRunTitle");
        _titleText.Text = Alpha12Text.Get("FirstRunTitle");
        _subtitleText.Text = Alpha12Text.Get("FirstRunSubtitle");
        _step1Text.Text = Alpha12Text.Get("FirstRunStep1");
        _step2Text.Text = Alpha12Text.Get("FirstRunStep2");
        _step2BodyText.Text = Alpha12Text.Get("FirstRunStep2Body");
        _step3Text.Text = Alpha12Text.Get("FirstRunStep3");
        _step3BodyText.Text = Alpha12Text.Get("FirstRunStep3Body");
        _continueButton.Content = Alpha12Text.Get("Continue");
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        var current = Alpha12PreferencesStore.Load();
        Alpha12PreferencesStore.Save(current with { FirstRunCompleted = true });
        DialogResult = true;
        Close();
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
