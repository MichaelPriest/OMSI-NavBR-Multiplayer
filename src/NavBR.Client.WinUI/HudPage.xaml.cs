using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavBR.Client.WinUI;

public sealed partial class HudPage : UserControl
{
    public Func<string, object?, Task>? CommandHandler { get; set; }

    public HudPage()
    {
        InitializeComponent();
    }

    public void ApplyState(JsonElement state)
    {
        var hud = JsonState.Property(
            JsonState.Property(state, "system"),
            "hud");

        EnabledToggle.IsOn = JsonState.Bool(hud, "enabled");
        ScaleNumberBox.Value = JsonState.Double(hud, "scale") ?? 1d;
        OpacityNumberBox.Value = JsonState.Double(hud, "opacity") ?? 0.92d;
        FuelCheckBox.IsChecked = JsonState.Bool(hud, "showFuel");
        PedalsCheckBox.IsChecked = JsonState.Bool(hud, "showPedals");
        StatusCheckBox.IsChecked = JsonState.Bool(hud, "showStatus");
        MinimapCheckBox.IsChecked = JsonState.Bool(hud, "showMinimap");
        MultiplayerCheckBox.IsChecked = JsonState.Bool(hud, "showMultiplayer");
        AlertsCheckBox.IsChecked = JsonState.Bool(hud, "showAlerts");
        SideIndicatorsCheckBox.IsChecked = JsonState.Bool(hud, "showSideIndicators");
        AutoScaleCheckBox.IsChecked = JsonState.Bool(hud, "autoScale");

        var presets = JsonState.Array(hud, "presets")
            .Select(x => new HudOption(
                JsonState.String(x, "id") ?? string.Empty,
                JsonState.String(x, "displayName") ?? "Preset"))
            .Where(x => x.Id.Length > 0)
            .ToArray();
        PresetComboBox.ItemsSource = presets;
        Select(PresetComboBox, presets, JsonState.String(hud, "preset"));

        var themes = JsonState.Array(hud, "themes")
            .Select(x => new HudOption(
                JsonState.String(x, "id") ?? string.Empty,
                JsonState.String(x, "displayName") ?? "Tema"))
            .Where(x => x.Id.Length > 0)
            .ToArray();
        ThemeComboBox.ItemsSource = themes;
        Select(ThemeComboBox, themes, JsonState.String(hud, "theme"));

        var anchors = JsonState.Array(hud, "anchors")
            .Select(x => new HudOption(
                JsonState.String(x, "id") ?? string.Empty,
                JsonState.String(x, "displayName") ?? "Posição"))
            .Where(x => x.Id.Length > 0)
            .ToArray();
        AnchorComboBox.ItemsSource = anchors;
        Select(AnchorComboBox, anchors, JsonState.String(hud, "anchor"));
    }

    private static void Select(
        ComboBox combo,
        IReadOnlyList<HudOption> options,
        string? id)
    {
        combo.SelectedItem = options.FirstOrDefault(x =>
            string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        if (combo.SelectedIndex < 0 && options.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private object BuildPayload()
    {
        var preset = PresetComboBox.SelectedItem as HudOption;
        var theme = ThemeComboBox.SelectedItem as HudOption;
        var anchor = AnchorComboBox.SelectedItem as HudOption;

        return new
        {
            enabled = EnabledToggle.IsOn,
            preset = preset?.Id,
            theme = theme?.Id,
            anchor = anchor?.Id,
            scale = double.IsFinite(ScaleNumberBox.Value) ? ScaleNumberBox.Value : 1d,
            width = 520d,
            height = 0d,
            opacity = double.IsFinite(OpacityNumberBox.Value) ? OpacityNumberBox.Value : 0.92d,
            autoScale = AutoScaleCheckBox.IsChecked == true,
            showFuel = FuelCheckBox.IsChecked == true,
            showPedals = PedalsCheckBox.IsChecked == true,
            showStatus = StatusCheckBox.IsChecked == true,
            showMinimap = MinimapCheckBox.IsChecked == true,
            showMultiplayer = MultiplayerCheckBox.IsChecked == true,
            showAlerts = AlertsCheckBox.IsChecked == true,
            showSideIndicators = SideIndicatorsCheckBox.IsChecked == true,
            minimapScale = 1d,
            multiplayerScale = 1d,
            alertsScale = 1d,
            sideIndicatorsScale = 1d
        };
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
            NoticeBar.Message = "HUD atualizado.";
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

    private async void Preview_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("previewHudSettings", BuildPayload());

    private async void Save_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("saveHudSettings", BuildPayload());

    private async void ClearPreview_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("clearHudPreview");

    private async void Reset_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("resetHudSettings");

    private async void ToggleLayout_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("toggleHudLayout");
}

public sealed record HudOption(string Id, string DisplayName);
