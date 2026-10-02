using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavBR.Client.WinUI;

public sealed partial class SettingsPage : UserControl
{
    public Func<string, object?, Task>? CommandHandler { get; set; }

    private readonly ObservableCollection<OmsiInstallRow> _installations = new();
    private bool _applyingState;

    public SettingsPage()
    {
        InitializeComponent();
        InstallationsList.ItemsSource = _installations;
    }

    public void ApplyState(JsonElement state)
    {
        var system = JsonState.Property(state, "system");

        var languages = JsonState.Array(state, "supportedLanguages")
            .Select(x => new LanguageRow(
                JsonState.String(x, "cultureName") ?? string.Empty,
                JsonState.String(x, "displayName") ?? "Idioma"))
            .Where(x => x.CultureName.Length > 0)
            .ToArray();

        _applyingState = true;
        try
        {
            LanguageComboBox.ItemsSource = languages;
            var currentCulture = JsonState.String(state, "cultureName");
            LanguageComboBox.SelectedItem = languages.FirstOrDefault(x =>
                string.Equals(
                    x.CultureName,
                    currentCulture,
                    StringComparison.OrdinalIgnoreCase));

            var preferences = JsonState.Property(system, "legacyPreferences");
            AdvancedModeCheckBox.IsChecked =
                JsonState.Bool(preferences, "advancedModeEnabled");
            DrivingTipsCheckBox.IsChecked =
                JsonState.Bool(preferences, "showDrivingTips");
        }
        finally
        {
            _applyingState = false;
        }

        InstallationsNoticeText.Text =
            JsonState.String(system, "installationsNotice") ??
            "Perfis detectados e administrados pelo Runtime Host.";

        _installations.Clear();
        foreach (var item in JsonState.Array(system, "installations"))
        {
            var preferred = JsonState.Bool(item, "isPreferred");
            var running = JsonState.Bool(item, "isRunning");
            _installations.Add(new OmsiInstallRow(
                JsonState.String(item, "id") ?? string.Empty,
                JsonState.String(item, "name") ?? "OMSI 2",
                JsonState.String(item, "installDirectory") ?? "—",
                JsonState.String(item, "executablePath") ?? string.Empty,
                preferred,
                running,
                running ? "EM EXECUÇÃO" : JsonState.Bool(item, "executableExists") ? "PRONTO" : "EXE AUSENTE",
                preferred ? "PRINCIPAL" : string.Empty));
        }
    }

    private async Task RunAsync(string command, object? payload = null)
    {
        try
        {
            if (CommandHandler is null)
            {
                return;
            }

            await CommandHandler(command, payload);
            NoticeBar.Message = "Configuração atualizada.";
            NoticeBar.Severity = InfoBarSeverity.Success;
            NoticeBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            NoticeBar.Message = ex.Message;
            NoticeBar.Severity = InfoBarSeverity.Error;
            NoticeBar.IsOpen = true;
        }
    }

    private async void LanguageComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_applyingState ||
            LanguageComboBox.SelectedItem is not LanguageRow language)
        {
            return;
        }

        await RunAsync(
            "setLanguage",
            new { cultureName = language.CultureName });
    }

    private async void SavePreferences_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "saveLegacyPreferences",
            new
            {
                advancedModeEnabled = AdvancedModeCheckBox.IsChecked == true,
                showDrivingTips = DrivingTipsCheckBox.IsChecked == true
            });

    private async void Discover_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("discoverOmsiProfiles");

    private async void SelectFolder_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("selectOmsiFolder");

    private async void SelectExe_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("selectOmsiExecutable");

    private OmsiInstallRow? SelectedProfile =>
        InstallationsList.SelectedItem as OmsiInstallRow;

    private async void LaunchProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is { } profile)
        {
            await RunAsync("launchOmsiProfile", new { profileId = profile.Id });
        }
    }

    private async void PreferredProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is { } profile)
        {
            await RunAsync("setPreferredOmsiProfile", new { profileId = profile.Id });
        }
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is { } profile)
        {
            await RunAsync("openOmsiProfileFolder", new { profileId = profile.Id });
        }
    }

    private async void RemoveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is { } profile)
        {
            await RunAsync("removeOmsiProfile", new { profileId = profile.Id });
        }
    }
}

public sealed record LanguageRow(string CultureName, string DisplayName);

public sealed record OmsiInstallRow(
    string Id,
    string Name,
    string InstallDirectory,
    string ExecutablePath,
    bool IsPreferred,
    bool IsRunning,
    string Status,
    string Preferred);
