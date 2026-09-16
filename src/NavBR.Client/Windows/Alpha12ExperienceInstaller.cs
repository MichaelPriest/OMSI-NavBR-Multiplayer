using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace NavBR.Client.Windows;

internal static class Alpha12ExperienceInstaller
{
    private const string TagPrefix = "alpha12-text:";
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        var settingsButton = InstallSettingsButton(window);
        var advancedToggle = FindAdvancedToggle(window);
        ApplyPreferences(window, advancedToggle, Alpha12PreferencesStore.Load());
        ApplyLocalization(window, settingsButton);

        SelectionChangedEventHandler languageChanged = (_, _) =>
            _ = window.Dispatcher.BeginInvoke(
                () => ApplyLocalization(window, settingsButton),
                DispatcherPriority.Loaded);
        window.LanguageComboBox.SelectionChanged += languageChanged;

        Action<Alpha12Preferences> preferencesSaved = preferences =>
        {
            if (!window.Dispatcher.CheckAccess())
            {
                _ = window.Dispatcher.BeginInvoke(() => ApplyPreferences(window, advancedToggle, preferences));
                return;
            }
            ApplyPreferences(window, advancedToggle, preferences);
        };
        Alpha12PreferencesStore.PreferencesSaved += preferencesSaved;

        if (advancedToggle is not null)
        {
            advancedToggle.Checked += (_, _) => PersistAdvancedMode(true);
            advancedToggle.Unchecked += (_, _) => PersistAdvancedMode(false);
        }

        window.Closed += (_, _) =>
        {
            window.LanguageComboBox.SelectionChanged -= languageChanged;
            Alpha12PreferencesStore.PreferencesSaved -= preferencesSaved;
            Installed.Remove(window);
        };

        _ = window.Dispatcher.BeginInvoke(
            () => ShowFirstRunIfNeeded(window),
            DispatcherPriority.ApplicationIdle);
    }

    private static Button InstallSettingsButton(MainWindow window)
    {
        var button = new Button();
        StyleNavigationButton(button);
        button.Margin = new Thickness(0d, 0d, 0d, 10d);
        button.Click += (_, _) =>
        {
            var dialog = new Alpha12SettingsWindow(window);
            dialog.ShowDialog();
            ApplyLocalization(window, button);
        };

        if (window.LanguageLabelText.Parent is Panel footer)
        {
            var insertIndex = footer.Children.IndexOf(window.LanguageLabelText);
            var advancedToggle = footer.Children.OfType<CheckBox>().FirstOrDefault();
            if (advancedToggle is not null)
            {
                insertIndex = footer.Children.IndexOf(advancedToggle);
            }
            footer.Children.Insert(Math.Max(0, insertIndex), button);
        }

        return button;
    }

    private static void ShowFirstRunIfNeeded(MainWindow window)
    {
        if (!window.IsVisible || Alpha12PreferencesStore.Load().FirstRunCompleted)
        {
            return;
        }

        var wizard = new Alpha12FirstRunWindow(window);
        wizard.ShowDialog();
        ApplyLocalization(window, FindSettingsButton(window));
    }

    private static Button? FindSettingsButton(MainWindow window) =>
        EnumerateVisualChildren<Button>(window)
            .FirstOrDefault(button => button.Tag as string == TagPrefix + "SettingsButton");

    private static CheckBox? FindAdvancedToggle(DependencyObject root)
    {
        foreach (var checkBox in EnumerateVisualChildren<CheckBox>(root))
        {
            if (checkBox.Content is not string text)
            {
                continue;
            }

            if (Alpha12Text.TryResolveKey(text, out var key) && key == "ShowAdvanced")
            {
                return checkBox;
            }
        }
        return null;
    }

    private static void PersistAdvancedMode(bool enabled)
    {
        var preferences = Alpha12PreferencesStore.Load();
        if (preferences.AdvancedModeEnabled == enabled)
        {
            return;
        }
        Alpha12PreferencesStore.Save(preferences with { AdvancedModeEnabled = enabled });
    }

    private static void ApplyPreferences(
        MainWindow window,
        CheckBox? advancedToggle,
        Alpha12Preferences preferences)
    {
        if (advancedToggle is not null && advancedToggle.IsChecked != preferences.AdvancedModeEnabled)
        {
            advancedToggle.IsChecked = preferences.AdvancedModeEnabled;
        }

        Alpha12HudThemeService.ApplyToOpenHud(preferences.HudTheme);
    }

    public static void ApplyLocalization(MainWindow window, Button? settingsButton = null)
    {
        TagAndTranslateTree(window);

        settingsButton ??= FindSettingsButton(window);
        if (settingsButton is not null)
        {
            settingsButton.Tag = TagPrefix + "SettingsButton";
            settingsButton.Content = Alpha12Text.Get("SettingsButton");
        }
    }

    internal static void TagAndTranslateTree(DependencyObject root)
    {
        if (root is TextBlock textBlock)
        {
            TranslateTextBlock(textBlock);
        }
        else if (root is ContentControl contentControl && contentControl.Content is string content)
        {
            TranslateContent(contentControl, content);
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            TagAndTranslateTree(VisualTreeHelper.GetChild(root, index));
        }
    }

    private static void TranslateTextBlock(TextBlock textBlock)
    {
        var key = ResolveTaggedKey(textBlock.Tag) ?? ResolveKey(textBlock.Text);
        if (key is null)
        {
            return;
        }
        textBlock.Tag = TagPrefix + key;
        textBlock.Text = Alpha12Text.Get(key);
    }

    private static void TranslateContent(ContentControl control, string content)
    {
        var key = ResolveTaggedKey(control.Tag) ?? ResolveKey(content);
        if (key is null)
        {
            return;
        }
        control.Tag = TagPrefix + key;
        control.Content = Alpha12Text.Get(key);
    }

    private static string? ResolveTaggedKey(object? tag)
    {
        if (tag is string value && value.StartsWith(TagPrefix, StringComparison.Ordinal))
        {
            return value[TagPrefix.Length..];
        }
        return null;
    }

    private static string? ResolveKey(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text) && Alpha12Text.TryResolveKey(text, out var key))
        {
            return key;
        }
        return null;
    }

    private static IEnumerable<T> EnumerateVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var descendant in EnumerateVisualChildren<T>(VisualTreeHelper.GetChild(root, index)))
            {
                yield return descendant;
            }
        }
    }

    private static void StyleNavigationButton(Button button)
    {
        button.Height = 42d;
        button.Padding = new Thickness(13d, 8d, 13d, 8d);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Background = new SolidColorBrush(Color.FromRgb(10, 19, 25));
        button.Foreground = new SolidColorBrush(Color.FromRgb(218, 230, 238));
        button.BorderBrush = new SolidColorBrush(Color.FromRgb(28, 42, 51));
        button.BorderThickness = new Thickness(1d);
        button.FontSize = 12d;
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }
}
