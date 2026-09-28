using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavBR.Client.WinUI;

public sealed partial class RoleplayPage : UserControl
{
    public Func<string, object?, Task>? CommandHandler { get; set; }

    private readonly ObservableCollection<RoleplayCharacterRow> _characters = new();
    private readonly ObservableCollection<string> _interactions = new();
    private bool _applyingState;

    public RoleplayPage()
    {
        InitializeComponent();
        CharacterComboBox.ItemsSource = _characters;
        InteractionsList.ItemsSource = _interactions;
    }

    public void ApplyState(JsonElement state)
    {
        var roleplay = JsonState.Property(state, "roleplay");
        _applyingState = true;
        try
        {
            EnabledToggle.IsOn = JsonState.Bool(roleplay, "enabled");
        }
        finally
        {
            _applyingState = false;
        }

        var runtimeAvailable = JsonState.Bool(roleplay, "runtimeAvailable");
        var active = JsonState.Bool(roleplay, "active");
        RuntimeText.Text = runtimeAvailable ? "PRONTO" : "INDISPONÍVEL";
        ActiveText.Text = active ? "ATIVO" : "PARADO";
        BusDistanceText.Text = JsonState.Double(roleplay, "busDistanceMeters") is double distance
            ? $"{distance:0.0} m"
            : "—";

        var current = JsonState.Property(roleplay, "current");
        ActivityText.Text = JsonState.String(current, "activity") ?? "—";
        PositionText.Text = JsonState.IsObject(current)
            ? $"Posição: {JsonState.Double(current, "localX") ?? 0d:0.00}, {JsonState.Double(current, "localY") ?? 0d:0.00}, {JsonState.Double(current, "localZ") ?? 0d:0.00} · rumo {JsonState.Double(current, "headingDegrees") ?? 0d:0}°"
            : "Posição: —";

        SubtitleText.Text = JsonState.String(roleplay, "errorMessage") ??
            (JsonState.Bool(roleplay, "mapReady")
                ? "Mapa pronto para personagem RP."
                : "Carregue um mapa no OMSI para usar o personagem RP.");

        var selectedId = JsonState.String(
            JsonState.Property(roleplay, "selected"),
            "id");

        _characters.Clear();
        foreach (var character in JsonState.Array(roleplay, "characters"))
        {
            _characters.Add(new RoleplayCharacterRow(
                JsonState.String(character, "id") ?? string.Empty,
                JsonState.String(character, "displayName") ?? "Personagem",
                JsonState.Bool(character, "isActiveDriver")));
        }

        _applyingState = true;
        try
        {
            CharacterComboBox.SelectedItem = _characters.FirstOrDefault(x =>
                string.Equals(x.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _applyingState = false;
        }

        _interactions.Clear();
        foreach (var interaction in JsonState.Array(roleplay, "interactions"))
        {
            var name = JsonState.String(interaction, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                _interactions.Add(name);
            }
        }

        StartButton.IsEnabled =
            EnabledToggle.IsOn &&
            runtimeAvailable &&
            JsonState.Bool(roleplay, "mapReady") &&
            !active;
        StopButton.IsEnabled = active;
        EnterBusButton.IsEnabled = JsonState.Bool(roleplay, "canEnterBus");
        TriggerInteractionButton.IsEnabled =
            JsonState.Bool(roleplay, "canInteractWithBus") &&
            _interactions.Count > 0;
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
            NoticeBar.Message = "Estado RP atualizado.";
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

    private async void EnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_applyingState)
        {
            return;
        }

        await RunAsync(
            "setRoleplayEnabled",
            new { enabled = EnabledToggle.IsOn });
    }

    private async void CharacterComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_applyingState ||
            CharacterComboBox.SelectedItem is not RoleplayCharacterRow character)
        {
            return;
        }

        await RunAsync(
            "selectRoleplayCharacter",
            new { characterId = character.Id });
    }

    private async void Start_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("startRoleplay");

    private async void EnterBus_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("enterRoleplayBus");

    private async void Stop_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("stopRoleplay");

    private async void TriggerInteraction_Click(object sender, RoutedEventArgs e)
    {
        if (InteractionsList.SelectedItem is not string interaction)
        {
            return;
        }

        await RunAsync(
            "triggerRoleplayVehicle",
            new { triggerName = interaction });
    }
}

public sealed record RoleplayCharacterRow(
    string Id,
    string DisplayName,
    bool IsActiveDriver);
