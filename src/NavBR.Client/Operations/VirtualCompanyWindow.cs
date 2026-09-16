using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Driver;
using NavBR.Client.Localization;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Operations;

internal sealed class VirtualCompanyWindow : Window
{
    private readonly Func<VehicleTelemetry?> _telemetryProvider;
    private readonly TextBox _nameBox = new();
    private readonly TextBox _shortNameBox = new();
    private readonly TextBox _baseMapBox = new();
    private readonly TextBox _fleetNumberBox = new();
    private readonly StackPanel _fleetPanel = new();
    private readonly TextBlock _title = new();
    private readonly TextBlock _subtitle = new();
    private readonly TextBlock _nameLabel = new();
    private readonly TextBlock _shortNameLabel = new();
    private readonly TextBlock _baseMapLabel = new();
    private readonly TextBlock _fleetHeading = new();
    private readonly TextBlock _currentBusText = new();
    private readonly TextBlock _fleetNumberLabel = new();
    private readonly Button _saveButton = new();
    private readonly Button _registerButton = new();
    private Action<VirtualCompanyData>? _companyChanged;

    public VirtualCompanyWindow(Window owner, Func<VehicleTelemetry?> telemetryProvider)
    {
        _telemetryProvider = telemetryProvider;
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 860d;
        Height = 720d;
        MinWidth = 760d;
        MinHeight = 620d;
        Background = Brush(7, 12, 17);
        Content = BuildContent();
        LoadCompany(VirtualCompanyStore.Load());
        ApplyLocalization();

        _companyChanged = company =>
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(() => RenderFleet(company));
                return;
            }
            RenderFleet(company);
        };
        VirtualCompanyStore.CompanyChanged += _companyChanged;
        Closed += (_, _) =>
        {
            if (_companyChanged is not null)
            {
                VirtualCompanyStore.CompanyChanged -= _companyChanged;
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

        var identityGrid = new Grid();
        identityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2d, GridUnitType.Star) });
        identityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        identityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2d, GridUnitType.Star) });
        AddField(identityGrid, _nameLabel, _nameBox, 0, new Thickness(0d, 0d, 8d, 0d));
        AddField(identityGrid, _shortNameLabel, _shortNameBox, 1, new Thickness(8d, 0d, 8d, 0d));
        AddField(identityGrid, _baseMapLabel, _baseMapBox, 2, new Thickness(8d, 0d, 0d, 0d));
        body.Children.Add(NewCard(identityGrid, new Thickness(0d, 0d, 0d, 16d)));

        var currentBusStack = new StackPanel();
        _currentBusText.Foreground = Brush(210, 226, 236);
        _currentBusText.FontSize = 12d;
        _currentBusText.FontWeight = FontWeights.SemiBold;
        _currentBusText.TextWrapping = TextWrapping.Wrap;
        currentBusStack.Children.Add(_currentBusText);

        _fleetNumberLabel.Foreground = Brush(150, 170, 183);
        _fleetNumberLabel.FontSize = 9.5d;
        _fleetNumberLabel.FontWeight = FontWeights.Bold;
        _fleetNumberLabel.Margin = new Thickness(0d, 14d, 0d, 6d);
        currentBusStack.Children.Add(_fleetNumberLabel);
        _fleetNumberBox.Height = 36d;
        _fleetNumberBox.MaxWidth = 220d;
        _fleetNumberBox.HorizontalAlignment = HorizontalAlignment.Left;
        _fleetNumberBox.Padding = new Thickness(9d, 6d, 9d, 6d);
        currentBusStack.Children.Add(_fleetNumberBox);

        _registerButton.Height = 40d;
        _registerButton.MinWidth = 210d;
        _registerButton.HorizontalAlignment = HorizontalAlignment.Left;
        _registerButton.Margin = new Thickness(0d, 12d, 0d, 0d);
        StylePrimaryButton(_registerButton);
        _registerButton.Click += RegisterButton_Click;
        currentBusStack.Children.Add(_registerButton);
        body.Children.Add(NewCard(currentBusStack, new Thickness(0d, 0d, 0d, 18d)));

        _fleetHeading.Foreground = Brushes.White;
        _fleetHeading.FontSize = 15d;
        _fleetHeading.FontWeight = FontWeights.Bold;
        _fleetHeading.Margin = new Thickness(2d, 0d, 0d, 10d);
        body.Children.Add(_fleetHeading);
        body.Children.Add(_fleetPanel);

        Grid.SetRow(scroller, 1);
        root.Children.Add(scroller);

        _saveButton.Height = 42d;
        _saveButton.MinWidth = 190d;
        _saveButton.HorizontalAlignment = HorizontalAlignment.Right;
        StylePrimaryButton(_saveButton);
        _saveButton.Click += SaveButton_Click;
        Grid.SetRow(_saveButton, 2);
        root.Children.Add(_saveButton);

        return root;
    }

    private static void AddField(Grid grid, TextBlock label, TextBox box, int column, Thickness margin)
    {
        var stack = new StackPanel { Margin = margin };
        label.Foreground = Brush(166, 183, 195);
        label.FontSize = 9.5d;
        label.FontWeight = FontWeights.Bold;
        label.Margin = new Thickness(0d, 0d, 0d, 6d);
        stack.Children.Add(label);
        box.Height = 36d;
        box.Padding = new Thickness(9d, 6d, 9d, 6d);
        stack.Children.Add(box);
        Grid.SetColumn(stack, column);
        grid.Children.Add(stack);
    }

    private void LoadCompany(VirtualCompanyData company)
    {
        _nameBox.Text = company.Name;
        _shortNameBox.Text = company.ShortName;
        _baseMapBox.Text = company.BaseMap ?? string.Empty;
        RenderFleet(company);
        RefreshCurrentBus();
    }

    private void RefreshCurrentBus()
    {
        var telemetry = _telemetryProvider();
        var bus = string.IsNullOrWhiteSpace(telemetry?.VehicleName) ? Text("NoBus") : telemetry.VehicleName;
        var map = string.IsNullOrWhiteSpace(telemetry?.MapName) ? "—" : telemetry.MapName;
        _currentBusText.Text = $"{Text("CurrentBus")}: {bus}\n{Text("CurrentMap")}: {map}";
        _registerButton.IsEnabled = telemetry?.IsInGame == true && !string.IsNullOrWhiteSpace(telemetry.VehicleName);
    }

    private void RenderFleet(VirtualCompanyData company)
    {
        _fleetPanel.Children.Clear();
        if (company.Vehicles.Count == 0)
        {
            _fleetPanel.Children.Add(new TextBlock
            {
                Text = Text("EmptyFleet"),
                Foreground = Brush(126, 145, 158),
                FontSize = 11d,
                Margin = new Thickness(3d, 4d, 0d, 12d)
            });
            return;
        }

        foreach (var vehicle in company.Vehicles.OrderBy(vehicle => vehicle.FleetNumber, StringComparer.OrdinalIgnoreCase))
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var details = new StackPanel();
            details.Children.Add(new TextBlock
            {
                Text = vehicle.FleetNumber,
                Foreground = Brushes.White,
                FontSize = 14d,
                FontWeight = FontWeights.Bold
            });
            details.Children.Add(new TextBlock
            {
                Text = vehicle.VehicleModel,
                Foreground = Brush(148, 168, 181),
                FontSize = 10.5d,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0d, 4d, 0d, 0d)
            });
            if (vehicle.LastUsedAt is not null)
            {
                details.Children.Add(new TextBlock
                {
                    Text = $"{Text("LastUsed")}: {vehicle.LastUsedAt.Value.ToLocalTime():g}",
                    Foreground = Brush(103, 123, 137),
                    FontSize = 9d,
                    Margin = new Thickness(0d, 4d, 0d, 0d)
                });
            }
            Grid.SetColumn(details, 0);
            grid.Children.Add(details);

            var remove = new Button
            {
                Content = Text("Remove"),
                Tag = vehicle.Id,
                MinWidth = 88d,
                Height = 34d,
                Padding = new Thickness(10d, 5d, 10d, 5d),
                Background = Brush(30, 18, 18),
                Foreground = Brush(239, 176, 176),
                BorderBrush = Brush(91, 43, 43),
                BorderThickness = new Thickness(1d),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            remove.Click += (_, _) => VirtualCompanyStore.RemoveVehicle(vehicle.Id);
            Grid.SetColumn(remove, 1);
            grid.Children.Add(remove);

            _fleetPanel.Children.Add(NewCard(grid, new Thickness(0d, 0d, 0d, 10d)));
        }
    }

    private void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        var telemetry = _telemetryProvider();
        if (telemetry?.IsInGame != true || string.IsNullOrWhiteSpace(telemetry.VehicleName))
        {
            RefreshCurrentBus();
            return;
        }

        SaveCompanyFields();
        VirtualCompanyStore.RegisterVehicle(_fleetNumberBox.Text, telemetry.VehicleName);
        _fleetNumberBox.Clear();
        RefreshCurrentBus();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCompanyFields();
        Close();
    }

    private void SaveCompanyFields()
    {
        var current = VirtualCompanyStore.Load();
        var updated = current with
        {
            Name = _nameBox.Text,
            ShortName = _shortNameBox.Text,
            BaseMap = _baseMapBox.Text
        };
        VirtualCompanyStore.Save(updated);

        if (!string.IsNullOrWhiteSpace(updated.Name))
        {
            var profile = DriverProfileStore.Load();
            if (!string.Equals(profile.CompanyName, updated.Name.Trim(), StringComparison.Ordinal))
            {
                DriverProfileStore.Save(profile with { CompanyName = updated.Name.Trim() });
            }
        }
    }

    private void ApplyLocalization()
    {
        Title = Text("Title");
        _title.Text = Text("Title");
        _subtitle.Text = Text("Subtitle");
        _nameLabel.Text = Text("CompanyName");
        _shortNameLabel.Text = Text("ShortName");
        _baseMapLabel.Text = Text("BaseMap");
        _fleetHeading.Text = Text("Fleet");
        _fleetNumberLabel.Text = Text("FleetNumber");
        _registerButton.Content = Text("RegisterCurrentBus");
        _saveButton.Content = Text("SaveCompany");
        RefreshCurrentBus();
        RenderFleet(VirtualCompanyStore.Load());
    }

    private static void StylePrimaryButton(Button button)
    {
        button.Padding = new Thickness(16d, 8d, 16d, 8d);
        button.Background = Brush(205, 88, 17);
        button.Foreground = Brushes.White;
        button.BorderBrush = Brush(255, 139, 48);
        button.BorderThickness = new Thickness(1d);
        button.FontWeight = FontWeights.SemiBold;
        button.Cursor = System.Windows.Input.Cursors.Hand;
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

    internal static string MenuText() => LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
    {
        "pt" => "▥  Empresa e frota",
        "es" => "▥  Empresa y flota",
        "de" => "▥  Unternehmen & Flotte",
        "fr" => "▥  Entreprise et flotte",
        _ => "▥  Company & fleet"
    };

    private static string Text(string key)
    {
        var table = LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
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
        ("Title", "Virtual company & fleet"), ("Subtitle", "Create your local company and register the buses you use in OMSI."),
        ("CompanyName", "COMPANY NAME"), ("ShortName", "SHORT NAME"), ("BaseMap", "BASE / MAIN MAP"),
        ("Fleet", "Fleet"), ("FleetNumber", "FLEET NUMBER (OPTIONAL)"), ("RegisterCurrentBus", "Register current bus"),
        ("SaveCompany", "Save company"), ("CurrentBus", "Current bus"), ("CurrentMap", "Current map"),
        ("NoBus", "No active OMSI bus"), ("EmptyFleet", "No buses registered yet."), ("Remove", "Remove"), ("LastUsed", "Last used"));
    private static readonly IReadOnlyDictionary<string, string> Pt = T(
        ("Title", "Empresa virtual e frota"), ("Subtitle", "Crie sua empresa local e registre os ônibus que você usa no OMSI."),
        ("CompanyName", "NOME DA EMPRESA"), ("ShortName", "SIGLA"), ("BaseMap", "BASE / MAPA PRINCIPAL"),
        ("Fleet", "Frota"), ("FleetNumber", "NÚMERO DE FROTA (OPCIONAL)"), ("RegisterCurrentBus", "Registrar ônibus atual"),
        ("SaveCompany", "Salvar empresa"), ("CurrentBus", "Ônibus atual"), ("CurrentMap", "Mapa atual"),
        ("NoBus", "Nenhum ônibus ativo no OMSI"), ("EmptyFleet", "Nenhum ônibus cadastrado ainda."), ("Remove", "Remover"), ("LastUsed", "Último uso"));
    private static readonly IReadOnlyDictionary<string, string> Es = T(
        ("Title", "Empresa virtual y flota"), ("Subtitle", "Crea tu empresa local y registra los autobuses que usas en OMSI."),
        ("CompanyName", "NOMBRE DE LA EMPRESA"), ("ShortName", "SIGLA"), ("BaseMap", "BASE / MAPA PRINCIPAL"),
        ("Fleet", "Flota"), ("FleetNumber", "NÚMERO DE FLOTA (OPCIONAL)"), ("RegisterCurrentBus", "Registrar autobús actual"),
        ("SaveCompany", "Guardar empresa"), ("CurrentBus", "Autobús actual"), ("CurrentMap", "Mapa actual"),
        ("NoBus", "No hay autobús activo en OMSI"), ("EmptyFleet", "Todavía no hay autobuses registrados."), ("Remove", "Eliminar"), ("LastUsed", "Último uso"));
    private static readonly IReadOnlyDictionary<string, string> De = T(
        ("Title", "Virtuelles Unternehmen & Flotte"), ("Subtitle", "Erstelle dein lokales Unternehmen und registriere die in OMSI genutzten Busse."),
        ("CompanyName", "UNTERNEHMENSNAME"), ("ShortName", "KÜRZEL"), ("BaseMap", "BASIS / HAUPTKARTE"),
        ("Fleet", "Flotte"), ("FleetNumber", "WAGENNUMMER (OPTIONAL)"), ("RegisterCurrentBus", "Aktuellen Bus registrieren"),
        ("SaveCompany", "Unternehmen speichern"), ("CurrentBus", "Aktueller Bus"), ("CurrentMap", "Aktuelle Karte"),
        ("NoBus", "Kein aktiver OMSI-Bus"), ("EmptyFleet", "Noch keine Busse registriert."), ("Remove", "Entfernen"), ("LastUsed", "Zuletzt genutzt"));
    private static readonly IReadOnlyDictionary<string, string> Fr = T(
        ("Title", "Entreprise virtuelle et flotte"), ("Subtitle", "Créez votre entreprise locale et enregistrez les bus utilisés dans OMSI."),
        ("CompanyName", "NOM DE L’ENTREPRISE"), ("ShortName", "SIGLE"), ("BaseMap", "BASE / CARTE PRINCIPALE"),
        ("Fleet", "Flotte"), ("FleetNumber", "NUMÉRO DE PARC (OPTIONNEL)"), ("RegisterCurrentBus", "Enregistrer le bus actuel"),
        ("SaveCompany", "Enregistrer l’entreprise"), ("CurrentBus", "Bus actuel"), ("CurrentMap", "Carte actuelle"),
        ("NoBus", "Aucun bus OMSI actif"), ("EmptyFleet", "Aucun bus enregistré pour le moment."), ("Remove", "Retirer"), ("LastUsed", "Dernière utilisation"));

    private static IReadOnlyDictionary<string, string> T(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
