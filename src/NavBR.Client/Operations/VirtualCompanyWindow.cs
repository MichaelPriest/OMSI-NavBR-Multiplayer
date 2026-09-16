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
    private readonly TextBlock _companyMonogram = new();
    private readonly TextBlock _fleetCountText = new();
    private readonly TextBlock _baseMapPreview = new();
    private readonly Button _saveButton = new();
    private readonly Button _registerButton = new();
    private Action<VirtualCompanyData>? _companyChanged;

    public VirtualCompanyWindow(Window owner, Func<VehicleTelemetry?> telemetryProvider)
    {
        _telemetryProvider = telemetryProvider;
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 1040d;
        Height = 760d;
        MinWidth = 900d;
        MinHeight = 650d;
        Background = Brush(4, 10, 16);
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
        var root = new Grid { Margin = new Thickness(24d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headerGrid = new Grid { Margin = new Thickness(0d, 0d, 0d, 18d) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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
        headerGrid.Children.Add(heading);

        var companyBadge = new Border
        {
            Padding = new Thickness(12d, 7d, 12d, 7d),
            Background = Brush(7, 34, 51),
            BorderBrush = Brush(24, 85, 117),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(999d),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "EMPRESA • ALPHA.12",
                Foreground = Brush(84, 190, 255),
                FontSize = 10d,
                FontWeight = FontWeights.Bold
            }
        };
        Grid.SetColumn(companyBadge, 1);
        headerGrid.Children.Add(companyBadge);
        Grid.SetRow(headerGrid, 0);
        root.Children.Add(headerGrid);

        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false
        };
        var body = new StackPanel();
        scroller.Content = body;

        body.Children.Add(BuildCompanyHero());

        var tabs = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(2d, 0d, 0d, 14d)
        };
        tabs.Children.Add(BuildTab(Text("Overview"), true));
        tabs.Children.Add(BuildTab(Text("Fleet"), false));
        tabs.Children.Add(BuildTab(Text("Operation"), false));
        body.Children.Add(tabs);

        var identityAndBus = new Grid { Margin = new Thickness(0d, 0d, 0d, 16d) };
        identityAndBus.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3d, GridUnitType.Star) });
        identityAndBus.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14d) });
        identityAndBus.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8d, GridUnitType.Star) });

        var identityGrid = new Grid();
        identityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2d, GridUnitType.Star) });
        identityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        identityGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        identityGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddField(identityGrid, _nameLabel, _nameBox, 0, 0, new Thickness(0d, 0d, 8d, 12d));
        AddField(identityGrid, _shortNameLabel, _shortNameBox, 1, 0, new Thickness(8d, 0d, 0d, 12d));
        AddField(identityGrid, _baseMapLabel, _baseMapBox, 0, 1, new Thickness(0d));
        Grid.SetColumnSpan(identityGrid.Children[^1], 2);
        var identityCard = NewCard(identityGrid, new Thickness(0d));
        Grid.SetColumn(identityCard, 0);
        identityAndBus.Children.Add(identityCard);

        var currentBusStack = new StackPanel();
        currentBusStack.Children.Add(new TextBlock
        {
            Text = Text("CurrentOperation").ToUpperInvariant(),
            Foreground = Brush(95, 151, 184),
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 0d, 0d, 10d)
        });
        _currentBusText.Foreground = Brush(222, 234, 241);
        _currentBusText.FontSize = 12d;
        _currentBusText.FontWeight = FontWeights.SemiBold;
        _currentBusText.TextWrapping = TextWrapping.Wrap;
        currentBusStack.Children.Add(_currentBusText);

        _fleetNumberLabel.Foreground = Brush(121, 151, 169);
        _fleetNumberLabel.FontSize = 9d;
        _fleetNumberLabel.FontWeight = FontWeights.Bold;
        _fleetNumberLabel.Margin = new Thickness(0d, 14d, 0d, 6d);
        currentBusStack.Children.Add(_fleetNumberLabel);
        _fleetNumberBox.Height = 36d;
        _fleetNumberBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        _fleetNumberBox.Padding = new Thickness(9d, 6d, 9d, 6d);
        currentBusStack.Children.Add(_fleetNumberBox);

        _registerButton.Height = 40d;
        _registerButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _registerButton.Margin = new Thickness(0d, 12d, 0d, 0d);
        StylePrimaryButton(_registerButton);
        _registerButton.Click += RegisterButton_Click;
        currentBusStack.Children.Add(_registerButton);
        var busCard = NewCard(currentBusStack, new Thickness(0d));
        Grid.SetColumn(busCard, 2);
        identityAndBus.Children.Add(busCard);
        body.Children.Add(identityAndBus);

        var fleetHeader = new Grid { Margin = new Thickness(2d, 0d, 2d, 10d) };
        fleetHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        fleetHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _fleetHeading.Foreground = Brushes.White;
        _fleetHeading.FontSize = 16d;
        _fleetHeading.FontWeight = FontWeights.Bold;
        Grid.SetColumn(_fleetHeading, 0);
        fleetHeader.Children.Add(_fleetHeading);
        _fleetCountText.Foreground = Brush(82, 196, 255);
        _fleetCountText.FontSize = 10d;
        _fleetCountText.FontWeight = FontWeights.Bold;
        _fleetCountText.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_fleetCountText, 1);
        fleetHeader.Children.Add(_fleetCountText);
        body.Children.Add(fleetHeader);
        body.Children.Add(_fleetPanel);

        Grid.SetRow(scroller, 1);
        root.Children.Add(scroller);

        var footer = new Grid { Margin = new Thickness(0d, 18d, 0d, 0d) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock
        {
            Text = Text("LocalCompanyNote"),
            Foreground = Brush(104, 130, 146),
            FontSize = 9.5d,
            VerticalAlignment = VerticalAlignment.Center
        });
        _saveButton.Height = 42d;
        _saveButton.MinWidth = 190d;
        _saveButton.HorizontalAlignment = HorizontalAlignment.Right;
        StylePrimaryButton(_saveButton);
        _saveButton.Click += SaveButton_Click;
        Grid.SetColumn(_saveButton, 1);
        footer.Children.Add(_saveButton);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        _nameBox.TextChanged += (_, _) => RefreshCompanyPreview();
        _shortNameBox.TextChanged += (_, _) => RefreshCompanyPreview();
        _baseMapBox.TextChanged += (_, _) => RefreshCompanyPreview();

        return root;
    }

    private Border BuildCompanyHero()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var logo = new Border
        {
            Width = 82d,
            Height = 82d,
            Margin = new Thickness(0d, 0d, 18d, 0d),
            Background = new LinearGradientBrush(Color.FromRgb(19, 78, 117), Color.FromRgb(8, 37, 59), 45d),
            BorderBrush = Brush(57, 143, 198),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(18d),
            Child = _companyMonogram
        };
        _companyMonogram.HorizontalAlignment = HorizontalAlignment.Center;
        _companyMonogram.VerticalAlignment = VerticalAlignment.Center;
        _companyMonogram.Foreground = Brushes.White;
        _companyMonogram.FontSize = 25d;
        _companyMonogram.FontWeight = FontWeights.Bold;
        Grid.SetColumn(logo, 0);
        grid.Children.Add(logo);

        var identity = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        identity.Children.Add(new TextBlock
        {
            Text = Text("CompanyWorkspace").ToUpperInvariant(),
            Foreground = Brush(97, 154, 188),
            FontSize = 9d,
            FontWeight = FontWeights.Bold
        });
        var companyTitle = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 22d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 4d, 0d, 0d)
        };
        companyTitle.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Text") { Source = _nameBox, FallbackValue = "NavBR Transportes" });
        identity.Children.Add(companyTitle);
        _baseMapPreview.Foreground = Brush(132, 157, 174);
        _baseMapPreview.FontSize = 10.5d;
        _baseMapPreview.Margin = new Thickness(0d, 5d, 0d, 0d);
        identity.Children.Add(_baseMapPreview);
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);

        var stats = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        stats.Children.Add(BuildHeroMetric(Text("Fleet"), _fleetCountText));
        Grid.SetColumn(stats, 2);
        grid.Children.Add(stats);

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

    private static Border BuildHeroMetric(string label, TextBlock sharedValue)
    {
        var value = new TextBlock
        {
            Text = "0",
            Foreground = Brushes.White,
            FontSize = 20d,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        sharedValue.Tag = value;
        var stack = new StackPanel();
        stack.Children.Add(value);
        stack.Children.Add(new TextBlock
        {
            Text = label.ToUpperInvariant(),
            Foreground = Brush(91, 131, 155),
            FontSize = 8d,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0d, 2d, 0d, 0d)
        });
        return new Border
        {
            MinWidth = 94d,
            Padding = new Thickness(12d, 10d, 12d, 10d),
            Background = Brush(6, 19, 29),
            BorderBrush = Brush(24, 57, 76),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(11d),
            Child = stack
        };
    }

    private static Border BuildTab(string text, bool selected)
    {
        return new Border
        {
            Margin = new Thickness(0d, 0d, 8d, 0d),
            Padding = new Thickness(13d, 7d, 13d, 7d),
            Background = selected ? Brush(14, 62, 96) : Brush(8, 19, 27),
            BorderBrush = selected ? Brush(36, 119, 175) : Brush(25, 45, 57),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(8d),
            Child = new TextBlock
            {
                Text = text,
                Foreground = selected ? Brushes.White : Brush(142, 163, 177),
                FontSize = 10d,
                FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal
            }
        };
    }

    private static void AddField(Grid grid, TextBlock label, TextBox box, int column, int row, Thickness margin)
    {
        var stack = new StackPanel { Margin = margin };
        label.Foreground = Brush(128, 157, 175);
        label.FontSize = 9d;
        label.FontWeight = FontWeights.Bold;
        label.Margin = new Thickness(0d, 0d, 0d, 6d);
        stack.Children.Add(label);
        box.Height = 36d;
        box.Padding = new Thickness(9d, 6d, 9d, 6d);
        stack.Children.Add(box);
        Grid.SetColumn(stack, column);
        Grid.SetRow(stack, row);
        grid.Children.Add(stack);
    }

    private void LoadCompany(VirtualCompanyData company)
    {
        _nameBox.Text = company.Name;
        _shortNameBox.Text = company.ShortName;
        _baseMapBox.Text = company.BaseMap ?? string.Empty;
        RenderFleet(company);
        RefreshCurrentBus();
        RefreshCompanyPreview();
    }

    private void RefreshCompanyPreview()
    {
        var shortName = _shortNameBox.Text.Trim();
        var name = _nameBox.Text.Trim();
        _companyMonogram.Text = !string.IsNullOrWhiteSpace(shortName)
            ? shortName[..Math.Min(3, shortName.Length)].ToUpperInvariant()
            : !string.IsNullOrWhiteSpace(name)
                ? string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => char.ToUpperInvariant(part[0])))
                : "NB";
        _baseMapPreview.Text = string.IsNullOrWhiteSpace(_baseMapBox.Text)
            ? Text("NoBaseMap")
            : $"{Text("BaseMap")}: {_baseMapBox.Text.Trim()}";
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
        _fleetCountText.Text = string.Format(Text("VehicleCount"), company.Vehicles.Count);
        if (_fleetCountText.Tag is TextBlock heroValue)
        {
            heroValue.Text = company.Vehicles.Count.ToString();
        }

        if (company.Vehicles.Count == 0)
        {
            _fleetPanel.Children.Add(new Border
            {
                Padding = new Thickness(18d),
                Background = Brush(7, 18, 25),
                BorderBrush = Brush(24, 46, 59),
                BorderThickness = new Thickness(1d),
                CornerRadius = new CornerRadius(11d),
                Child = new TextBlock
                {
                    Text = Text("EmptyFleet"),
                    Foreground = Brush(126, 148, 162),
                    FontSize = 11d
                }
            });
            return;
        }

        foreach (var vehicle in company.Vehicles.OrderBy(vehicle => vehicle.FleetNumber, StringComparer.OrdinalIgnoreCase))
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new Border
            {
                Width = 46d,
                Height = 46d,
                Margin = new Thickness(0d, 0d, 13d, 0d),
                Background = Brush(8, 42, 63),
                BorderBrush = Brush(27, 92, 126),
                BorderThickness = new Thickness(1d),
                CornerRadius = new CornerRadius(10d),
                Child = new TextBlock
                {
                    Text = "BUS",
                    Foreground = Brush(82, 199, 255),
                    FontWeight = FontWeights.Bold,
                    FontSize = 9d,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            details.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(vehicle.FleetNumber) ? Text("NoFleetNumber") : vehicle.FleetNumber,
                Foreground = Brushes.White,
                FontSize = 14d,
                FontWeight = FontWeights.Bold
            });
            details.Children.Add(new TextBlock
            {
                Text = vehicle.VehicleModel,
                Foreground = Brush(148, 171, 185),
                FontSize = 10.5d,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0d, 3d, 0d, 0d)
            });
            if (vehicle.LastUsedAt is not null)
            {
                details.Children.Add(new TextBlock
                {
                    Text = $"{Text("LastUsed")}: {vehicle.LastUsedAt.Value.ToLocalTime():g}",
                    Foreground = Brush(99, 125, 143),
                    FontSize = 9d,
                    Margin = new Thickness(0d, 3d, 0d, 0d)
                });
            }
            Grid.SetColumn(details, 1);
            grid.Children.Add(details);

            var remove = new Button
            {
                Content = Text("Remove"),
                Tag = vehicle.Id,
                MinWidth = 88d,
                Height = 34d,
                Padding = new Thickness(10d, 5d, 10d, 5d),
                Background = Brush(26, 15, 19),
                Foreground = Brush(239, 176, 176),
                BorderBrush = Brush(82, 38, 46),
                BorderThickness = new Thickness(1d),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            remove.Click += (_, _) => VirtualCompanyStore.RemoveVehicle(vehicle.Id);
            Grid.SetColumn(remove, 2);
            grid.Children.Add(remove);

            _fleetPanel.Children.Add(NewCard(grid, new Thickness(0d, 0d, 0d, 9d)));
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
        RefreshCompanyPreview();

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
        RefreshCompanyPreview();
        RenderFleet(VirtualCompanyStore.Load());
    }

    private static void StylePrimaryButton(Button button)
    {
        button.Padding = new Thickness(16d, 8d, 16d, 8d);
        button.Background = Brush(19, 103, 171);
        button.Foreground = Brushes.White;
        button.BorderBrush = Brush(55, 155, 221);
        button.BorderThickness = new Thickness(1d);
        button.FontWeight = FontWeights.SemiBold;
        button.Cursor = System.Windows.Input.Cursors.Hand;
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
        ("Title", "Virtual company & fleet"), ("Subtitle", "Manage your company identity, active operation and buses from one workspace."),
        ("CompanyName", "COMPANY NAME"), ("ShortName", "SHORT NAME"), ("BaseMap", "BASE / MAIN MAP"),
        ("Fleet", "Fleet"), ("FleetNumber", "FLEET NUMBER (OPTIONAL)"), ("RegisterCurrentBus", "Register current bus"),
        ("SaveCompany", "Save company"), ("CurrentBus", "Current bus"), ("CurrentMap", "Current map"),
        ("NoBus", "No active OMSI bus"), ("EmptyFleet", "No buses registered yet."), ("Remove", "Remove"), ("LastUsed", "Last used"),
        ("Overview", "Overview"), ("Operation", "Operation"), ("CurrentOperation", "Current operation"), ("CompanyWorkspace", "Virtual transport company"),
        ("NoBaseMap", "No base map defined"), ("VehicleCount", "{0} vehicle(s)"), ("NoFleetNumber", "Unnumbered vehicle"),
        ("LocalCompanyNote", "Company data is stored locally and linked to your driver profile."));
    private static readonly IReadOnlyDictionary<string, string> Pt = T(
        ("Title", "Empresa virtual e frota"), ("Subtitle", "Gerencie identidade da empresa, operação atual e ônibus em um único painel."),
        ("CompanyName", "NOME DA EMPRESA"), ("ShortName", "SIGLA"), ("BaseMap", "BASE / MAPA PRINCIPAL"),
        ("Fleet", "Frota"), ("FleetNumber", "NÚMERO DE FROTA (OPCIONAL)"), ("RegisterCurrentBus", "Registrar ônibus atual"),
        ("SaveCompany", "Salvar empresa"), ("CurrentBus", "Ônibus atual"), ("CurrentMap", "Mapa atual"),
        ("NoBus", "Nenhum ônibus ativo no OMSI"), ("EmptyFleet", "Nenhum ônibus cadastrado ainda."), ("Remove", "Remover"), ("LastUsed", "Último uso"),
        ("Overview", "Visão geral"), ("Operation", "Operação"), ("CurrentOperation", "Operação atual"), ("CompanyWorkspace", "Empresa virtual de transporte"),
        ("NoBaseMap", "Nenhum mapa-base definido"), ("VehicleCount", "{0} veículo(s)"), ("NoFleetNumber", "Veículo sem prefixo"),
        ("LocalCompanyNote", "Os dados da empresa ficam salvos localmente e vinculados ao seu perfil de motorista."));
    private static readonly IReadOnlyDictionary<string, string> Es = T(
        ("Title", "Empresa virtual y flota"), ("Subtitle", "Gestiona identidad, operación activa y autobuses desde un único panel."),
        ("CompanyName", "NOMBRE DE LA EMPRESA"), ("ShortName", "SIGLA"), ("BaseMap", "BASE / MAPA PRINCIPAL"),
        ("Fleet", "Flota"), ("FleetNumber", "NÚMERO DE FLOTA (OPCIONAL)"), ("RegisterCurrentBus", "Registrar autobús actual"),
        ("SaveCompany", "Guardar empresa"), ("CurrentBus", "Autobús actual"), ("CurrentMap", "Mapa actual"),
        ("NoBus", "No hay autobús activo en OMSI"), ("EmptyFleet", "Todavía no hay autobuses registrados."), ("Remove", "Eliminar"), ("LastUsed", "Último uso"),
        ("Overview", "Resumen"), ("Operation", "Operación"), ("CurrentOperation", "Operación actual"), ("CompanyWorkspace", "Empresa virtual de transporte"),
        ("NoBaseMap", "Sin mapa base definido"), ("VehicleCount", "{0} vehículo(s)"), ("NoFleetNumber", "Vehículo sin número"),
        ("LocalCompanyNote", "Los datos de la empresa se guardan localmente y se vinculan al perfil del conductor."));
    private static readonly IReadOnlyDictionary<string, string> De = T(
        ("Title", "Virtuelles Unternehmen & Flotte"), ("Subtitle", "Unternehmensidentität, aktiven Betrieb und Busse in einem Bereich verwalten."),
        ("CompanyName", "UNTERNEHMENSNAME"), ("ShortName", "KÜRZEL"), ("BaseMap", "BASIS / HAUPTKARTE"),
        ("Fleet", "Flotte"), ("FleetNumber", "WAGENNUMMER (OPTIONAL)"), ("RegisterCurrentBus", "Aktuellen Bus registrieren"),
        ("SaveCompany", "Unternehmen speichern"), ("CurrentBus", "Aktueller Bus"), ("CurrentMap", "Aktuelle Karte"),
        ("NoBus", "Kein aktiver OMSI-Bus"), ("EmptyFleet", "Noch keine Busse registriert."), ("Remove", "Entfernen"), ("LastUsed", "Zuletzt genutzt"),
        ("Overview", "Übersicht"), ("Operation", "Betrieb"), ("CurrentOperation", "Aktueller Betrieb"), ("CompanyWorkspace", "Virtuelles Verkehrsunternehmen"),
        ("NoBaseMap", "Keine Basiskarte definiert"), ("VehicleCount", "{0} Fahrzeug(e)"), ("NoFleetNumber", "Fahrzeug ohne Nummer"),
        ("LocalCompanyNote", "Unternehmensdaten werden lokal gespeichert und mit dem Fahrerprofil verknüpft."));
    private static readonly IReadOnlyDictionary<string, string> Fr = T(
        ("Title", "Entreprise virtuelle et flotte"), ("Subtitle", "Gérez l’identité, l’exploitation active et les bus depuis un seul espace."),
        ("CompanyName", "NOM DE L’ENTREPRISE"), ("ShortName", "SIGLE"), ("BaseMap", "BASE / CARTE PRINCIPALE"),
        ("Fleet", "Flotte"), ("FleetNumber", "NUMÉRO DE PARC (OPTIONNEL)"), ("RegisterCurrentBus", "Enregistrer le bus actuel"),
        ("SaveCompany", "Enregistrer l’entreprise"), ("CurrentBus", "Bus actuel"), ("CurrentMap", "Carte actuelle"),
        ("NoBus", "Aucun bus OMSI actif"), ("EmptyFleet", "Aucun bus enregistré pour le moment."), ("Remove", "Retirer"), ("LastUsed", "Dernière utilisation"),
        ("Overview", "Aperçu"), ("Operation", "Exploitation"), ("CurrentOperation", "Exploitation actuelle"), ("CompanyWorkspace", "Entreprise virtuelle de transport"),
        ("NoBaseMap", "Aucune carte de base définie"), ("VehicleCount", "{0} véhicule(s)"), ("NoFleetNumber", "Véhicule sans numéro"),
        ("LocalCompanyNote", "Les données de l’entreprise sont stockées localement et liées au profil conducteur."));

    private static IReadOnlyDictionary<string, string> T(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}