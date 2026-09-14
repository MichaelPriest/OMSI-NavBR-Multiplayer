using System.Windows;
using System.Windows.Controls;
using NavBR.Client.Localization;
using NavBR.Client.Omsi;

namespace NavBR.Client;

public partial class MainWindow : Window
{
    private readonly OmsiProcessDetector _detector = new();
    private bool _languageSelectorReady;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureLanguageSelector();
        ApplyLocalization();
        Loaded += (_, _) => RefreshOmsiStatus();
    }

    private void ConfigureLanguageSelector()
    {
        LanguageComboBox.ItemsSource = LocalizationService.SupportedLanguages;
        LanguageComboBox.DisplayMemberPath = nameof(SupportedLanguage.DisplayName);
        LanguageComboBox.SelectedValuePath = nameof(SupportedLanguage.CultureName);
        LanguageComboBox.SelectedValue = LocalizationService.CurrentCulture.Name;
        _languageSelectorReady = true;
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_languageSelectorReady || LanguageComboBox.SelectedValue is not string cultureName)
        {
            return;
        }

        if (!string.Equals(
                cultureName,
                LocalizationService.CurrentCulture.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            LocalizationService.SetCulture(cultureName);
        }

        ApplyLocalization();
        RefreshOmsiStatus();
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.Get("AppTitle");
        TaglineText.Text = LocalizationService.Get("Tagline");
        LanguageLabelText.Text = LocalizationService.Get("LanguageLabel");
        StatusHeadingText.Text = LocalizationService.Get("StatusHeading");
        StatusText.Text = LocalizationService.Get("StatusSearching");
        RefreshButton.Content = LocalizationService.Get("RedetectButton");
        MilestoneHeadingText.Text = LocalizationService.Get("FirstMilestoneHeading");
        MilestoneBodyText.Text = LocalizationService.Get("FirstMilestoneBody");
        PhaseFooterText.Text = LocalizationService.Get("PhaseFooter");
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshOmsiStatus();
    }

    private void RefreshOmsiStatus()
    {
        var instances = _detector.FindRunningInstances();

        if (instances.Count == 0)
        {
            StatusText.Text = LocalizationService.Get("OmsiNotRunning");
            InstallPathText.Text = LocalizationService.Get("OpenOmsiInstruction");
            ProcessDetailsText.Text = LocalizationService.Get("NoProcessFound");
            return;
        }

        var omsi = instances[0];
        StatusText.Text = LocalizationService.Get("OmsiDetected");
        InstallPathText.Text = omsi.InstallDirectory;
        ProcessDetailsText.Text =
            $"{LocalizationService.Get("PidLabel")}: {omsi.ProcessId}\n" +
            $"{LocalizationService.Get("VersionLabel")}: {omsi.FileVersion}\n" +
            $"{LocalizationService.Get("ExecutableLabel")}: {omsi.ExecutablePath}";
    }
}
