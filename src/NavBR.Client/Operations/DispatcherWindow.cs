using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Driver;
using NavBR.Client.Localization;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Operations;

internal sealed class DispatcherWindow : Window
{
    private readonly Func<VehicleTelemetry?> _telemetryProvider;
    private readonly DispatcherTimer _refreshTimer;
    private readonly TextBlock _title = new();
    private readonly TextBlock _subtitle = new();
    private readonly TextBlock _operationState = new();
    private readonly TextBlock _driverValue = new();
    private readonly TextBlock _companyValue = new();
    private readonly TextBlock _vehicleValue = new();
    private readonly TextBlock _fleetValue = new();
    private readonly TextBlock _mapValue = new();
    private readonly TextBlock _lineValue = new();
    private readonly TextBlock _routeValue = new();
    private readonly TextBlock _destinationValue = new();
    private readonly TextBlock _nextStopValue = new();
    private readonly TextBlock _streetValue = new();
    private readonly TextBlock _speedValue = new();
    private readonly TextBlock _delayValue = new();
    private readonly TextBlock _doorsValue = new();
    private readonly TextBlock _stopRequestValue = new();
    private readonly Border _stateCard;

    public DispatcherWindow(Window owner, Func<VehicleTelemetry?> telemetryProvider)
    {
        _telemetryProvider = telemetryProvider;
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 980d;
        Height = 720d;
        MinWidth = 860d;
        MinHeight = 620d;
        Background = Brush(6, 11, 16);

        _stateCard = BuildStateCard();
        Content = BuildContent();
        ApplyLocalization();
        RefreshOperation();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500d) };
        _refreshTimer.Tick += (_, _) => RefreshOperation();
        _refreshTimer.Start();
        Closed += (_, _) => _refreshTimer.Stop();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(28d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        var heading = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 18d) };
        _title.Foreground = Brushes.White;
        _title.FontSize = 25d;
        _title.FontWeight = FontWeights.Bold;
        heading.Children.Add(_title);
        _subtitle.Foreground = Brush(142, 161, 174);
        _subtitle.FontSize = 11.5d;
        _subtitle.TextWrapping = TextWrapping.Wrap;
        _subtitle.Margin = new Thickness(0d, 6d, 0d, 0d);
        heading.Children.Add(_subtitle);
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        Grid.SetRow(_stateCard, 1);
        root.Children.Add(_stateCard);

        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0d, 18d, 0d, 0d)
        };
        var body = new StackPanel();
        scroller.Content = body;

        body.Children.Add(BuildSection(
            Text("Identity"),
            (Text("Driver"), _driverValue),
            (Text("Company"), _companyValue),
            (Text("Vehicle"), _vehicleValue),
            (Text("FleetNumber"), _fleetValue)));

        body.Children.Add(BuildSection(
            Text("Service"),
            (Text("Map"), _mapValue),
            (Text("Line"), _lineValue),
            (Text("Route"), _routeValue),
            (Text("Destination"), _destinationValue),
            (Text("NextStop"), _nextStopValue),
            (Text("Street"), _streetValue)));

        var liveWrap = new WrapPanel();
        liveWrap.Children.Add(BuildMetricCard(Text("Speed"), _speedValue, "km/h"));
        liveWrap.Children.Add(BuildMetricCard(Text("Delay"), _delayValue, string.Empty));
        liveWrap.Children.Add(BuildMetricCard(Text("Doors"), _doorsValue, string.Empty));
        liveWrap.Children.Add(BuildMetricCard(Text("StopRequest"), _stopRequestValue, string.Empty));
        body.Children.Add(liveWrap);

        var note = new Border
        {
            Margin = new Thickness(0d, 8d, 0d, 0d),
            Padding = new Thickness(15d),
            Background = Brush(10, 19, 25),
            BorderBrush = Brush(31, 47, 57),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(11d),
            Child = new TextBlock
            {
                Text = Text("LocalOnly"),
                Foreground = Brush(141, 160, 173),
                FontSize = 10.5d,
                TextWrapping = TextWrapping.Wrap
            }
        };
        body.Children.Add(note);

        Grid.SetRow(scroller, 2);
        root.Children.Add(scroller);
        return root;
    }

    private Border BuildStateCard()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var lamp = new Border
        {
            Width = 13d,
            Height = 13d,
            CornerRadius = new CornerRadius(999d),
            Background = Brush(99, 116, 128),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 12d, 0d),
            Tag = "dispatcher-lamp"
        };
        Grid.SetColumn(lamp, 0);
        grid.Children.Add(lamp);

        _operationState.Foreground = Brushes.White;
        _operationState.FontSize = 15d;
        _operationState.FontWeight = FontWeights.SemiBold;
        Grid.SetColumn(_operationState, 1);
        grid.Children.Add(_operationState);

        return new Border
        {
            Padding = new Thickness(16d),
            Background = Brush(11, 20, 26),
            BorderBrush = Brush(31, 47, 57),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Child = grid
        };
    }

    private static Border BuildSection(string heading, params (string Label, TextBlock Value)[] rows)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = heading,
            Foreground = Brushes.White,
            FontSize = 14d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 0d, 0d, 10d)
        });

        foreach (var row in rows)
        {
            row.Value.Foreground = Brush(219, 230, 237);
            row.Value.FontSize = 11d;
            row.Value.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(BuildValueLine(row.Label, row.Value));
        }

        return new Border
        {
            Margin = new Thickness(0d, 0d, 0d, 12d),
            Padding = new Thickness(16d),
            Background = Brush(10, 19, 25),
            BorderBrush = Brush(31, 47, 57),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Child = stack
        };
    }

    private static FrameworkElement BuildValueLine(string label, TextBlock value)
    {
        var grid = new Grid { Margin = new Thickness(0d, 4d, 0d, 4d) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = Brush(126, 146, 160),
            FontSize = 10d
        };
        Grid.SetColumn(labelBlock, 0);
        Grid.SetColumn(value, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(value);
        return grid;
    }

    private static Border BuildMetricCard(string label, TextBlock value, string suffix)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brush(131, 151, 165),
            FontSize = 9d,
            FontWeight = FontWeights.Bold
        });
        value.Foreground = Brushes.White;
        value.FontSize = 21d;
        value.FontWeight = FontWeights.Bold;
        value.Margin = new Thickness(0d, 5d, 0d, 0d);
        stack.Children.Add(value);
        if (!string.IsNullOrWhiteSpace(suffix))
        {
            stack.Children.Add(new TextBlock
            {
                Text = suffix,
                Foreground = Brush(104, 125, 140),
                FontSize = 8.5d,
                Margin = new Thickness(0d, 1d, 0d, 0d)
            });
        }

        return new Border
        {
            Width = 155d,
            MinHeight = 92d,
            Margin = new Thickness(0d, 0d, 10d, 10d),
            Padding = new Thickness(14d),
            Background = Brush(10, 19, 25),
            BorderBrush = Brush(31, 47, 57),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(11d),
            Child = stack
        };
    }

    private void RefreshOperation()
    {
        var telemetry = _telemetryProvider();
        var profile = DriverProfileStore.Load();
        var company = VirtualCompanyStore.Load();

        _driverValue.Text = Safe(profile.DisplayName);
        _companyValue.Text = Safe(company.Name.Length > 0 ? company.Name : profile.CompanyName);
        _vehicleValue.Text = Safe(telemetry?.VehicleName);
        _mapValue.Text = Safe(telemetry?.MapName);
        _lineValue.Text = Safe(telemetry?.Line);
        _routeValue.Text = Safe(telemetry?.Route);
        _destinationValue.Text = Safe(telemetry?.DestinationName);
        _nextStopValue.Text = Safe(telemetry?.NextStopName);
        _streetValue.Text = Safe(telemetry?.CurrentStreetName);
        _speedValue.Text = telemetry?.IsInGame == true ? telemetry.SpeedKph.ToString("0.0") : "—";
        _delayValue.Text = FormatDelay(telemetry?.DelaySeconds);
        _doorsValue.Text = FormatDoors(telemetry?.Doors ?? VehicleDoorFlags.None);
        _stopRequestValue.Text = telemetry?.IsInGame == true
            ? telemetry.StopRequested ? Text("Requested") : Text("No")
            : "—";

        var fleetMatch = telemetry?.VehicleName is { Length: > 0 } vehicleName
            ? company.Vehicles
                .OrderByDescending(vehicle => vehicle.LastUsedAt)
                .FirstOrDefault(vehicle => string.Equals(
                    vehicle.VehicleModel,
                    vehicleName,
                    StringComparison.OrdinalIgnoreCase))
            : null;
        _fleetValue.Text = fleetMatch?.FleetNumber ?? "—";

        var inGame = telemetry?.IsInGame == true;
        _operationState.Text = inGame ? Text("Operating") : Text("WaitingOmsi");
        if (_stateCard.Child is Grid grid &&
            grid.Children.OfType<Border>().FirstOrDefault(item => item.Tag as string == "dispatcher-lamp") is { } lamp)
        {
            lamp.Background = inGame ? Brush(78, 206, 132) : Brush(99, 116, 128);
        }
    }

    private void ApplyLocalization()
    {
        Title = Text("Title");
        _title.Text = Text("Title");
        _subtitle.Text = Text("Subtitle");
    }

    private static string FormatDelay(int? seconds)
    {
        if (seconds is null)
        {
            return "—";
        }
        if (Math.Abs(seconds.Value) < 30)
        {
            return Text("OnTime");
        }

        var minutes = (int)Math.Round(Math.Abs(seconds.Value) / 60d);
        return seconds.Value > 0
            ? $"+{minutes} min"
            : $"-{minutes} min";
    }

    private static string FormatDoors(VehicleDoorFlags doors) => doors == VehicleDoorFlags.None
        ? Text("Closed")
        : Text("Open");

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    internal static string MenuText() => LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
    {
        "pt" => "⌁  CCO / Operação",
        "es" => "⌁  CCO / Operación",
        "de" => "⌁  Leitstelle / Betrieb",
        "fr" => "⌁  PCC / Exploitation",
        _ => "⌁  Control center"
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
        ("Title", "Operations control center"), ("Subtitle", "Local operational view of the active OMSI service. Multiplayer dispatching will be added in the next stage."),
        ("Identity", "Driver and vehicle"), ("Driver", "Driver"), ("Company", "Company"), ("Vehicle", "Vehicle"), ("FleetNumber", "Fleet number"),
        ("Service", "Current service"), ("Map", "Map"), ("Line", "Line"), ("Route", "Route"), ("Destination", "Destination"), ("NextStop", "Next stop"), ("Street", "Street"),
        ("Speed", "Speed"), ("Delay", "Schedule"), ("Doors", "Doors"), ("StopRequest", "Stop request"),
        ("Operating", "OMSI operation active"), ("WaitingOmsi", "Waiting for an active OMSI service"), ("OnTime", "On time"),
        ("Closed", "Closed"), ("Open", "Open"), ("Requested", "Requested"), ("No", "No"),
        ("LocalOnly", "Alpha.12 starts with a safe local, read-only control center. Remote drivers, assignments and dispatcher commands will be added on top of the multiplayer session later."));
    private static readonly IReadOnlyDictionary<string, string> Pt = T(
        ("Title", "CCO / Centro de Operações"), ("Subtitle", "Visão operacional local do serviço ativo no OMSI. O despacho multiplayer será acrescentado na próxima etapa."),
        ("Identity", "Motorista e veículo"), ("Driver", "Motorista"), ("Company", "Empresa"), ("Vehicle", "Veículo"), ("FleetNumber", "Número de frota"),
        ("Service", "Serviço atual"), ("Map", "Mapa"), ("Line", "Linha"), ("Route", "Rota"), ("Destination", "Destino"), ("NextStop", "Próxima parada"), ("Street", "Rua"),
        ("Speed", "Velocidade"), ("Delay", "Horário"), ("Doors", "Portas"), ("StopRequest", "Parada solicitada"),
        ("Operating", "Operação OMSI ativa"), ("WaitingOmsi", "Aguardando um serviço ativo no OMSI"), ("OnTime", "No horário"),
        ("Closed", "Fechadas"), ("Open", "Abertas"), ("Requested", "Solicitada"), ("No", "Não"),
        ("LocalOnly", "A Alpha.12 começa com um CCO local, seguro e somente leitura. Motoristas remotos, escalas e comandos do despachante serão adicionados depois sobre a sessão multiplayer."));
    private static readonly IReadOnlyDictionary<string, string> Es = T(
        ("Title", "CCO / Centro de Operaciones"), ("Subtitle", "Vista operativa local del servicio activo en OMSI. El despacho multijugador se añadirá en la siguiente etapa."),
        ("Identity", "Conductor y vehículo"), ("Driver", "Conductor"), ("Company", "Empresa"), ("Vehicle", "Vehículo"), ("FleetNumber", "Número de flota"),
        ("Service", "Servicio actual"), ("Map", "Mapa"), ("Line", "Línea"), ("Route", "Ruta"), ("Destination", "Destino"), ("NextStop", "Próxima parada"), ("Street", "Calle"),
        ("Speed", "Velocidad"), ("Delay", "Horario"), ("Doors", "Puertas"), ("StopRequest", "Parada solicitada"),
        ("Operating", "Operación OMSI activa"), ("WaitingOmsi", "Esperando un servicio activo de OMSI"), ("OnTime", "En horario"),
        ("Closed", "Cerradas"), ("Open", "Abiertas"), ("Requested", "Solicitada"), ("No", "No"),
        ("LocalOnly", "Alpha.12 comienza con un CCO local, seguro y de solo lectura. Conductores remotos, asignaciones y comandos del despachador se añadirán después sobre la sesión multijugador."));
    private static readonly IReadOnlyDictionary<string, string> De = T(
        ("Title", "Leitstelle / Betrieb"), ("Subtitle", "Lokale Betriebsansicht des aktiven OMSI-Dienstes. Mehrspieler-Dispositionsfunktionen folgen in der nächsten Stufe."),
        ("Identity", "Fahrer und Fahrzeug"), ("Driver", "Fahrer"), ("Company", "Unternehmen"), ("Vehicle", "Fahrzeug"), ("FleetNumber", "Wagennummer"),
        ("Service", "Aktueller Dienst"), ("Map", "Karte"), ("Line", "Linie"), ("Route", "Route"), ("Destination", "Ziel"), ("NextStop", "Nächste Haltestelle"), ("Street", "Straße"),
        ("Speed", "Geschwindigkeit"), ("Delay", "Fahrplan"), ("Doors", "Türen"), ("StopRequest", "Haltewunsch"),
        ("Operating", "OMSI-Betrieb aktiv"), ("WaitingOmsi", "Warte auf einen aktiven OMSI-Dienst"), ("OnTime", "Pünktlich"),
        ("Closed", "Geschlossen"), ("Open", "Offen"), ("Requested", "Aktiv"), ("No", "Nein"),
        ("LocalOnly", "Alpha.12 startet mit einer sicheren lokalen Leitstelle im Nur-Lese-Modus. Remote-Fahrer, Umläufe und Disponentenbefehle werden später auf der Mehrspieler-Sitzung aufgebaut."));
    private static readonly IReadOnlyDictionary<string, string> Fr = T(
        ("Title", "PCC / Centre d’exploitation"), ("Subtitle", "Vue opérationnelle locale du service OMSI actif. La régulation multijoueur sera ajoutée à l’étape suivante."),
        ("Identity", "Conducteur et véhicule"), ("Driver", "Conducteur"), ("Company", "Entreprise"), ("Vehicle", "Véhicule"), ("FleetNumber", "Numéro de parc"),
        ("Service", "Service actuel"), ("Map", "Carte"), ("Line", "Ligne"), ("Route", "Itinéraire"), ("Destination", "Destination"), ("NextStop", "Prochain arrêt"), ("Street", "Rue"),
        ("Speed", "Vitesse"), ("Delay", "Horaire"), ("Doors", "Portes"), ("StopRequest", "Arrêt demandé"),
        ("Operating", "Exploitation OMSI active"), ("WaitingOmsi", "En attente d’un service OMSI actif"), ("OnTime", "À l’heure"),
        ("Closed", "Fermées"), ("Open", "Ouvertes"), ("Requested", "Demandé"), ("No", "Non"),
        ("LocalOnly", "Alpha.12 commence par un PCC local, sûr et en lecture seule. Les conducteurs distants, affectations et commandes de régulation seront ajoutés ensuite au-dessus de la session multijoueur."));

    private static IReadOnlyDictionary<string, string> T(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
