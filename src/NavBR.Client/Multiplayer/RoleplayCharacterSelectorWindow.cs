using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

internal sealed class RoleplayCharacterSelectorWindow : Window
{
    private readonly ListBox _list = new();
    private readonly TextBlock _source = new();

    public RoleplayCharacterOption? SelectedCharacter { get; private set; }

    public RoleplayCharacterSelectorWindow(
        IReadOnlyList<RoleplayCharacterOption> options,
        string? selectedId = null)
    {
        Title = T(
            "Selecionar personagem",
            "Select character",
            "Seleccionar personaje",
            "Charakter auswählen",
            "Sélectionner le personnage");
        Width = 560d;
        Height = 520d;
        MinWidth = 460d;
        MinHeight = 420d;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(6, 16, 26);
        Foreground = Brushes.White;

        var root = new Grid { Margin = new Thickness(22d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = T(
                "PERSONAGEM DO MOTORISTA",
                "DRIVER CHARACTER",
                "PERSONAJE DEL CONDUCTOR",
                "FAHRERCHARAKTER",
                "PERSONNAGE CONDUCTEUR"),
            FontSize = 11d,
            FontWeight = FontWeights.Bold,
            Foreground = Brush(113, 198, 255)
        });
        header.Children.Add(new TextBlock
        {
            Text = T(
                "Escolha um personagem real do mapa. ★ indica o motorista atualmente vinculado ao seu ônibus.",
                "Choose a real map character. ★ marks the driver currently linked to your bus.",
                "Elige un personaje real del mapa. ★ indica el conductor vinculado actualmente a tu autobús.",
                "Wähle einen echten Kartencharakter. ★ markiert den aktuell mit deinem Bus verbundenen Fahrer.",
                "Choisissez un personnage réel de la carte. ★ indique le conducteur actuellement lié à votre bus."),
            Margin = new Thickness(0d, 6d, 0d, 16d),
            FontSize = 12d,
            Foreground = Brush(151, 171, 185),
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _list.ItemsSource = options;
        _list.DisplayMemberPath = nameof(RoleplayCharacterOption.DisplayLabel);
        _list.Background = Brush(9, 20, 29);
        _list.Foreground = Brushes.White;
        _list.BorderBrush = Brush(31, 47, 57);
        _list.BorderThickness = new Thickness(1d);
        _list.Padding = new Thickness(4d);
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedItem is RoleplayCharacterOption option)
            {
                _source.Text = option.SourceValue;
            }
        };
        var selected = options.FirstOrDefault(option =>
            string.Equals(option.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        _list.SelectedItem =
            selected ??
            options.FirstOrDefault(option => option.IsActiveDriver) ??
            options.FirstOrDefault();

        Grid.SetRow(_list, 1);
        root.Children.Add(_list);

        _source.Foreground = Brush(151, 171, 185);
        _source.FontSize = 10d;
        _source.Margin = new Thickness(0d, 10d, 0d, 12d);
        _source.TextWrapping = TextWrapping.Wrap;
        _source.Text = (_list.SelectedItem as RoleplayCharacterOption)?.SourceValue ?? "—";
        Grid.SetRow(_source, 2);
        root.Children.Add(_source);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancel = Button(T("Cancelar", "Cancel", "Cancelar", "Abbrechen", "Annuler"), false);
        cancel.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
        actions.Children.Add(cancel);

        var select = Button(
            T("Usar personagem", "Use character", "Usar personaje", "Charakter verwenden", "Utiliser le personnage"),
            true);
        select.Margin = new Thickness(10d, 0d, 0d, 0d);
        select.Click += (_, _) =>
        {
            if (_list.SelectedItem is not RoleplayCharacterOption option)
            {
                return;
            }

            SelectedCharacter = option;
            DialogResult = true;
            Close();
        };
        actions.Children.Add(select);

        Grid.SetRow(actions, 3);
        root.Children.Add(actions);
        Content = root;
    }

    private static Button Button(string text, bool primary) => new()
    {
        Content = text,
        Padding = new Thickness(16d, 8d, 16d, 8d),
        MinWidth = 105d,
        Background = primary ? Brush(61, 137, 196) : Brush(13, 26, 36),
        Foreground = Brushes.White,
        BorderBrush = primary ? Brush(113, 198, 255) : Brush(31, 47, 57),
        BorderThickness = new Thickness(1d),
        FontWeight = FontWeights.SemiBold,
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));

    private static string T(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
}
