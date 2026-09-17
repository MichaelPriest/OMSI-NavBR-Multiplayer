using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;
using NavBR.Client.Windows;
using NavBR.Shared.Network;

namespace NavBR.Client.Network;

internal static class CompanyMembersInstaller
{
    private const string ButtonTag = "alpha12-company-members";
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
                new CompanyMembersWindow(window, app.NetworkRuntime).ShowDialog();
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
            "pt" => "♙  Equipe da empresa",
            "es" => "♙  Equipo de la empresa",
            "de" => "♙  Unternehmensteam",
            "fr" => "♙  Équipe de l’entreprise",
            _ => "♙  Company team"
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

internal sealed class CompanyMembersWindow : Window
{
    private readonly NavBRNetworkRuntime _runtime;
    private readonly ListBox _members = new();
    private readonly ComboBox _roles = new();
    private readonly TextBlock _company = new();
    private readonly TextBlock _detail = new();
    private readonly TextBlock _status = new();
    private readonly Button _applyRole;
    private readonly Button _remove;
    private CompanyNodeSnapshot? _snapshot;
    private CompanyMemberRecord? _self;

    public CompanyMembersWindow(Window owner, NavBRNetworkRuntime runtime)
    {
        Owner = owner;
        _runtime = runtime;
        Title = T("Equipe da Empresa — NavBR", "Company Team — NavBR", "Equipo de la empresa — NavBR", "Unternehmensteam — NavBR", "Équipe de l’entreprise — NavBR");
        Width = 1020d;
        Height = 720d;
        MinWidth = 860d;
        MinHeight = 600d;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(6, 12, 18);

        foreach (var role in Enum.GetValues<CompanyRole>())
        {
            if (role != CompanyRole.President)
            {
                _roles.Items.Add(new RoleChoice(role, RoleText(role)));
            }
        }
        _roles.DisplayMemberPath = nameof(RoleChoice.Label);
        _roles.SelectedValuePath = nameof(RoleChoice.Role);

        _applyRole = Button(T("Aplicar cargo", "Apply role", "Aplicar cargo", "Rolle anwenden", "Appliquer le rôle"), ApplyRole_Click);
        _remove = Button(T("Remover membro", "Remove member", "Eliminar miembro", "Mitglied entfernen", "Retirer le membre"), Remove_Click, danger: true);

        Content = BuildContent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(26d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        var header = new Grid { Margin = new Thickness(0d, 0d, 0d, 18d) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = T("Equipe da Empresa", "Company Team", "Equipo de la empresa", "Unternehmensteam", "Équipe de l’entreprise"),
            Foreground = Brushes.White,
            FontSize = 25d,
            FontWeight = FontWeights.SemiBold
        });
        _company.Foreground = Brush(148, 170, 184);
        _company.FontSize = 11.5d;
        _company.Margin = new Thickness(0d, 5d, 0d, 0d);
        heading.Children.Add(_company);
        header.Children.Add(heading);
        var refresh = Button(T("Atualizar", "Refresh", "Actualizar", "Aktualisieren", "Actualiser"), async (_, _) => await RefreshAsync());
        refresh.Margin = new Thickness(12d, 0d, 0d, 0d);
        Grid.SetColumn(refresh, 1);
        header.Children.Add(refresh);
        root.Children.Add(header);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.46d, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14d) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.54d, GridUnitType.Star) });
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        _members.Background = Brush(8, 16, 23);
        _members.Foreground = Brushes.White;
        _members.BorderBrush = Brush(30, 48, 60);
        _members.BorderThickness = new Thickness(1d);
        _members.Padding = new Thickness(5d);
        _members.SelectionChanged += (_, _) => RenderSelection();
        body.Children.Add(Card(T("Membros", "Members", "Miembros", "Mitglieder", "Membres"), _members));

        var right = new StackPanel();
        Grid.SetColumn(right, 2);
        body.Children.Add(right);

        _detail.Foreground = Brush(218, 230, 238);
        _detail.FontSize = 12d;
        _detail.TextWrapping = TextWrapping.Wrap;
        right.Children.Add(Card(T("Membro selecionado", "Selected member", "Miembro seleccionado", "Ausgewähltes Mitglied", "Membre sélectionné"), _detail));

        var admin = new StackPanel();
        admin.Children.Add(new TextBlock
        {
            Text = T(
                "A hierarquia é protegida pelo Company Node. Você só pode administrar cargos abaixo do seu próprio nível.",
                "Hierarchy is enforced by the Company Node. You may only manage roles below your own level.",
                "La jerarquía es aplicada por Company Node. Solo puedes administrar cargos inferiores al tuyo.",
                "Die Hierarchie wird vom Company Node erzwungen. Sie können nur niedrigere Rollen verwalten.",
                "La hiérarchie est imposée par Company Node. Vous ne pouvez gérer que les rôles inférieurs au vôtre."),
            Foreground = Brush(151, 171, 185),
            FontSize = 11d,
            TextWrapping = TextWrapping.Wrap
        });
        _roles.MinHeight = 36d;
        _roles.Margin = new Thickness(0d, 12d, 0d, 10d);
        _roles.Background = Brush(7, 14, 20);
        _roles.Foreground = Brushes.White;
        _roles.BorderBrush = Brush(42, 62, 76);
        admin.Children.Add(_roles);
        var actions = new WrapPanel();
        actions.Children.Add(_applyRole);
        actions.Children.Add(_remove);
        admin.Children.Add(actions);
        right.Children.Add(Card(T("Administração", "Administration", "Administración", "Verwaltung", "Administration"), admin));

        _status.Foreground = Brush(185, 202, 214);
        _status.FontSize = 11.5d;
        _status.TextWrapping = TextWrapping.Wrap;
        right.Children.Add(Card(T("Estado", "Status", "Estado", "Status", "État"), _status));
        return root;
    }

    private async Task RefreshAsync()
    {
        try
        {
            _status.Text = T("Atualizando quadro da empresa...", "Refreshing company roster...", "Actualizando equipo...", "Unternehmensteam wird aktualisiert...", "Actualisation de l’équipe...");
            _snapshot = await _runtime.GetCompanySnapshotAsync();
            _self = _snapshot.Members.FirstOrDefault(member =>
                string.Equals(member.PlayerId, _runtime.Identity.PlayerId, StringComparison.OrdinalIgnoreCase));
            _company.Text = $"{_snapshot.Name}  •  {_snapshot.ShortName}  •  {_snapshot.Members.Count} {T("membros", "members", "miembros", "Mitglieder", "membres")}";

            var previousId = (_members.SelectedItem as MemberChoice)?.Member.PlayerId;
            _members.Items.Clear();
            foreach (var member in _snapshot.Members.OrderBy(member => member.Role).ThenBy(member => member.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                _members.Items.Add(new MemberChoice(member, $"{RoleBadge(member.Role)}  {member.DisplayName}\n{RoleText(member.Role)}  •  {member.PlayerId}"));
            }
            _members.DisplayMemberPath = nameof(MemberChoice.Label);
            _members.SelectedItem = _members.Items.Cast<MemberChoice>().FirstOrDefault(item =>
                string.Equals(item.Member.PlayerId, previousId, StringComparison.OrdinalIgnoreCase))
                ?? _members.Items.Cast<MemberChoice>().FirstOrDefault();
            _status.Text = T("Quadro atualizado.", "Roster updated.", "Equipo actualizado.", "Team aktualisiert.", "Équipe actualisée.");
            RenderSelection();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _snapshot = null;
            _self = null;
            _members.Items.Clear();
            RenderSelection();
        }
    }

    private void RenderSelection()
    {
        var selected = (_members.SelectedItem as MemberChoice)?.Member;
        if (selected is null)
        {
            _detail.Text = T("Selecione um membro.", "Select a member.", "Selecciona un miembro.", "Wählen Sie ein Mitglied.", "Sélectionnez un membre.");
            _applyRole.IsEnabled = false;
            _remove.IsEnabled = false;
            return;
        }

        var isOwner = _snapshot is not null && string.Equals(_snapshot.OwnerPlayerId, selected.PlayerId, StringComparison.OrdinalIgnoreCase);
        _detail.Text = $"{selected.DisplayName}\n{RoleText(selected.Role)}{(isOwner ? "  •  OWNER" : string.Empty)}\n{selected.PlayerId}\n{T("Entrou", "Joined", "Ingreso", "Beigetreten", "Adhésion")}: {selected.JoinedAtUtc.ToLocalTime():g}";

        _roles.SelectedItem = _roles.Items.Cast<RoleChoice>().FirstOrDefault(item => item.Role == selected.Role)
                              ?? _roles.Items.Cast<RoleChoice>().LastOrDefault();

        var canManageRole = _self is not null &&
                            (_self.Permissions & CompanyPermission.ManageRoles) != 0 &&
                            !isOwner &&
                            _self.Role < selected.Role;
        var canRemove = _self is not null &&
                        (_self.Permissions & CompanyPermission.RemoveMembers) != 0 &&
                        !isOwner &&
                        _self.Role < selected.Role;
        _applyRole.IsEnabled = canManageRole;
        _remove.IsEnabled = canRemove;
    }

    private async void ApplyRole_Click(object sender, RoutedEventArgs e)
    {
        if ((_members.SelectedItem as MemberChoice)?.Member is not { } selected ||
            _roles.SelectedItem is not RoleChoice role)
        {
            return;
        }
        try
        {
            _status.Text = T("Aplicando cargo...", "Applying role...", "Aplicando cargo...", "Rolle wird angewendet...", "Application du rôle...");
            var result = await _runtime.ChangeCompanyMemberRoleAsync(selected.PlayerId, role.Role);
            _status.Text = result.Success
                ? T("Cargo atualizado pela empresa.", "Company role updated.", "Cargo actualizado.", "Unternehmensrolle aktualisiert.", "Rôle mis à jour.")
                : $"{T("Alteração recusada", "Change rejected", "Cambio rechazado", "Änderung abgelehnt", "Modification refusée")}: {result.Error}";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((_members.SelectedItem as MemberChoice)?.Member is not { } selected)
        {
            return;
        }
        var answer = MessageBox.Show(
            T($"Remover {selected.DisplayName} da empresa?", $"Remove {selected.DisplayName} from the company?", $"¿Eliminar a {selected.DisplayName} de la empresa?", $"{selected.DisplayName} aus dem Unternehmen entfernen?", $"Retirer {selected.DisplayName} de l’entreprise ?"),
            Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }
        try
        {
            _status.Text = T("Removendo membro...", "Removing member...", "Eliminando miembro...", "Mitglied wird entfernt...", "Retrait du membre...");
            var result = await _runtime.RemoveCompanyMemberAsync(selected.PlayerId);
            _status.Text = result.Success
                ? T("Membro removido.", "Member removed.", "Miembro eliminado.", "Mitglied entfernt.", "Membre retiré.")
                : $"{T("Remoção recusada", "Removal rejected", "Eliminación rechazada", "Entfernung abgelehnt", "Retrait refusé")}: {result.Error}";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

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

    private static Button Button(string text, RoutedEventHandler handler, bool danger = false)
    {
        var button = new Button
        {
            Content = text,
            MinHeight = 38d,
            Padding = new Thickness(14d, 8d, 14d, 8d),
            Margin = new Thickness(0d, 0d, 8d, 0d),
            Background = danger ? Brush(62, 23, 28) : Brush(17, 45, 67),
            Foreground = Brushes.White,
            BorderBrush = danger ? Brush(164, 62, 70) : Brush(61, 137, 196),
            BorderThickness = new Thickness(1d),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += handler;
        return button;
    }

    private static string RoleBadge(CompanyRole role) => role switch
    {
        CompanyRole.President => "◆",
        CompanyRole.VicePresident => "◇",
        CompanyRole.Director => "★",
        CompanyRole.OperationsManager => "▣",
        CompanyRole.Dispatcher => "◉",
        CompanyRole.Supervisor => "●",
        CompanyRole.SeniorDriver => "▸",
        CompanyRole.Driver => "›",
        _ => "·"
    };

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

    private sealed record MemberChoice(CompanyMemberRecord Member, string Label);
    private sealed record RoleChoice(CompanyRole Role, string Label);
}
