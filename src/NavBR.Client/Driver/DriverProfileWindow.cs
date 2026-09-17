using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private readonly TextBlock _avatarText = new();
    private readonly TextBlock _profileNamePreview = new();
    private readonly TextBlock _companyPreview = new();
    private readonly Button _saveButton = new();
    private Action<DriverProfileData>? _profileChanged;

    public DriverProfileWindow(Window owner)
    {
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 940d;
        Height = 700d;
        MinWidth = 820d;
        MinHeight = 610d;
        Background = Brush(4, 10, 16);
        Content = BuildContent();
        LoadProfile(DriverProfileStore.Load());
        ApplyLocalization();

        _profileChanged = profile =>
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(() =>
                {
                    RenderStatistics(profile);
                    RefreshPreview();
                });
                return;
            }
            RenderStatistics(profile);
            RefreshPreview();
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
        var root = new Grid { Margin = new Thickness(24d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { Margin = new Thickness(0d, 0d, 0d, 18d) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var heading = new StackPanel();
        _title.Foreground = Brushes.White;
        _title.FontSize = 25d;
        _title.FontWeight = FontWeights.Bold;
        heading.Children.Add(_title);
        _subtitle.Foreground = Brush(128, 151, 168);
        _subtitle.FontSize = 11.5d;
        _subtitle.TextWrapping = TextWrapping.Wrap;
        _subtitle.Margin = new Thickness(0d, 5d, 16d, 0d);
        heading.Children.Add(_subtitle);
        Grid.SetColumn(heading, 0);
        header.Children.Add(heading);

        var badge = new Border
        {
            Padding = new Thickness(11d, 6d, 11d, 6d),
            Background = Brush(8, 39, 58),
            BorderBrush = Brush(28, 92, 126),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(999d),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "PERFIL • LOCAL",
                Foreground = Brush(82, 196, 255),
                FontSize = 9d,
                FontWeight = FontWeights.Bold
            }
        };
        Grid.SetColumn(badge, 1);
        header.Children.Add(badge);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false
        };
        var body = new StackPanel();
        scroller.Content = body;

        body.Children.Add(BuildProfileHero());

        var identity = new Grid { Margin = new Thickness(0d, 0d, 0d, 16d) };
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

        var footer = new Grid { Margin = new Thickness(0d, 18d, 0d, 0d) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock
        {
            Text = Text("LocalProfileNote"),
            Foreground = Brush(99, 126, 143),
            FontSize = 9.5d,
            VerticalAlignment = VerticalAlignment.Center
        });
        _saveButton.Height = 42d;
        _saveButton.MinWidth = 180d;
        _saveButton.HorizontalAlignment = HorizontalAlignment.Right;
        _saveButton.Padding = new Thickness(18d, 8d, 18d, 8d);
        _saveButton.Background = Brush(19, 103, 171);
        _saveButton.Foreground = Brushes.White;
        _saveButton.BorderBrush = Brush(55, 155, 221);
        _saveButton.BorderThickness = new Thickness(1d);
        _saveButton.FontWeight = FontWeights.SemiBold;
        _saveButton.Cursor = System.Windows.Input.Cursors.Hand;
        _saveButton.Click += SaveButton_Click;
        Grid.SetColumn(_saveButton, 1);
        footer.Children.Add(_saveButton);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        _nameBox.TextChanged += (_, _) => RefreshPreview();
        _companyBox.TextChanged += (_, _) => RefreshPreview();
        return root;
    }

    private Border BuildProfileHero()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var avatar = new Border
        {
            Width = 88d,
            Height = 88d,
            Margin = new Thickness(0d, 0d, 18d, 0d),
            Background = new LinearGradientBrush(Color.FromRgb(20, 85, 126), Color.FromRgb(8, 39, 60), 45d),
            BorderBrush = Brush(61, 150, 203),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(44d),
            Child = _avatarText
        };
        _avatarText.HorizontalAlignment = HorizontalAlignment.Center;
        _avatarText.VerticalAlignment = VerticalAlignment.Center;
        _avatarText.Foreground = Brushes.White;
        _avatarText.FontSize = 27d;
        _avatarText.FontWeight = FontWeights.Bold;
        Grid.SetColumn(avatar, 0);
        grid.Children.Add(avatar);

        var identity = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        identity.Children.Add(new TextBlock
        {
            Text = Text("CareerCard").ToUpperInvariant(),
            Foreground = Brush(92, 154, 190),
            FontSize = 9d,
            FontWeight = FontWeights.Bold
        });
        _profileNamePreview.Foreground = Brushes.White;
        _profileNamePreview.FontSize = 23d;
        _profileNamePreview.FontWeight = FontWeights.Bold;
        _profileNamePreview.Margin = new Thickness(0d, 4d, 0d, 0d);
        identity.Children.Add(_profileNamePreview);
        _companyPreview.Foreground = Brush(135, 160, 176);
        _companyPreview.FontSize = 10.5d;
        _companyPreview.Margin = new Thickness(0d, 5d, 0d, 0d);
        identity.Children.Add(_companyPreview);
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);

        var state = new Border
        {
            Padding = new Thickness(12d, 8d, 12d, 8d),
            Background = Brush(8, 45, 34),
            BorderBrush = Brush(32, 104, 77),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(10d),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = Text("ProfileActive"),
                Foreground = Brush(101, 225, 161),
                FontSize = 9.5d,
                FontWeight = FontWeights.Bold
            }
        };
        Grid.SetColumn(state, 2);
        grid.Children.Add(state);

        return new Border
        {
            Margin = new Thickness(0d, 0d, 0d, 16d),
            Padding = new Thickness(20d),
            Background = new LinearGradientBrush(Color.FromRgb(9, 29, 43), Color.FromRgb(6, 16, 24), 0d),
            BorderBrush = Brush(27, 72, 97),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(15d),
            Child = grid
        };
    }

    private static FrameworkElement BuildField(TextBlock label, TextBox input)
    {
        label.Foreground = Brush(137, 162, 179);
        label.FontSize = 9.2d;
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
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        var name = string.IsNullOrWhiteSpace(_nameBox.Text) ? Text("UnnamedDriver") : _nameBox.Text.Trim();
        _profileNamePreview.Text = name;
        _companyPreview.Text = string.IsNullOrWhiteSpace(_companyBox.Text)
            ? Text("IndependentDriver")
            : _companyBox.Text.Trim();
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        _avatarText.Text = parts.Length == 0
            ? "NB"
            : string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }

    private void RenderStatistics(DriverProfileData profile)
    {
        _statsPanel.Children.Clear();
        var culture = LocalizationService.CurrentCulture;

        _statsPanel.Children.Add(new TextBlock
        {
            Text = Text("Statistics").ToUpperInvariant(),
            Foreground = Brush(101, 156, 188),
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(2d, 0d, 0d, 9d)
        });

        var top = new UniformGrid
        {
            Columns = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
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
        _statsPanel.Children.Add(NewCard(last, new Thickness(0d, 10d, 0d, 0d)));
    }

    private static Border StatCard(string label, string value, string icon)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = icon,
            Foreground = Brush(82, 196, 255),
            FontSize = 16d
        });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            Foreground = Brushes.White,
            FontSize = 19d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 5d, 0d, 0d)
        });
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brush(126, 149, 164),
            FontSize = 9.2d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 4d, 0d, 0d)
        });

        return new Border
        {
            MinHeight = 103d,
            Margin = new Thickness(0d, 0d, 9d, 9d),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(13d),
            Background = Brush(7, 18, 25),
            BorderBrush = Brush(25, 47, 60),
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
        var left = new TextBlock { Text = label, Foreground = Brush(116, 144, 161), FontSize = 10d };
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
        Background = Brush(7, 18, 25),
        BorderBrush = Brush(25, 47, 60),
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
        RefreshPreview();
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
        ("ProfileTitle", "Driver profile"), ("ProfileSubtitle", "Your identity, career and driving history in NavBR."),
        ("DriverName", "DRIVER NAME"), ("VirtualCompany", "VIRTUAL COMPANY (OPTIONAL)"),
        ("SaveProfile", "Save profile"), ("DrivingTime", "Driving time"), ("Distance", "Distance"),
        ("Trips", "Trips"), ("AverageSpeed", "Average speed"), ("TopSpeed", "Top speed"),
        ("LastOperation", "Last operation"), ("Map", "Map"), ("Line", "Line"), ("Route", "Route"), ("LastDriven", "Last driven"),
        ("CareerCard", "Driver career"), ("ProfileActive", "PROFILE ACTIVE"), ("Statistics", "Statistics"),
        ("UnnamedDriver", "NavBR Driver"), ("IndependentDriver", "Independent driver"),
        ("LocalProfileNote", "Profile statistics are stored locally and updated from OMSI telemetry."));
    private static readonly IReadOnlyDictionary<string, string> Pt = T(
        ("ProfileTitle", "Perfil do motorista"), ("ProfileSubtitle", "Sua identidade, carreira e histórico de condução no NavBR."),
        ("DriverName", "NOME DO MOTORISTA"), ("VirtualCompany", "EMPRESA VIRTUAL (OPCIONAL)"),
        ("SaveProfile", "Salvar perfil"), ("DrivingTime", "Tempo dirigindo"), ("Distance", "Distância"),
        ("Trips", "Viagens"), ("AverageSpeed", "Velocidade média"), ("TopSpeed", "Maior velocidade"),
        ("LastOperation", "Última operação"), ("Map", "Mapa"), ("Line", "Linha"), ("Route", "Rota"), ("LastDriven", "Última condução"),
        ("CareerCard", "Carreira do motorista"), ("ProfileActive", "PERFIL ATIVO"), ("Statistics", "Estatísticas"),
        ("UnnamedDriver", "Motorista NavBR"), ("IndependentDriver", "Motorista independente"),
        ("LocalProfileNote", "As estatísticas ficam salvas localmente e são atualizadas pela telemetria do OMSI."));
    private static readonly IReadOnlyDictionary<string, string> Es = T(
        ("ProfileTitle", "Perfil del conductor"), ("ProfileSubtitle", "Tu identidad, carrera e historial de conducción en NavBR."),
        ("DriverName", "NOMBRE DEL CONDUCTOR"), ("VirtualCompany", "EMPRESA VIRTUAL (OPCIONAL)"),
        ("SaveProfile", "Guardar perfil"), ("DrivingTime", "Tiempo conduciendo"), ("Distance", "Distancia"),
        ("Trips", "Viajes"), ("AverageSpeed", "Velocidad media"), ("TopSpeed", "Velocidad máxima"),
        ("LastOperation", "Última operación"), ("Map", "Mapa"), ("Line", "Línea"), ("Route", "Ruta"), ("LastDriven", "Última conducción"),
        ("CareerCard", "Carrera del conductor"), ("ProfileActive", "PERFIL ACTIVO"), ("Statistics", "Estadísticas"),
        ("UnnamedDriver", "Conductor NavBR"), ("IndependentDriver", "Conductor independiente"),
        ("LocalProfileNote", "Las estadísticas se guardan localmente y se actualizan con la telemetría de OMSI."));
    private static readonly IReadOnlyDictionary<string, string> De = T(
        ("ProfileTitle", "Fahrerprofil"), ("ProfileSubtitle", "Deine Identität, Karriere und Fahrhistorie in NavBR."),
        ("DriverName", "FAHRERNAME"), ("VirtualCompany", "VIRTUELLES UNTERNEHMEN (OPTIONAL)"),
        ("SaveProfile", "Profil speichern"), ("DrivingTime", "Fahrzeit"), ("Distance", "Distanz"),
        ("Trips", "Fahrten"), ("AverageSpeed", "Durchschnitt"), ("TopSpeed", "Höchstgeschwindigkeit"),
        ("LastOperation", "Letzter Einsatz"), ("Map", "Karte"), ("Line", "Linie"), ("Route", "Route"), ("LastDriven", "Zuletzt gefahren"),
        ("CareerCard", "Fahrerkarriere"), ("ProfileActive", "PROFIL AKTIV"), ("Statistics", "Statistik"),
        ("UnnamedDriver", "NavBR-Fahrer"), ("IndependentDriver", "Unabhängiger Fahrer"),
        ("LocalProfileNote", "Profilstatistiken werden lokal gespeichert und durch OMSI-Telemetrie aktualisiert."));
    private static readonly IReadOnlyDictionary<string, string> Fr = T(
        ("ProfileTitle", "Profil conducteur"), ("ProfileSubtitle", "Votre identité, carrière et historique de conduite dans NavBR."),
        ("DriverName", "NOM DU CONDUCTEUR"), ("VirtualCompany", "ENTREPRISE VIRTUELLE (OPTIONNEL)"),
        ("SaveProfile", "Enregistrer le profil"), ("DrivingTime", "Temps de conduite"), ("Distance", "Distance"),
        ("Trips", "Trajets"), ("AverageSpeed", "Vitesse moyenne"), ("TopSpeed", "Vitesse maximale"),
        ("LastOperation", "Dernière opération"), ("Map", "Carte"), ("Line", "Ligne"), ("Route", "Itinéraire"), ("LastDriven", "Dernière conduite"),
        ("CareerCard", "Carrière conducteur"), ("ProfileActive", "PROFIL ACTIF"), ("Statistics", "Statistiques"),
        ("UnnamedDriver", "Conducteur NavBR"), ("IndependentDriver", "Conducteur indépendant"),
        ("LocalProfileNote", "Les statistiques sont stockées localement et mises à jour par la télémétrie OMSI."));

    private static IReadOnlyDictionary<string, string> T(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}