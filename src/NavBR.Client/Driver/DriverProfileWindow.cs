using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;

namespace NavBR.Client.Driver;

internal sealed class DriverProfileWindow : Window
{
    private readonly TextBox _nameBox = new();
    private readonly TextBox _companyBox = new();
    private readonly StackPanel _statsPanel = new();
    private readonly TextBlock _title = new();
    private readonly TextBlock _subtitle = new();
    private readonly TextBlock _nameLabel = new();
    private readonly TextBlock _companyLabel = new();
    private readonly Button _saveButton = new();
    private Action<DriverProfileData>? _profileChanged;

    public DriverProfileWindow(Window owner)
    {
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 760d;
        Height = 650d;
        MinWidth = 680d;
        MinHeight = 580d;
        Background = Brush(7, 12, 17);
        Content = BuildContent();
        LoadProfile(DriverProfileStore.Load());
        ApplyLocalization();

        _profileChanged = profile =>
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(() => RenderStatistics(profile));
                return;
            }
            RenderStatistics(profile);
        };
        DriverProfileStore.ProfileChanged += _profileChanged;
        Closed += (_, _) =>
        {
            if (_profileChanged is not null)
            {
                DriverProfileStore.ProfileChanged -= _profileChanged;
            }
        };
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(28d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 20d) };
        _title.Foreground = Brushes.White;
        _title.FontSize = 25d;
        _title.FontWeight = FontWeights.Bold;
        heading.Children.Add(_title);
        _subtitle.Foreground = Brush(145, 163, 176);
        _subtitle.FontSize = 11.5d;
        _subtitle.TextWrapping = TextWrapping.Wrap;
        _subtitle.Margin = new Thickness(0d, 6d, 0d, 0d);
        heading.Children.Add(_subtitle);
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var body = new StackPanel();
        scroller.Content = body;

        var identity = new Grid();
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        var nameField = BuildField(_nameLabel, _nameBox);
        nameField.Margin = new Thickness(0d, 0d, 7d, 0d);
        Grid.SetColumn(nameField, 0);
        identity.Children.Add(nameField);
        var companyField = BuildField(_companyLabel, _companyBox);
        companyField.Margin = new Thickness(7d, 0d, 0d, 0d);
        Grid.SetColumn(companyField, 1);
        identity.Children.Add(companyField);
        body.Children.Add(NewCard(identity, new Thickness(0d, 0d, 0d, 16d)));

        body.Children.Add(_statsPanel);
        Grid.SetRow(scroller, 1);
        root.Children.Add(scroller);

        _saveButton.Height = 42d;
        _saveButton.MinWidth = 180d;
        _saveButton.HorizontalAlignment = HorizontalAlignment.Right;
        _saveButton.Padding = new Thickness(18d, 8d, 18d, 8d);
        _saveButton.Background = Brush(205, 88, 17);
        _saveButton.Foreground = Brushes.White;
        _saveButton.BorderBrush = Brush(255, 139, 48);
        _saveButton.BorderThickness = new Thickness(1d);
        _saveButton.FontWeight = FontWeights.SemiBold;
        _saveButton.Cursor = System.Windows.Input.Cursors.Hand;
        _saveButton.Click += SaveButton_Click;
        Grid.SetRow(_saveButton, 2);
        root.Children.Add(_saveButton);

        return root;
    }

    private static FrameworkElement BuildField(TextBlock label, TextBox input)
    {
        label.Foreground = Brush(166, 183, 195);
        label.FontSize = 9.5d;
        label.FontWeight = FontWeights.Bold;
        label.Margin = new Thickness(0d, 0d, 0d, 6d);
        input.Height = 36d;
        input.Padding = new Thickness(9d, 6d, 9d, 6d);
        var stack = new StackPanel();
        stack.Children.Add(label);
        stack.Children.Add(input);
        return stack;
    }

    private void LoadProfile(DriverProfileData profile)
    {
        _nameBox.Text = profile.DisplayName;
        _companyBox.Text = profile.CompanyName ?? string.Empty;
        RenderStatistics(profile);
    }

    private void RenderStatistics(DriverProfileData profile)
    {
        _statsPanel.Children.Clear();
        var culture = LocalizationService.CurrentCulture;

        var top = new WrapPanel();
        top.Children.Add(StatCard(Text("DrivingTime"), FormatDuration(profile.TotalDrivingSeconds), "◷"));
        top.Children.Add(StatCard(Text("Distance"), $"{profile.TotalDistanceKm:N1} km", "↝"));
        top.Children.Add(StatCard(Text("Trips"), profile.Trips.ToString("N0", culture), "▣"));
        top.Children.Add(StatCard(Text("AverageSpeed"), $"{profile.AverageMovingSpeedKph:N1} km/h", "≈"));
        top.Children.Add(StatCard(Text("TopSpeed"), $"{profile.HighestSpeedKph:N1} km/h", "▲"));
        _statsPanel.Children.Add(top);

        var last = new StackPanel();
        last.Children.Add(SectionLabel(Text("LastOperation")));
        last.Children.Add(ValueLine(Text("Map"), profile.LastMap));
        last.Children.Add(ValueLine(Text("Line"), profile.LastLine));
        last.Children.Add(ValueLine(Text("Route"), profile.LastRoute));
        last.Children.Add(ValueLine(
            Text("LastDriven"),
            profile.LastDrivenAt?.ToLocalTime().ToString("g", culture)));
        _statsPanel.Children.Add(NewCard(last, new Thickness(0d, 12d, 0d, 0d)));
    }

    private static Border StatCard(string label, string value, string icon)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = icon,
            Foreground = Brush(255, 164, 75),
            FontSize = 17d
        });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            Foreground = Brushes.White,
            FontSize = 19d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 6d, 0d, 0d)
        });
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brush(139, 158, 171),
            FontSize = 9.5d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 4d, 0d, 0d)
        });

        return new Border
        {
            Width = 125d,
            MinHeight = 105d,
            Margin = new Thickness(0d, 0d, 10d, 10d),
            Padding = new Thickness(13d),
            Background = Brush(10, 19, 25),
            BorderBrush = Brush(31, 47, 57),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(11d),
            Child = stack
        };
    }

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        Foreground = Brushes.White,
        FontSize = 13d,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0d, 0d, 0d, 8d)
    };

    private static FrameworkElement ValueLine(string label, string? value)
    {
        var grid = new Grid { Margin = new Thickness(0d, 4d, 0d, 4d) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        var left = new TextBlock { Text = label, Foreground = Brush(129, 148, 162), FontSize = 10d };
        var right = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
            Foreground = Brush(218, 230, 238),
            FontSize = 11d,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private static Border NewCard(UIElement child, Thickness margin) => new()
    {
        Margin = margin,
        Padding = new Thickness(16d),
        Background = Brush(10, 19, 25),
        BorderBrush = Brush(31, 47, 57),
        BorderThickness = new Thickness(1d),
        CornerRadius = new CornerRadius(12d),
        Child = child
    };

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var profile = DriverProfileStore.Load();
        DriverProfileStore.Save(profile with
        {
            DisplayName = _nameBox.Text,
            CompanyName = _companyBox.Text
        });
        Close();
    }

    private void ApplyLocalization()
    {
        Title = Text("ProfileTitle");
        _title.Text = Text("ProfileTitle");
        _subtitle.Text = Text("ProfileSubtitle");
        _nameLabel.Text = Text("DriverName");
        _companyLabel.Text = Text("VirtualCompany");
        _saveButton.Content = Text("SaveProfile");
        RenderStatistics(DriverProfileStore.Load());
    }

    private static string FormatDuration(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0d, seconds));
        return span.TotalHours >= 1d
            ? $"{(int)span.TotalHours:00}:{span.Minutes:00}"
            : $"{span.Minutes:00}:{span.Seconds:00}";
    }

    private static string Text(string key)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        var table = language switch
        {
            "pt" => Pt,
            "es" => Es,
            "de" => De,
            "fr" => Fr,
            _ => En
        };
        return table.TryGetValue(key, out var value) ? value : key;
    }

    private static readonly IReadOnlyDictionary<string, string> En = T(
        ("ProfileTitle", "Driver profile"), ("ProfileSubtitle", "Your local identity and driving history in NavBR."),
        ("DriverName", "DRIVER NAME"), ("VirtualCompany", "VIRTUAL COMPANY (OPTIONAL)"),
        ("SaveProfile", "Save profile"), ("DrivingTime", "Driving time"), ("Distance", "Distance"),
        ("Trips", "Trips"), ("AverageSpeed", "Average speed"), ("TopSpeed", "Top speed"),
        ("LastOperation", "Last operation"), ("Map", "Map"), ("Line", "Line"), ("Route", "Route"), ("LastDriven", "Last driven"));
    private static readonly IReadOnlyDictionary<string, string> Pt = T(
        ("ProfileTitle", "Perfil do motorista"), ("ProfileSubtitle", "Sua identidade local e seu histórico de condução no NavBR."),
        ("DriverName", "NOME DO MOTORISTA"), ("VirtualCompany", "EMPRESA VIRTUAL (OPCIONAL)"),
        ("SaveProfile", "Salvar perfil"), ("DrivingTime", "Tempo dirigindo"), ("Distance", "Distância"),
        ("Trips", "Viagens"), ("AverageSpeed", "Velocidade média"), ("TopSpeed", "Maior velocidade"),
        ("LastOperation", "Última operação"), ("Map", "Mapa"), ("Line", "Linha"), ("Route", "Rota"), ("LastDriven", "Última condução"));
    private static readonly IReadOnlyDictionary<string, string> Es = T(
        ("ProfileTitle", "Perfil del conductor"), ("ProfileSubtitle", "Tu identidad local y tu historial de conducción en NavBR."),
        ("DriverName", "NOMBRE DEL CONDUCTOR"), ("VirtualCompany", "EMPRESA VIRTUAL (OPCIONAL)"),
        ("SaveProfile", "Guardar perfil"), ("DrivingTime", "Tiempo conduciendo"), ("Distance", "Distancia"),
        ("Trips", "Viajes"), ("AverageSpeed", "Velocidad media"), ("TopSpeed", "Velocidad máxima"),
        ("LastOperation", "Última operación"), ("Map", "Mapa"), ("Line", "Línea"), ("Route", "Ruta"), ("LastDriven", "Última conducción"));
    private static readonly IReadOnlyDictionary<string, string> De = T(
        ("ProfileTitle", "Fahrerprofil"), ("ProfileSubtitle", "Deine lokale Identität und Fahrhistorie in NavBR."),
        ("DriverName", "FAHRERNAME"), ("VirtualCompany", "VIRTUELLES UNTERNEHMEN (OPTIONAL)"),
        ("SaveProfile", "Profil speichern"), ("DrivingTime", "Fahrzeit"), ("Distance", "Distanz"),
        ("Trips", "Fahrten"), ("AverageSpeed", "Durchschnitt"), ("TopSpeed", "Höchstgeschwindigkeit"),
        ("LastOperation", "Letzter Einsatz"), ("Map", "Karte"), ("Line", "Linie"), ("Route", "Route"), ("LastDriven", "Zuletzt gefahren"));
    private static readonly IReadOnlyDictionary<string, string> Fr = T(
        ("ProfileTitle", "Profil conducteur"), ("ProfileSubtitle", "Votre identité locale et votre historique de conduite dans NavBR."),
        ("DriverName", "NOM DU CONDUCTEUR"), ("VirtualCompany", "ENTREPRISE VIRTUELLE (OPTIONNEL)"),
        ("SaveProfile", "Enregistrer le profil"), ("DrivingTime", "Temps de conduite"), ("Distance", "Distance"),
        ("Trips", "Trajets"), ("AverageSpeed", "Vitesse moyenne"), ("TopSpeed", "Vitesse maximale"),
        ("LastOperation", "Dernière opération"), ("Map", "Carte"), ("Line", "Ligne"), ("Route", "Itinéraire"), ("LastDriven", "Dernière conduite"));

    private static IReadOnlyDictionary<string, string> T(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
