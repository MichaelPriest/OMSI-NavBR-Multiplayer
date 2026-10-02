using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavBR.Client.WinUI;

public sealed partial class HudPage : UserControl
{
    public Func<string, object?, Task>? CommandHandler { get; set; }

    private double _width = 520d;
    private double _height;
    private double _minimapScale = 1d;
    private double _multiplayerScale = 1d;
    private double _alertsScale = 1d;
    private double _sideIndicatorsScale = 1d;
    private bool _applyingState;
    private bool _draftDirty;

    public HudPage()
    {
        InitializeComponent();

        // Runtime state is pushed several times per second while OMSI is
        // running. Mark the native editor as a local draft as soon as the user
        // changes any field so a telemetry refresh cannot immediately restore
        // the last persisted HUD value and close/reset the control.
        EnabledToggle.Toggled += (_, _) => MarkDraftDirty();
        PresetComboBox.SelectionChanged += (_, _) => MarkDraftDirty();
        ThemeComboBox.SelectionChanged += (_, _) => MarkDraftDirty();
        AnchorComboBox.SelectionChanged += (_, _) => MarkDraftDirty();
        ScaleNumberBox.ValueChanged += (_, _) => MarkDraftDirty();
        OpacityNumberBox.ValueChanged += (_, _) => MarkDraftDirty();
        FuelCheckBox.Click += (_, _) => MarkDraftDirty();
        PedalsCheckBox.Click += (_, _) => MarkDraftDirty();
        StatusCheckBox.Click += (_, _) => MarkDraftDirty();
        MinimapCheckBox.Click += (_, _) => MarkDraftDirty();
        MultiplayerCheckBox.Click += (_, _) => MarkDraftDirty();
        AlertsCheckBox.Click += (_, _) => MarkDraftDirty();
        SideIndicatorsCheckBox.Click += (_, _) => MarkDraftDirty();
        AutoScaleCheckBox.Click += (_, _) => MarkDraftDirty();
    }

    public void ApplyState(JsonElement state)
    {
        var hud = JsonState.Property(
            JsonState.Property(state, "system"),
            "hud");

        // Do not overwrite an unsaved native HUD draft with the high-frequency
        // telemetry snapshot. This was the cause of ComboBox values "blinking"
        // back to the saved option before the user could press Save.
        if (_draftDirty)
        {
            return;
        }

        _applyingState = true;
        try
        {
        EnabledToggle.IsOn = JsonState.Bool(hud, "enabled");
        ScaleNumberBox.Value = JsonState.Double(hud, "scale") ?? 1d;
        OpacityNumberBox.Value = JsonState.Double(hud, "opacity") ?? 0.92d;
        _width = JsonState.Double(hud, "width") ?? _width;
        _height = JsonState.Double(hud, "height") ?? _height;
        _minimapScale = JsonState.Double(hud, "minimapScale") ?? _minimapScale;
        _multiplayerScale = JsonState.Double(hud, "multiplayerScale") ?? _multiplayerScale;
        _alertsScale = JsonState.Double(hud, "alertsScale") ?? _alertsScale;
        _sideIndicatorsScale = JsonState.Double(hud, "sideIndicatorsScale") ?? _sideIndicatorsScale;
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
        SyncOptions(
            PresetComboBox,
            presets,
            JsonState.String(hud, "preset"));

        var themes = JsonState.Array(hud, "themes")
            .Select(x => new HudOption(
                JsonState.String(x, "id") ?? string.Empty,
                JsonState.String(x, "displayName") ?? "Tema"))
            .Where(x => x.Id.Length > 0)
            .ToArray();
        SyncOptions(
            ThemeComboBox,
            themes,
            JsonState.String(hud, "theme"));

        var anchors = JsonState.Array(hud, "anchors")
            .Select(x => new HudOption(
                JsonState.String(x, "id") ?? string.Empty,
                JsonState.String(x, "displayName") ?? "Posição"))
            .Where(x => x.Id.Length > 0)
            .ToArray();
        SyncOptions(
            AnchorComboBox,
            anchors,
            JsonState.String(hud, "anchor"));
        }
        finally
        {
            _applyingState = false;
        }
    }

    private void MarkDraftDirty()
    {
        if (!_applyingState)
        {
            _draftDirty = true;
        }
    }

    private static void SyncOptions(
        ComboBox combo,
        IReadOnlyList<HudOption> options,
        string? id)
    {
        var current = combo.ItemsSource as IEnumerable<HudOption>;
        var sameOptions =
            current is not null &&
            current.SequenceEqual(options);

        if (!sameOptions)
        {
            combo.ItemsSource = options;
        }

        // Never touch selection while the flyout is open. Reassigning even the
        // same selection causes WinUI ComboBox to collapse on state refresh.
        if (combo.IsDropDownOpen)
        {
            return;
        }

        var selected = combo.SelectedItem as HudOption;
        if (string.Equals(
                selected?.Id,
                id,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var source = (combo.ItemsSource as IEnumerable<HudOption>)?.ToArray()
            ?? options.ToArray();
        combo.SelectedItem = source.FirstOrDefault(x =>
            string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        if (combo.SelectedIndex < 0 && source.Length > 0)
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
            width = _width,
            height = _height,
            opacity = double.IsFinite(OpacityNumberBox.Value) ? OpacityNumberBox.Value : 0.92d,
            autoScale = AutoScaleCheckBox.IsChecked == true,
            showFuel = FuelCheckBox.IsChecked == true,
            showPedals = PedalsCheckBox.IsChecked == true,
            showStatus = StatusCheckBox.IsChecked == true,
            showMinimap = MinimapCheckBox.IsChecked == true,
            showMultiplayer = MultiplayerCheckBox.IsChecked == true,
            showAlerts = AlertsCheckBox.IsChecked == true,
            showSideIndicators = SideIndicatorsCheckBox.IsChecked == true,
            minimapScale = _minimapScale,
            multiplayerScale = _multiplayerScale,
            alertsScale = _alertsScale,
            sideIndicatorsScale = _sideIndicatorsScale
        };
    }

    private async Task<bool> RunAsync(string command, object? payload = null)
    {
        try
        {
            if (CommandHandler is null)
            {
                return false;
            }

            await CommandHandler(command, payload);
            NoticeBar.Message = "HUD atualizado.";
            NoticeBar.Severity = InfoBarSeverity.Success;
            NoticeBar.IsOpen = true;
            return true;
        }
        catch (Exception ex)
        {
            NoticeBar.Message = ex.Message;
            NoticeBar.Severity = InfoBarSeverity.Error;
            NoticeBar.IsOpen = true;
            return false;
        }
    }

    private async void Preview_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("previewHudSettings", BuildPayload());

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (await RunAsync("saveHudSettings", BuildPayload()))
        {
            _draftDirty = false;
        }
    }

    private async void ClearPreview_Click(object sender, RoutedEventArgs e)
    {
        if (await RunAsync("clearHudPreview"))
        {
            _draftDirty = false;
        }
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (await RunAsync("resetHudSettings"))
        {
            _draftDirty = false;
        }
    }

    private async void ToggleLayout_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("toggleHudLayout");
}

public sealed record HudOption(string Id, string DisplayName);
