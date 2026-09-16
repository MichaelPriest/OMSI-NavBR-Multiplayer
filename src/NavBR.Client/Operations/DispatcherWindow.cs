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
        Width = 1180d;
        Height = 780d;
        MinWidth = 960d;
        MinHeight = 660d;
        Background = Brush(4, 10, 16);

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
        var root = new Grid { Margin = new Thickness(22d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        var headingGrid = new Grid { Margin = new Thickness(2d, 0d, 2d, 16d) };
        headingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        headingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var heading = new StackPanel();
        _title.Foreground = Brushes.White;
        _title.FontSize = 25d;
        _title.FontWeight = FontWeights.Bold;
        heading.Children.Add(_title);
        _subtitle.Foreground = Brush(128, 151, 168);
        _subtitle.FontSize = 11.5d;
        _subtitle.TextWrapping = TextWrapping.Wrap;
        _subtitle.Margin = new Thickness(0d, 5d, 18d, 0d);
        heading.Children.Add(_subtitle);
        Grid.SetColumn(heading, 0);
        headingGrid.Children.Add(heading);

        var badge = new Border
        {
            Padding = new Thickness(12d, 7d, 12d, 7d),
            Background = Brush(7, 34, 51),
            BorderBrush = Brush(24, 85, 117),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(999d),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "CCO • LIVE",
                Foreground = Brush(84, 190, 255),
                FontSize = 10d,
                FontWeight = FontWeights.Bold
            }
        };
        Grid.SetColumn(badge, 1);
        headingGrid.Children.Add(badge);
        Grid.SetRow(headingGrid, 0);
        root.Children.Add(headingGrid);

        Grid.SetRow(_stateCard, 1);
        root.Children.Add(_stateCard);

        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0d, 14d, 0d, 0d),
            CanContentScroll = false
        };
        var body = new StackPanel();
        scroller.Content = body;

        var overview = new Grid { Margin = new Thickness(0d, 0d, 0d, 14d) };
        overview.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.65d, GridUnitType.Star) });
        overview.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14d) });
        overview.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.95d, GridUnitType.Star) });

        var mapCard = BuildMapCard();
        Grid.SetColumn(mapCard, 0);
        overview.Children.Add(mapCard);

        var serviceCard = BuildServiceCard();
        Grid.SetColumn(serviceCard, 2);
        overview.Children.Add(serviceCard);
        body.Children.Add(overview);

        var liveWrap = new WrapPanel { Margin = new Thickness(0d, 0d, 0d, 4d) };
        liveWrap.Children.Add(BuildMetricCard(Text("Speed"), _speedValue, "km/h"));
        liveWrap.Children.Add(BuildMetricCard(Text("Delay"), _delayValue, string.Empty));
        liveWrap.Children.Add(BuildMetricCard(Text("Doors"), _doorsValue, string.Empty));
        liveWrap.Children.Add(BuildMetricCard(Text("StopRequest"), _stopRequestValue, string.Empty));
        body.Children.Add(liveWrap);

        body.Children.Add(BuildSection(
            Text("Identity"),
            (Text("Driver"), _driverValue),
            (Text("Company"), _companyValue),
            (Text("Vehicle"), _vehicleValue),
            (Text("FleetNumber"), _fleetValue)));

        var note = new Border
        {
            Margin = new Thickness(0d, 4d, 0d, 0d),
            Padding = new Thickness(14d),
            Background = Brush(7, 17, 24),
            BorderBrush = Brush(24, 46, 60),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(11d),
            Child = new TextBlock
            {
                Text = Text("LocalOnly"),
                Foreground = Brush(118, 145, 162),
                FontSize = 10.5d,
                TextWrapping = TextWrapping.Wrap
            }
        };
        body.Children.Add(note);

        Grid.SetRow(scroller, 2);
        root.Children.Add(scroller);
        return root;
    }

    private Border BuildMapCard()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(new TextBlock
        {
            Text = Text("Map").ToUpperInvariant(),
            Foreground = Brush(103, 155, 188),
            FontSize = 9d,
            FontWeight = FontWeights.Bold
        });
        _mapValue.Foreground = Brushes.White;
        _mapValue.FontSize = 12d;
        _mapValue.FontWeight = FontWeights.SemiBold;
        _mapValue.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(_mapValue, 1);
        top.Children.Add(_mapValue);
        Grid.SetRow(top, 0);
        root.Children.Add(top);

        var canvas = new Grid
        {
            MinHeight = 260d,
            Margin = new Thickness(0d, 12d, 0d, 12d),
            Background = Brush(5, 18, 29)
        };
        for (var i = 0; i < 6; i++)
        {
            canvas.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
            canvas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        }

        for (var i = 1; i < 6; i++)
        {
            var h = new Border { Height = 1d, Background = Brush(13, 37, 51), Opacity = 0.75 };
            Grid.SetRow(h, i);
            Grid.SetColumnSpan(h, 6);
            canvas.Children.Add(h);
            var v = new Border { Width = 1d, Background = Brush(13, 37, 51), Opacity = 0.75 };
            Grid.SetColumn(v, i);
            Grid.SetRowSpan(v, 6);
            canvas.Children.Add(v);
        }

        var routeVertical = new Border
        {
            Width = 8d,
            Background = Brush(22, 126, 211),
            CornerRadius = new CornerRadius(8d),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0d, 18d, 0d, 18d)
        };
        Grid.SetColumn(routeVertical, 2);
        Grid.SetRow(routeVertical, 1);
        Grid.SetRowSpan(routeVertical, 4);
        canvas.Children.Add(routeVertical);

        var routeHorizontal = new Border
        {
            Height = 8d,
            Background = Brush(22, 126, 211),
            CornerRadius = new CornerRadius(8d),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(18d, 0d, 18d, 0d)
        };
        Grid.SetColumn(routeHorizontal, 2);
        Grid.SetColumnSpan(routeHorizontal, 3);
        Grid.SetRow(routeHorizontal, 4);
        canvas.Children.Add(routeHorizontal);

        var bus = new Border
        {
            Width = 30d,
            Height = 42d,
            Background = Brush(58, 178, 255),
            BorderBrush = Brush(157, 222, 255),
            BorderThickness = new Thickness(2d),
            CornerRadius = new CornerRadius(8d),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "BUS",
                Foreground = Brushes.White,
                FontSize = 7d,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(bus, 4);
        Grid.SetRow(bus, 4);
        canvas.Children.Add(bus);

        var mapHint = new Border
        {
            Padding = new Thickness(10d, 7d, 10d, 7d),
            Background = Brush(7, 25, 37),
            CornerRadius = new CornerRadius(8d),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(10d),
            Child = new TextBlock
            {
                Text = Text("MapSchematic"),
                Foreground = Brush(126, 159, 178),
                FontSize = 9d
            }
        };
        Grid.SetColumnSpan(mapHint, 6);
        Grid.SetRowSpan(mapHint, 6);
        canvas.Children.Add(mapHint);

        Grid.SetRow(canvas, 1);
        root.Children.Add(canvas);

        var bottom = new Grid();
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        var currentStreet = BuildCompactValue(Text("Street"), _streetValue);
        var nextStop = BuildCompactValue(Text("NextStop"), _nextStopValue);
        Grid.SetColumn(currentStreet, 0);
        Grid.SetColumn(nextStop, 1);
        bottom.Children.Add(currentStreet);
        bottom.Children.Add(nextStop);
        Grid.SetRow(bottom, 2);
        root.Children.Add(bottom);

        return new Border
        {
            Padding = new Thickness(16d),
            Background = Brush(6, 16, 24),
            BorderBrush = Brush(25, 55, 72),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(13d),
            Child = root
        };
    }

    private Border BuildServiceCard()
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = Text("Service").ToUpperInvariant(),
            Foreground = Brush(103, 155, 188),
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 0d, 0d, 12d)
        });
        AddServiceField(stack, Text("Line"), _lineValue, true);
        AddServiceField(stack, Text("Route"), _routeValue, false);
        AddServiceField(stack, Text("Destination"), _destinationValue, false);

        return new Border
        {
            Padding = new Thickness(17d),
            Background = Brush(8, 19, 27),
            BorderBrush = Brush(26, 49, 63),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(13d),
            Child = stack
        };
    }

    private static void AddServiceField(Panel panel, string label, TextBlock value, bool emphasize)
    {
        panel.Children.Add(new TextBlock
        {
            Text = label.ToUpperInvariant(),
            Foreground = Brush(101, 128, 145),
            FontSize = 8.5d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 0d, 0d, 3d)
        });
        value.Foreground = emphasize ? Brush(95, 205, 255) : Brushes.White;
        value.FontSize = emphasize ? 27d : 14d;
        value.FontWeight = emphasize ? FontWeights.Bold : FontWeights.SemiBold;
        value.TextWrapping = TextWrapping.Wrap;
        value.Margin = new Thickness(0d, 0d, 0d, 16d);
        panel.Children.Add(value);
    }

    private static FrameworkElement BuildCompactValue(string label, TextBlock value)
    {
        var stack = new StackPanel { Margin = new Thickness(0d, 0d, 14d, 0d) };
        stack.Children.Add(new TextBlock
        {
            Text = label.ToUpperInvariant(),
            Foreground = Brush(98, 128, 146),
            FontSize = 8d,
            FontWeight = FontWeights.Bold
        });
        value.Foreground = Brushes.White;
        value.FontSize = 11d;
        value.FontWeight = FontWeights.SemiBold;
        value.TextWrapping = TextWrapping.Wrap;
        value.Margin = new Thickness(0d, 3d, 0d, 0d);
        stack.Children.Add(value);
        return stack;
    }

    private Border BuildStateCard()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var lamp = new Border
        {
            Width = 12d,
            Height = 12d,
            CornerRadius = new CornerRadius(999d),
            Background = Brush(99, 116, 128),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 11d, 0d),
            Tag = "dispatcher-lamp"
        };
        Grid.SetColumn(lamp, 0);
        grid.Children.Add(lamp);

        _operationState.Foreground = Brushes.White;
        _operationState.FontSize = 13d;
        _operationState.FontWeight = FontWeights.SemiBold;
        Grid.SetColumn(_operationState, 1);
        grid.Children.Add(_operationState);

        var legend = new TextBlock
        {
            Text = Text("LiveTelemetry"),
            Foreground = Brush(93, 155, 189),
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(legend, 2);
        grid.Children.Add(legend);

        return new Border
        {
            Padding = new Thickness(14d, 11d, 14d, 11d),
            Background = Brush(7, 18, 25),
            BorderBrush = Brush(24, 49, 64),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(10d),
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
            FontSize = 13d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 0d, 0d, 8d)
        });

        foreach (var row in rows)
        {
            row.Value.Foreground = Brush(219, 230, 237);
            row.Value.FontSize = 10.5d;
            row.Value.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(BuildValueLine(row.Label, row.Value));
        }

        return new Border
        {
            Margin = new Thickness(0d, 0d, 0d, 12d),
            Padding = new Thickness(14d),
            Background = Brush(8, 18, 25),
            BorderBrush = Brush(26, 47, 60),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(11d),
            Child = stack
        };
    }

    private static FrameworkElement BuildValueLine(string label, TextBlock value)
    {
        var grid = new Grid { Margin = new Thickness(0d, 3d, 0d, 3d) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = Brush(111, 139, 157),
            FontSize = 9.5d
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
            Text = label.ToUpperInvariant(),
            Foreground = Brush(98, 132, 153),
            FontSize = 8.5d,
            FontWeight = FontWeights.Bold
        });
        value.Foreground = Brushes.White;
        value.FontSize = 21d;
        value.FontWeight = FontWeights.Bold;
        value.Margin = new Thickness(0d, 4d, 0d, 0d);
        stack.Children.Add(value);
        if (!string.IsNullOrWhiteSpace(suffix))
        {
            stack.Children.Add(new TextBlock
            {
                Text = suffix,
                Foreground = Brush(96, 122, 140),
                FontSize = 8d,
                Margin = new Thickness(0d, 1d, 0d, 0d)
            });
        }

        return new Border
        {
            Width = 170d,
            MinHeight = 88d,
            Margin = new Thickness(0d, 0d, 10d, 10d),
            Padding = new Thickness(13d),
            Background = Brush(7, 18, 25),
            BorderBrush = Brush(24, 48, 62),
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
        ("Title", "Operations control center"), ("Subtitle", "Live operational view of the active OMSI service and multiplayer fleet."),
        ("Identity", "Driver and vehicle"), ("Driver", "Driver"), ("Company", "Company"), ("Vehicle", "Vehicle"), ("FleetNumber", "Fleet number"),
        ("Service", "Current service"), ("Map", "Map"), ("Line", "Line"), ("Route", "Route"), ("Destination", "Destination"), ("NextStop", "Next stop"), ("Street", "Street"),
        ("Speed", "Speed"), ("Delay", "Schedule"), ("Doors", "Doors"), ("StopRequest", "Stop request"),
        ("Operating", "OMSI operation active"), ("WaitingOmsi", "Waiting for an active OMSI service"), ("OnTime", "On time"),
        ("Closed", "Closed"), ("Open", "Open"), ("Requested", "Requested"), ("No", "No"),
        ("MapSchematic", "Operational schematic • real map layer will use route/position data when available"),
        ("LiveTelemetry", "LIVE TELEMETRY"),
        ("LocalOnly", "CCO keeps local operation available at all times. Remote drivers appear below when a multiplayer room is connected."));
    private static readonly IReadOnlyDictionary<string, string> Pt = T(
        ("Title", "CCO / Centro de Operações"), ("Subtitle", "Visão operacional ao vivo do serviço ativo no OMSI e da frota multiplayer."),
        ("Identity", "Motorista e veículo"), ("Driver", "Motorista"), ("Company", "Empresa"), ("Vehicle", "Veículo"), ("FleetNumber", "Número de frota"),
        ("Service", "Serviço atual"), ("Map", "Mapa"), ("Line", "Linha"), ("Route", "Rota"), ("Destination", "Destino"), ("NextStop", "Próxima parada"), ("Street", "Rua"),
        ("Speed", "Velocidade"), ("Delay", "Horário"), ("Doors", "Portas"), ("StopRequest", "Parada solicitada"),
        ("Operating", "Operação OMSI ativa"), ("WaitingOmsi", "Aguardando um serviço ativo no OMSI"), ("OnTime", "No horário"),
        ("Closed", "Fechadas"), ("Open", "Abertas"), ("Requested", "Solicitada"), ("No", "Não"),
        ("MapSchematic", "Esquema operacional • o mapa real usará rota/posição quando esses dados estiverem disponíveis"),
        ("LiveTelemetry", "TELEMETRIA AO VIVO"),
        ("LocalOnly", "O CCO mantém a operação local sempre disponível. Motoristas remotos aparecem abaixo quando houver uma sala multiplayer conectada."));
    private static readonly IReadOnlyDictionary<string, string> Es = T(
        ("Title", "CCO / Centro de Operaciones"), ("Subtitle", "Vista operativa en vivo del servicio activo en OMSI y de la flota multijugador."),
        ("Identity", "Conductor y vehículo"), ("Driver", "Conductor"), ("Company", "Empresa"), ("Vehicle", "Vehículo"), ("FleetNumber", "Número de flota"),
        ("Service", "Servicio actual"), ("Map", "Mapa"), ("Line", "Línea"), ("Route", "Ruta"), ("Destination", "Destino"), ("NextStop", "Próxima parada"), ("Street", "Calle"),
        ("Speed", "Velocidad"), ("Delay", "Horario"), ("Doors", "Puertas"), ("StopRequest", "Parada solicitada"),
        ("Operating", "Operación OMSI activa"), ("WaitingOmsi", "Esperando un servicio activo de OMSI"), ("OnTime", "En horario"),
        ("Closed", "Cerradas"), ("Open", "Abiertas"), ("Requested", "Solicitada"), ("No", "No"),
        ("MapSchematic", "Esquema operativo • el mapa real usará ruta/posición cuando estén disponibles"),
        ("LiveTelemetry", "TELEMETRÍA EN VIVO"),
        ("LocalOnly", "El CCO mantiene siempre disponible la operación local. Los conductores remotos aparecen abajo cuando hay una sala conectada."));
    private static readonly IReadOnlyDictionary<string, string> De = T(
        ("Title", "Leitstelle / Betrieb"), ("Subtitle", "Live-Betriebsansicht des aktiven OMSI-Dienstes und der Mehrspieler-Flotte."),
        ("Identity", "Fahrer und Fahrzeug"), ("Driver", "Fahrer"), ("Company", "Unternehmen"), ("Vehicle", "Fahrzeug"), ("FleetNumber", "Wagennummer"),
        ("Service", "Aktueller Dienst"), ("Map", "Karte"), ("Line", "Linie"), ("Route", "Route"), ("Destination", "Ziel"), ("NextStop", "Nächste Haltestelle"), ("Street", "Straße"),
        ("Speed", "Geschwindigkeit"), ("Delay", "Fahrplan"), ("Doors", "Türen"), ("StopRequest", "Haltewunsch"),
        ("Operating", "OMSI-Betrieb aktiv"), ("WaitingOmsi", "Warte auf einen aktiven OMSI-Dienst"), ("OnTime", "Pünktlich"),
        ("Closed", "Geschlossen"), ("Open", "Offen"), ("Requested", "Aktiv"), ("No", "Nein"),
        ("MapSchematic", "Betriebsschema • echte Kartendaten werden mit Route/Position verwendet, sobald verfügbar"),
        ("LiveTelemetry", "LIVE-TELEMETRIE"),
        ("LocalOnly", "Die Leitstelle hält den lokalen Betrieb immer verfügbar. Remote-Fahrer erscheinen unten, sobald ein Mehrspieler-Raum verbunden ist."));
    private static readonly IReadOnlyDictionary<string, string> Fr = T(
        ("Title", "PCC / Centre d’exploitation"), ("Subtitle", "Vue opérationnelle en direct du service OMSI actif et de la flotte multijoueur."),
        ("Identity", "Conducteur et véhicule"), ("Driver", "Conducteur"), ("Company", "Entreprise"), ("Vehicle", "Véhicule"), ("FleetNumber", "Numéro de parc"),
        ("Service", "Service actuel"), ("Map", "Carte"), ("Line", "Ligne"), ("Route", "Itinéraire"), ("Destination", "Destination"), ("NextStop", "Prochain arrêt"), ("Street", "Rue"),
        ("Speed", "Vitesse"), ("Delay", "Horaire"), ("Doors", "Portes"), ("StopRequest", "Arrêt demandé"),
        ("Operating", "Exploitation OMSI active"), ("WaitingOmsi", "En attente d’un service OMSI actif"), ("OnTime", "À l’heure"),
        ("Closed", "Fermées"), ("Open", "Ouvertes"), ("Requested", "Demandé"), ("No", "Non"),
        ("MapSchematic", "Schéma opérationnel • la carte réelle utilisera route/position dès que ces données seront disponibles"),
        ("LiveTelemetry", "TÉLÉMÉTRIE EN DIRECT"),
        ("LocalOnly", "Le PCC garde l’exploitation locale toujours disponible. Les conducteurs distants apparaissent ci-dessous lorsqu’une salle est connectée."));

    private static IReadOnlyDictionary<string, string> T(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}