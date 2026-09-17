using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private bool _roomWizardInstalled;

    [ModuleInitializer]
    internal static void InitializeRoomWizardBootstrap()
    {
        EventManager.RegisterClassHandler(
            typeof(MultiplayerWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(RoomWizardWindowLoaded));
    }

    private static void RoomWizardWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MultiplayerWindow window)
        {
            return;
        }

        _ = window.Dispatcher.BeginInvoke(
            new Action(window.InstallRoomWizard),
            DispatcherPriority.ContextIdle);
    }

    private void InstallRoomWizard()
    {
        if (_roomWizardInstalled || Content is not Grid root)
        {
            return;
        }

        var legacyQuickPanel = FindWizardAncestor<Border>(RoomTextBox);
        if (legacyQuickPanel is null)
        {
            return;
        }

        _roomWizardInstalled = true;
        legacyQuickPanel.Visibility = Visibility.Collapsed;

        var wizard = BuildRoomWizard();
        Grid.SetRow(wizard, 3);
        root.Children.Add(wizard);
    }

    private FrameworkElement BuildRoomWizard()
    {
        var currentStep = 0;

        var roomBox = NewWizardTextBox(RoomTextBox.Text, 64);
        var nicknameBox = NewWizardTextBox(NicknameTextBox.Text, 32);
        var passwordBox = new PasswordBox
        {
            MaxLength = 128,
            Padding = new Thickness(10d, 7d),
            Background = WizardBrush(5, 15, 24),
            Foreground = Brushes.White,
            BorderBrush = WizardBrush(35, 63, 82),
            BorderThickness = new Thickness(1d)
        };
        passwordBox.Password = RoomPasswordBox.Password;

        var publicRoom = new RadioButton
        {
            GroupName = "NavBRRoomPrivacyWizard",
            IsChecked = PrivateRoomCheckBox.IsChecked != true,
            Content = WizardText("Sala pública", "Public room", "Sala pública", "Öffentlicher Raum", "Salle publique"),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0d, 4d, 0d, 8d)
        };
        var privateRoom = new RadioButton
        {
            GroupName = "NavBRRoomPrivacyWizard",
            IsChecked = PrivateRoomCheckBox.IsChecked == true,
            Content = WizardText("Sala privada com senha", "Private room with password", "Sala privada con contraseña", "Privater Raum mit Passwort", "Salle privée avec mot de passe"),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0d, 4d, 0d, 8d)
        };

        var passwordPanel = new StackPanel
        {
            Margin = new Thickness(22d, 5d, 0d, 0d),
            IsEnabled = privateRoom.IsChecked == true
        };
        passwordPanel.Children.Add(WizardLabel(WizardText("Senha da sala", "Room password", "Contraseña de la sala", "Raumpasswort", "Mot de passe de la salle")));
        passwordPanel.Children.Add(passwordBox);
        passwordPanel.Children.Add(new TextBlock
        {
            Text = WizardText(
                "Use pelo menos 4 caracteres. A senha não é salva permanentemente.",
                "Use at least 4 characters. The password is not stored permanently.",
                "Usa al menos 4 caracteres. La contraseña no se guarda de forma permanente.",
                "Mindestens 4 Zeichen. Das Passwort wird nicht dauerhaft gespeichert.",
                "Utilisez au moins 4 caractères. Le mot de passe n’est pas enregistré durablement."),
            Foreground = WizardBrush(112, 139, 158),
            FontSize = 10d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 5d, 0d, 0d)
        });

        var upnpCheck = new CheckBox
        {
            IsChecked = UpnpEnabledCheckBox.IsChecked == true,
            Content = WizardText(
                "Tentar liberar TCP 27730 automaticamente com UPnP",
                "Try to open TCP 27730 automatically with UPnP",
                "Intentar abrir TCP 27730 automáticamente con UPnP",
                "TCP 27730 automatisch per UPnP freigeben",
                "Essayer d’ouvrir automatiquement TCP 27730 avec UPnP"),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0d, 7d, 0d, 4d)
        };

        var networkHint = new TextBlock
        {
            Text = WizardText(
                "Opcional. Para jogar na mesma rede, você pode deixar desligado. Para Internet, o NavBR tentará configurar o roteador somente se você ativar esta opção.",
                "Optional. For LAN play you can leave this off. For Internet play, NavBR will try to configure the router only if you enable this option.",
                "Opcional. Para jugar en la misma red puedes dejarlo desactivado. Para Internet, NavBR intentará configurar el router solo si activas esta opción.",
                "Optional. Im lokalen Netz kann dies deaktiviert bleiben. Für Internet-Spiel versucht NavBR den Router nur zu konfigurieren, wenn Sie diese Option aktivieren.",
                "Optionnel. Pour jouer sur le réseau local, laissez cette option désactivée. Pour Internet, NavBR tentera de configurer le routeur uniquement si vous l’activez."),
            Foreground = WizardBrush(112, 139, 158),
            FontSize = 10.2d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(22d, 0d, 0d, 11d)
        };

        var summary = new TextBlock
        {
            Foreground = WizardBrush(205, 221, 232),
            FontSize = 11d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 10d, 0d, 4d)
        };

        var pageRoom = new StackPanel();
        pageRoom.Children.Add(WizardPageTitle(
            WizardText("1. Sala", "1. Room", "1. Sala", "1. Raum", "1. Salle"),
            WizardText(
                "Escolha o nome da sala e como seu nome aparecerá para os outros motoristas.",
                "Choose the room name and how your name will appear to other drivers.",
                "Elige el nombre de la sala y cómo aparecerá tu nombre para los demás conductores.",
                "Wählen Sie den Raumnamen und wie Ihr Name für andere Fahrer angezeigt wird.",
                "Choisissez le nom de la salle et le nom affiché aux autres conducteurs.")));
        pageRoom.Children.Add(WizardField(WizardText("Nome da sala", "Room name", "Nombre de la sala", "Raumname", "Nom de la salle"), roomBox));
        pageRoom.Children.Add(WizardField(WizardText("Seu nome", "Your name", "Tu nombre", "Ihr Name", "Votre nom"), nicknameBox));
        pageRoom.Children.Add(new TextBlock
        {
            Text = WizardText(
                "Se o nome da sala ficar vazio, o NavBR cria um identificador automaticamente.",
                "If the room name is empty, NavBR creates an identifier automatically.",
                "Si el nombre de la sala está vacío, NavBR crea un identificador automáticamente.",
                "Wenn der Raumname leer ist, erstellt NavBR automatisch eine Kennung.",
                "Si le nom de la salle est vide, NavBR crée automatiquement un identifiant."),
            Foreground = WizardBrush(105, 131, 150),
            FontSize = 10d,
            TextWrapping = TextWrapping.Wrap
        });

        var pagePrivacy = new StackPanel { Visibility = Visibility.Collapsed };
        pagePrivacy.Children.Add(WizardPageTitle(
            WizardText("2. Privacidade", "2. Privacy", "2. Privacidad", "2. Privatsphäre", "2. Confidentialité"),
            WizardText(
                "Decida se qualquer pessoa com o endereço pode entrar ou se a sala exige senha.",
                "Choose whether anyone with the address can join or whether the room requires a password.",
                "Decide si cualquiera con la dirección puede entrar o si la sala requiere contraseña.",
                "Entscheiden Sie, ob jeder mit der Adresse beitreten kann oder ein Passwort erforderlich ist.",
                "Choisissez si toute personne disposant de l’adresse peut entrer ou si un mot de passe est requis.")));
        pagePrivacy.Children.Add(publicRoom);
        pagePrivacy.Children.Add(privateRoom);
        pagePrivacy.Children.Add(passwordPanel);

        var pageNetwork = new StackPanel { Visibility = Visibility.Collapsed };
        pageNetwork.Children.Add(WizardPageTitle(
            WizardText("3. Rede", "3. Network", "3. Red", "3. Netzwerk", "3. Réseau"),
            WizardText(
                "A sala funciona primeiro no seu PC. Escolha apenas se quer tentar facilitar acesso pela Internet.",
                "The room runs on your PC first. Choose only whether to try to make Internet access easier.",
                "La sala funciona primero en tu PC. Elige solo si quieres facilitar el acceso por Internet.",
                "Der Raum läuft zuerst auf Ihrem PC. Wählen Sie nur, ob der Internetzugang erleichtert werden soll.",
                "La salle fonctionne d’abord sur votre PC. Choisissez uniquement si vous souhaitez faciliter l’accès Internet.")));
        pageNetwork.Children.Add(new Border
        {
            Background = WizardBrush(7, 26, 39),
            BorderBrush = WizardBrush(29, 64, 86),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(10d),
            Padding = new Thickness(12d),
            Child = new TextBlock
            {
                Text = "PEER-HOST • TCP 27730",
                Foreground = WizardBrush(92, 190, 255),
                FontSize = 10d,
                FontWeight = FontWeights.Bold
            }
        });
        pageNetwork.Children.Add(upnpCheck);
        pageNetwork.Children.Add(networkHint);
        pageNetwork.Children.Add(summary);

        var pages = new Grid();
        pages.Children.Add(pageRoom);
        pages.Children.Add(pagePrivacy);
        pages.Children.Add(pageNetwork);

        var stepBadges = new[]
        {
            NewStepBadge("1", WizardText("Sala", "Room", "Sala", "Raum", "Salle")),
            NewStepBadge("2", WizardText("Privacidade", "Privacy", "Privacidad", "Privatsphäre", "Confidentialité")),
            NewStepBadge("3", WizardText("Rede", "Network", "Red", "Netzwerk", "Réseau"))
        };

        var stepStrip = new Grid { Margin = new Thickness(0d, 0d, 0d, 13d) };
        for (var i = 0; i < 3; i++)
        {
            stepStrip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            Grid.SetColumn(stepBadges[i], i);
            stepStrip.Children.Add(stepBadges[i]);
        }

        var previousButton = WizardSecondaryButton(WizardText("Voltar", "Back", "Atrás", "Zurück", "Retour"));
        var nextButton = WizardPrimaryButton(WizardText("Continuar", "Continue", "Continuar", "Weiter", "Continuer"));
        var createButton = WizardPrimaryButton(WizardText("Criar sala", "Create room", "Crear sala", "Raum erstellen", "Créer la salle"));
        createButton.Visibility = Visibility.Collapsed;

        var joinButton = WizardSecondaryButton(WizardText("Entrar por convite", "Join by invite", "Entrar por invitación", "Per Einladung beitreten", "Rejoindre par invitation"));
        joinButton.ToolTip = WizardText(
            "Use Colar convite na área do mapa e depois entre na sala.",
            "Use Paste invite in the map area, then join the room.",
            "Usa Pegar invitación en el área del mapa y después entra en la sala.",
            "Nutzen Sie Einladung einfügen im Kartenbereich und treten Sie dann bei.",
            "Utilisez Coller l’invitation dans la zone de carte, puis rejoignez la salle.");

        var problemsButton = WizardLinkButton(WizardText(
            "Problemas de conexão?",
            "Connection problems?",
            "¿Problemas de conexión?",
            "Verbindungsprobleme?",
            "Problèmes de connexion ?"));
        problemsButton.Click += (_, _) =>
        {
            var diagnostics = new NatDiagnosticsWindow(this);
            diagnostics.ShowDialog();
        };
        problemsButton.Visibility = Visibility.Collapsed;

        var footer = new Grid { Margin = new Thickness(0d, 14d, 0d, 0d) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftButtons = new StackPanel { Orientation = Orientation.Horizontal };
        leftButtons.Children.Add(joinButton);
        leftButtons.Children.Add(problemsButton);
        Grid.SetColumn(leftButtons, 0);
        footer.Children.Add(leftButtons);

        var navigationButtons = new StackPanel { Orientation = Orientation.Horizontal };
        previousButton.Margin = new Thickness(0d, 0d, 8d, 0d);
        nextButton.Margin = new Thickness(0d, 0d, 0d, 0d);
        navigationButtons.Children.Add(previousButton);
        navigationButtons.Children.Add(nextButton);
        navigationButtons.Children.Add(createButton);
        Grid.SetColumn(navigationButtons, 1);
        footer.Children.Add(navigationButtons);

        void UpdateSummary()
        {
            var room = string.IsNullOrWhiteSpace(roomBox.Text)
                ? WizardText("gerada automaticamente", "generated automatically", "generada automáticamente", "automatisch erzeugt", "générée automatiquement")
                : roomBox.Text.Trim();
            var privacy = privateRoom.IsChecked == true
                ? WizardText("privada", "private", "privada", "privat", "privée")
                : WizardText("pública", "public", "pública", "öffentlich", "publique");
            var network = upnpCheck.IsChecked == true
                ? "UPnP"
                : WizardText("rede local / manual", "LAN / manual", "red local / manual", "LAN / manuell", "réseau local / manuel");

            summary.Text = WizardText(
                $"Sala: {room} • {privacy} • Rede: {network}",
                $"Room: {room} • {privacy} • Network: {network}",
                $"Sala: {room} • {privacy} • Red: {network}",
                $"Raum: {room} • {privacy} • Netzwerk: {network}",
                $"Salle : {room} • {privacy} • Réseau : {network}");
        }

        void ShowStep(int step)
        {
            currentStep = Math.Clamp(step, 0, 2);
            pageRoom.Visibility = currentStep == 0 ? Visibility.Visible : Visibility.Collapsed;
            pagePrivacy.Visibility = currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            pageNetwork.Visibility = currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;

            for (var i = 0; i < stepBadges.Length; i++)
            {
                StyleStepBadge(stepBadges[i], i == currentStep, i < currentStep);
            }

            previousButton.Visibility = currentStep == 0 ? Visibility.Collapsed : Visibility.Visible;
            nextButton.Visibility = currentStep < 2 ? Visibility.Visible : Visibility.Collapsed;
            createButton.Visibility = currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            problemsButton.Visibility = currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            joinButton.Visibility = currentStep == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateSummary();
        }

        roomBox.TextChanged += (_, _) =>
        {
            if (!string.Equals(RoomTextBox.Text, roomBox.Text, StringComparison.Ordinal))
            {
                RoomTextBox.Text = roomBox.Text;
            }
            UpdateSummary();
        };
        RoomTextBox.TextChanged += (_, _) =>
        {
            if (!string.Equals(roomBox.Text, RoomTextBox.Text, StringComparison.Ordinal))
            {
                roomBox.Text = RoomTextBox.Text;
            }
        };

        nicknameBox.TextChanged += (_, _) =>
        {
            if (!string.Equals(NicknameTextBox.Text, nicknameBox.Text, StringComparison.Ordinal))
            {
                NicknameTextBox.Text = nicknameBox.Text;
            }
        };
        NicknameTextBox.TextChanged += (_, _) =>
        {
            if (!string.Equals(nicknameBox.Text, NicknameTextBox.Text, StringComparison.Ordinal))
            {
                nicknameBox.Text = NicknameTextBox.Text;
            }
        };

        void SyncPrivacy()
        {
            var isPrivate = privateRoom.IsChecked == true;
            passwordPanel.IsEnabled = isPrivate;
            if (PrivateRoomCheckBox.IsChecked != isPrivate)
            {
                PrivateRoomCheckBox.IsChecked = isPrivate;
            }
            UpdateSummary();
        }
        publicRoom.Checked += (_, _) => SyncPrivacy();
        privateRoom.Checked += (_, _) => SyncPrivacy();

        passwordBox.PasswordChanged += (_, _) =>
        {
            if (!string.Equals(RoomPasswordBox.Password, passwordBox.Password, StringComparison.Ordinal))
            {
                RoomPasswordBox.Password = passwordBox.Password;
            }
        };
        RoomPasswordBox.PasswordChanged += (_, _) =>
        {
            if (!string.Equals(passwordBox.Password, RoomPasswordBox.Password, StringComparison.Ordinal))
            {
                passwordBox.Password = RoomPasswordBox.Password;
            }
        };

        void SyncUpnp()
        {
            if (UpnpEnabledCheckBox.IsChecked != upnpCheck.IsChecked)
            {
                UpnpEnabledCheckBox.IsChecked = upnpCheck.IsChecked;
            }
            UpdateSummary();
        }
        upnpCheck.Checked += (_, _) => SyncUpnp();
        upnpCheck.Unchecked += (_, _) => SyncUpnp();
        UpnpEnabledCheckBox.Checked += (_, _) => upnpCheck.IsChecked = true;
        UpnpEnabledCheckBox.Unchecked += (_, _) => upnpCheck.IsChecked = false;

        previousButton.Click += (_, _) => ShowStep(currentStep - 1);
        nextButton.Click += (_, _) =>
        {
            if (currentStep == 0)
            {
                if (string.IsNullOrWhiteSpace(nicknameBox.Text))
                {
                    StatusDetailText.Text = LocalizationService.Get("MultiplayerRequiredFields");
                    nicknameBox.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(roomBox.Text))
                {
                    roomBox.Text = $"navbr-{Random.Shared.Next(1000, 9999)}";
                }
            }
            else if (currentStep == 1 && privateRoom.IsChecked == true && passwordBox.Password.Length < 4)
            {
                StatusDetailText.Text = RoomPrivacyText.PasswordTooShort;
                passwordBox.Focus();
                return;
            }

            ShowStep(currentStep + 1);
        };

        joinButton.Click += (_, _) =>
        {
            RoomTextBox.Text = roomBox.Text;
            NicknameTextBox.Text = nicknameBox.Text;
            PrepareRoomPrivacyForAction(createPrivateRoom: false);
            ConnectButton_Click(ConnectButton, new RoutedEventArgs(Button.ClickEvent));
        };

        createButton.Click += (_, _) =>
        {
            RoomTextBox.Text = roomBox.Text;
            NicknameTextBox.Text = nicknameBox.Text;
            PrivateRoomCheckBox.IsChecked = privateRoom.IsChecked == true;
            RoomPasswordBox.Password = passwordBox.Password;
            UpnpEnabledCheckBox.IsChecked = upnpCheck.IsChecked;

            if (!PrepareRoomPrivacyForAction(createPrivateRoom: privateRoom.IsChecked == true))
            {
                ShowStep(1);
                return;
            }

            CreateRoomButton_Click(CreateRoomButton, new RoutedEventArgs(Button.ClickEvent));
        };

        var body = new StackPanel();
        body.Children.Add(stepStrip);
        body.Children.Add(pages);
        body.Children.Add(footer);

        ShowStep(0);

        return new Border
        {
            Margin = new Thickness(0d, 14d, 0d, 0d),
            Background = WizardBrush(10, 24, 36),
            BorderBrush = WizardBrush(29, 52, 69),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(13d),
            Padding = new Thickness(14d),
            Child = body
        };
    }

    private static Border NewStepBadge(string number, string label)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        stack.Children.Add(new Border
        {
            Width = 24d,
            Height = 24d,
            CornerRadius = new CornerRadius(12d),
            Background = WizardBrush(20, 48, 67),
            Child = new TextBlock
            {
                Text = number,
                Foreground = WizardBrush(132, 199, 245),
                FontSize = 10d,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = WizardBrush(132, 154, 170),
            FontSize = 10.5d,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(7d, 0d, 0d, 0d)
        });

        return new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = WizardBrush(28, 49, 64),
            BorderThickness = new Thickness(0d, 0d, 0d, 2d),
            Padding = new Thickness(6d, 4d, 6d, 9d),
            Margin = new Thickness(0d, 0d, 8d, 0d),
            Child = stack
        };
    }

    private static void StyleStepBadge(Border badge, bool active, bool complete)
    {
        badge.BorderBrush = active
            ? WizardBrush(62, 165, 230)
            : complete
                ? WizardBrush(42, 137, 91)
                : WizardBrush(28, 49, 64);
        badge.Opacity = active || complete ? 1d : 0.7d;
    }

    private static FrameworkElement WizardPageTitle(string title, string subtitle)
    {
        var stack = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 11d) };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontSize = 14d,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = subtitle,
            Foreground = WizardBrush(120, 146, 164),
            FontSize = 10.5d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 4d, 0d, 0d)
        });
        return stack;
    }

    private static FrameworkElement WizardField(string label, Control control)
    {
        var stack = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 10d) };
        stack.Children.Add(WizardLabel(label));
        stack.Children.Add(control);
        return stack;
    }

    private static TextBlock WizardLabel(string text) => new()
    {
        Text = text,
        Foreground = WizardBrush(151, 176, 193),
        FontSize = 9.5d,
        FontWeight = FontWeights.Bold,
        Margin = new Thickness(0d, 0d, 0d, 6d)
    };

    private static TextBox NewWizardTextBox(string? text, int maxLength) => new()
    {
        Text = text ?? string.Empty,
        MaxLength = maxLength,
        Height = 36d,
        Padding = new Thickness(9d, 6d),
        Background = WizardBrush(5, 15, 24),
        Foreground = Brushes.White,
        BorderBrush = WizardBrush(35, 63, 82),
        BorderThickness = new Thickness(1d)
    };

    private static Button WizardPrimaryButton(string text) => new()
    {
        Content = text,
        MinWidth = 128d,
        Height = 38d,
        Padding = new Thickness(15d, 7d, 15d, 7d),
        Background = WizardBrush(19, 103, 171),
        Foreground = Brushes.White,
        BorderBrush = WizardBrush(55, 155, 221),
        BorderThickness = new Thickness(1d),
        FontWeight = FontWeights.SemiBold,
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static Button WizardSecondaryButton(string text) => new()
    {
        Content = text,
        MinWidth = 118d,
        Height = 38d,
        Padding = new Thickness(14d, 7d, 14d, 7d),
        Background = WizardBrush(8, 20, 29),
        Foreground = WizardBrush(215, 228, 236),
        BorderBrush = WizardBrush(39, 62, 77),
        BorderThickness = new Thickness(1d),
        FontWeight = FontWeights.SemiBold,
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static Button WizardLinkButton(string text) => new()
    {
        Content = text,
        Height = 38d,
        Padding = new Thickness(10d, 7d, 10d, 7d),
        Margin = new Thickness(8d, 0d, 0d, 0d),
        Background = Brushes.Transparent,
        Foreground = WizardBrush(111, 181, 229),
        BorderThickness = new Thickness(0d),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static T? FindWizardAncestor<T>(DependencyObject? element)
        where T : DependencyObject
    {
        var current = element;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static SolidColorBrush WizardBrush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));

    private static string WizardText(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
}
