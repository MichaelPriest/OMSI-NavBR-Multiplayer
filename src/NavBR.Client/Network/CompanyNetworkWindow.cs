using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;
using NavBR.Client.Windows;
using NavBR.Shared.Network;

namespace NavBR.Client.Network;

internal static class CompanyNetworkInstaller
{
    private const string ButtonTag = "alpha12-company-network";
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window) ||
            window.FindName(Alpha12ProfessionalShellInstaller.OperationsPanelName) is not Panel panel)
        {
            return;
        }

        var button = new Button { Tag = ButtonTag };
        StyleButton(button);
        ApplyLocalization(button);
        button.Click += (_, _) =>
        {
            if (Application.Current is App app)
            {
                new CompanyNetworkWindow(window, app.NetworkRuntime).ShowDialog();
            }
        };
        panel.Children.Add(button);

        SelectionChangedEventHandler languageChanged = (_, _) => ApplyLocalization(button);
        window.LanguageComboBox.SelectionChanged += languageChanged;
        window.Closed += (_, _) =>
        {
            window.LanguageComboBox.SelectionChanged -= languageChanged;
            Installed.Remove(window);
        };
    }

    private static void ApplyLocalization(Button button)
    {
        button.Content = LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "◎  Rede da empresa",
            "es" => "◎  Red de la empresa",
            "de" => "◎  Unternehmensnetzwerk",
            "fr" => "◎  Réseau de l’entreprise",
            _ => "◎  Company network"
        };
    }

    private static void StyleButton(Button button)
    {
        button.Height = 44d;
        button.Margin = new Thickness(0d, 0d, 0d, 6d);
        button.Padding = new Thickness(13d, 9d, 13d, 9d);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Background = new SolidColorBrush(Color.FromRgb(10, 19, 25));
        button.Foreground = new SolidColorBrush(Color.FromRgb(218, 230, 238));
        button.BorderBrush = new SolidColorBrush(Color.FromRgb(28, 42, 51));
        button.BorderThickness = new Thickness(1d);
        button.FontSize = 12.5d;
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }
}

internal sealed class CompanyNetworkWindow : Window
{
    private readonly NavBRNetworkRuntime _runtime;
    private readonly TextBlock _status = new();
    private readonly TextBlock _identity = new();
    private readonly TextBlock _company = new();
    private readonly TextBlock _nodeAddresses = new();
    private readonly TextBox _nodeUrl = new();
    private readonly TextBox _inviteCode = new();
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private readonly Button _inviteButton;

    public CompanyNetworkWindow(Window owner, NavBRNetworkRuntime runtime)
    {
        Owner = owner;
        _runtime = runtime;
        Title = T("Rede da Empresa — NavBR", "Company Network — NavBR", "Red de la empresa — NavBR", "Unternehmensnetzwerk — NavBR", "Réseau de l’entreprise — NavBR");
        Width = 900d;
        Height = 720d;
        MinWidth = 760d;
        MinHeight = 620d;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(6, 12, 18);

        _startButton = Button(T("Hospedar empresa neste PC", "Host company on this PC", "Hospedar empresa en este PC", "Unternehmen auf diesem PC hosten", "Héberger l’entreprise sur ce PC"), StartNode_Click);
        _stopButton = Button(T("Parar Company Node", "Stop Company Node", "Detener Company Node", "Company Node stoppen", "Arrêter Company Node"), StopNode_Click);
        _inviteButton = Button(T("Criar convite de motorista", "Create driver invite", "Crear invitación de conductor", "Fahrereinladung erstellen", "Créer une invitation conducteur"), CreateInvite_Click);

        Content = BuildContent();
        Loaded += (_, _) => Refresh();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(26d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = T("NavBR Company Network", "NavBR Company Network", "NavBR Company Network", "NavBR Company Network", "NavBR Company Network"),
            Foreground = Brushes.White,
            FontSize = 25d,
            FontWeight = FontWeights.SemiBold
        });
        heading.Children.Add(new TextBlock
        {
            Text = T(
                "Sua empresa online é hospedada pelos próprios membros. Não depende de Supabase nem de conta Steam.",
                "Your online company is hosted by its own members. It does not depend on Supabase or Steam accounts.",
                "Tu empresa online es alojada por sus propios miembros. No depende de Supabase ni Steam.",
                "Ihr Online-Unternehmen wird von den eigenen Mitgliedern gehostet. Kein Supabase- oder Steam-Konto erforderlich.",
                "Votre entreprise en ligne est hébergée par ses propres membres, sans dépendre de Supabase ni de Steam."),
            Foreground = Brush(151, 171, 185),
            FontSize = 12d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 6d, 0d, 18d)
        });
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var body = new StackPanel();
        scroll.Content = body;
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        _identity.TextWrapping = TextWrapping.Wrap;
        body.Children.Add(Card(
            T("Identidade NavBR", "NavBR identity", "Identidad NavBR", "NavBR-Identität", "Identité NavBR"),
            _identity));

        _company.TextWrapping = TextWrapping.Wrap;
        body.Children.Add(Card(
            T("Empresa e cargo", "Company and role", "Empresa y cargo", "Unternehmen und Rolle", "Entreprise et rôle"),
            _company));

        var hostStack = new StackPanel();
        hostStack.Children.Add(new TextBlock
        {
            Text = T(
                "O Company Node usa a porta TCP 27740 e é independente da sala multiplayer. Fechar esta janela não encerra o nó.",
                "The Company Node uses TCP 27740 and is independent from the multiplayer room. Closing this window does not stop it.",
                "Company Node usa TCP 27740 y es independiente de la sala multijugador. Cerrar esta ventana no lo detiene.",
                "Der Company Node nutzt TCP 27740 und läuft unabhängig vom Multiplayer-Raum. Das Schließen dieses Fensters beendet ihn nicht.",
                "Le Company Node utilise TCP 27740 indépendamment de la salle multijoueur. Fermer cette fenêtre ne l’arrête pas."),
            Foreground = Brush(151, 171, 185),
            FontSize = 11d,
            TextWrapping = TextWrapping.Wrap
        });
        var hostButtons = new WrapPanel { Margin = new Thickness(0d, 12d, 0d, 8d) };
        hostButtons.Children.Add(_startButton);
        hostButtons.Children.Add(_stopButton);
        hostButtons.Children.Add(_inviteButton);
        hostStack.Children.Add(hostButtons);
        _nodeAddresses.Foreground = Brush(113, 198, 255);
        _nodeAddresses.FontFamily = new FontFamily("Consolas");
        _nodeAddresses.FontSize = 11d;
        _nodeAddresses.TextWrapping = TextWrapping.Wrap;
        hostStack.Children.Add(_nodeAddresses);
        var networkActions = new Grid { Margin = new Thickness(0d, 0d, 0d, 14d) };
        networkActions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        networkActions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16d) });
        networkActions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var hostCard = Card("Company Node", hostStack);
        hostCard.Margin = new Thickness(0d);
        Grid.SetColumn(hostCard, 0);
        networkActions.Children.Add(hostCard);

        var joinStack = new StackPanel();
        joinStack.Children.Add(Label(T("Endereço do Company Node", "Company Node address", "Dirección del Company Node", "Company-Node-Adresse", "Adresse du Company Node")));
        ConfigureTextBox(_nodeUrl, "http://192.168.0.10:27740");
        joinStack.Children.Add(_nodeUrl);
        joinStack.Children.Add(Label(T("Código do convite", "Invite code", "Código de invitación", "Einladungscode", "Code d’invitation")));
        ConfigureTextBox(_inviteCode, "NBR-....");
        joinStack.Children.Add(_inviteCode);
        var join = Button(T("Entrar na empresa", "Join company", "Entrar en la empresa", "Unternehmen beitreten", "Rejoindre l’entreprise"), Join_Click);
        join.Margin = new Thickness(0d, 12d, 0d, 0d);
        joinStack.Children.Add(join);
        var joinCard = Card(
            T("Entrar em uma Empresa Online", "Join an Online Company", "Entrar en una empresa online", "Einem Online-Unternehmen beitreten", "Rejoindre une entreprise en ligne"),
            joinStack);
        joinCard.Margin = new Thickness(0d);
        Grid.SetColumn(joinCard, 2);
        networkActions.Children.Add(joinCard);
        body.Children.Add(networkActions);

        _status.Foreground = Brush(185, 202, 214);
        _status.FontSize = 11.5d;
        _status.TextWrapping = TextWrapping.Wrap;
        body.Children.Add(Card(T("Estado", "Status", "Estado", "Status", "État"), _status));
        return root;
    }

    private async void StartNode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _status.Text = T("Iniciando Company Node...", "Starting Company Node...", "Iniciando Company Node...", "Company Node wird gestartet...", "Démarrage du Company Node...");
            await _runtime.StartCompanyNodeAsync();
            _status.Text = T("Company Node online.", "Company Node online.", "Company Node online.", "Company Node online.", "Company Node en ligne.");
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
        Refresh();
    }

    private async void StopNode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _runtime.CompanyNode.StopAsync();
            _status.Text = T("Company Node parado.", "Company Node stopped.", "Company Node detenido.", "Company Node gestoppt.", "Company Node arrêté.");
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
        Refresh();
    }

    private void CreateInvite_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var code = _runtime.CreateDriverInvite();
            var address = _runtime.CompanyNode.GetLanUrls().FirstOrDefault() ?? _runtime.CompanyNode.LocalUrl;
            var company = CompanyNodeStore.LoadCompany();
            var invite = $"NAVBR_COMPANY_INVITE_V1\nserver={address}\ncompany={company?.CompanyId}\ncode={code}";
            Clipboard.SetText(invite);
            _status.Text = T(
                $"Convite de Motorista copiado. Código: {code}",
                $"Driver invite copied. Code: {code}",
                $"Invitación de conductor copiada. Código: {code}",
                $"Fahrereinladung kopiert. Code: {code}",
                $"Invitation conducteur copiée. Code : {code}");
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

    private async void Join_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _status.Text = T("Validando convite e identidade...", "Validating invite and identity...", "Validando invitación e identidad...", "Einladung und Identität werden geprüft...", "Validation de l’invitation et de l’identité...");
            var result = await _runtime.JoinAsync(_nodeUrl.Text, _inviteCode.Text);
            _status.Text = result.Success
                ? T("Entrada confirmada pela empresa.", "Company membership confirmed.", "Ingreso confirmado por la empresa.", "Unternehmensbeitritt bestätigt.", "Adhésion confirmée par l’entreprise.")
                : $"{T("Entrada recusada", "Join rejected", "Ingreso rechazado", "Beitritt abgelehnt", "Adhésion refusée")}: {result.Error}";
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
        Refresh();
    }

    private void Refresh()
    {
        var identity = _runtime.Identity;
        _identity.Text = $"{identity.DisplayName}\n{identity.PlayerId}\n{T("Chave pública local; a chave privada não sai deste Windows.", "Local public key; the private key never leaves this Windows account.", "Clave pública local; la clave privada no sale de esta cuenta de Windows.", "Lokaler öffentlicher Schlüssel; der private Schlüssel verlässt dieses Windows-Konto nicht.", "Clé publique locale ; la clé privée ne quitte pas ce compte Windows.")}";

        var membership = _runtime.Membership;
        var hosted = CompanyNodeStore.LoadCompany();
        _company.Text = membership is not null
            ? $"{membership.CompanyName}\n{T("Cargo", "Role", "Cargo", "Rolle", "Rôle")}: {RoleText(membership.Role)}\nID: {membership.CompanyId}"
            : hosted is not null
                ? $"{hosted.Name}\n{T("Cargo", "Role", "Cargo", "Rolle", "Rôle")}: {RoleText(CompanyRole.President)}\nID: {hosted.CompanyId}"
                : T("Nenhuma Empresa Online vinculada.", "No Online Company linked.", "Ninguna empresa online vinculada.", "Kein Online-Unternehmen verknüpft.", "Aucune entreprise en ligne associée.");

        _startButton.IsEnabled = !_runtime.CompanyNode.IsRunning;
        _stopButton.IsEnabled = _runtime.CompanyNode.IsRunning;
        _inviteButton.IsEnabled = _runtime.CompanyNode.IsRunning && hosted is not null;
        _nodeAddresses.Text = _runtime.CompanyNode.IsRunning
            ? $"{T("Nó online", "Node online", "Nodo online", "Node online", "Nœud en ligne")}:\n" +
              string.Join("\n", _runtime.CompanyNode.GetLanUrls().Prepend(_runtime.CompanyNode.LocalUrl).Distinct())
            : T("Company Node offline.", "Company Node offline.", "Company Node offline.", "Company Node offline.", "Company Node hors ligne.");
    }

    private static string RoleText(CompanyRole role) => role switch
    {
        CompanyRole.President => T("Presidente", "President", "Presidente", "Präsident", "Président"),
        CompanyRole.VicePresident => T("Vice-Presidente", "Vice President", "Vicepresidente", "Vizepräsident", "Vice-président"),
        CompanyRole.Director => T("Diretoria", "Director", "Dirección", "Direktion", "Direction"),
        CompanyRole.OperationsManager => T("Gerente Operacional", "Operations Manager", "Gerente operativo", "Betriebsleiter", "Responsable opérations"),
        CompanyRole.Dispatcher => "CCO / Dispatcher",
        CompanyRole.Supervisor => T("Fiscal / Supervisor", "Supervisor", "Supervisor", "Supervisor", "Superviseur"),
        CompanyRole.SeniorDriver => T("Motorista Sênior", "Senior Driver", "Conductor sénior", "Senior-Fahrer", "Conducteur senior"),
        CompanyRole.Driver => T("Motorista", "Driver", "Conductor", "Fahrer", "Conducteur"),
        _ => T("Aprendiz", "Trainee", "Aprendiz", "Anwärter", "Apprenti")
    };

    private static Border Card(string title, UIElement content)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontSize = 14d,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0d, 0d, 0d, 10d)
        });
        stack.Children.Add(content);
        return new Border
        {
            Margin = new Thickness(0d, 0d, 0d, 14d),
            Padding = new Thickness(18d),
            Background = Brush(10, 19, 26),
            BorderBrush = Brush(30, 48, 60),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(13d),
            Child = stack
        };
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        Foreground = Brush(153, 172, 185),
        FontSize = 10.5d,
        Margin = new Thickness(0d, 8d, 0d, 4d)
    };

    private static void ConfigureTextBox(TextBox textBox, string placeholder)
    {
        textBox.MinHeight = 36d;
        textBox.Padding = new Thickness(10d, 7d, 10d, 7d);
        textBox.Background = Brush(7, 14, 20);
        textBox.Foreground = Brushes.White;
        textBox.BorderBrush = Brush(42, 62, 76);
        textBox.BorderThickness = new Thickness(1d);
        textBox.ToolTip = placeholder;
    }

    private static Button Button(string text, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = text,
            MinHeight = 38d,
            Padding = new Thickness(14d, 8d, 14d, 8d),
            Margin = new Thickness(0d, 0d, 8d, 0d),
            Background = Brush(17, 45, 67),
            Foreground = Brushes.White,
            BorderBrush = Brush(61, 137, 196),
            BorderThickness = new Thickness(1d),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += handler;
        return button;
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

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
