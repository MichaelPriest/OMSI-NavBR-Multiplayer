using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavBR.Client.WinUI;

public sealed partial class CompanyPage : UserControl
{
    public Func<string, object?, Task>? CommandHandler { get; set; }

    private readonly ObservableCollection<CompanyMemberRow> _members = new();

    public CompanyPage()
    {
        InitializeComponent();
        MembersList.ItemsSource = _members;
    }

    public void ApplyState(JsonElement state)
    {
        var companyNetwork = JsonState.Property(state, "companyNetwork");
        var company = JsonState.Property(companyNetwork, "company");
        var membership = JsonState.Property(companyNetwork, "membership");
        var node = JsonState.Property(companyNetwork, "node");

        var companyName =
            JsonState.String(company, "name") ??
            JsonState.String(membership, "companyName");
        CompanyNameText.Text = companyName ?? "Nenhuma empresa";
        CompanyMetaText.Text = JsonState.IsObject(company)
            ? $"{JsonState.String(company, "shortName") ?? "—"} · {JsonState.Int(company, "memberCount") ?? 0} funcionários"
            : "Empresa Online ainda não carregada";

        var badge = JsonState.Property(company, "selfBadge");
        if (!JsonState.IsObject(badge))
        {
            badge = JsonState.Property(membership, "badge");
        }

        var number = JsonState.String(badge, "employeeNumber");
        BadgeNumberText.Text = number is null ? "—" : $"#{number}";
        BadgeRoleText.Text = number is null
            ? "Sem credencial"
            : $"{JsonState.String(badge, "companyShortName") ?? "EMPRESA"} · {JsonState.String(badge, "role") ?? "membro"}";

        var nodeRunning = JsonState.Bool(node, "running");
        NodeStateText.Text = nodeRunning ? "ONLINE" : "OFFLINE";
        NodeAddressText.Text = JsonState.String(node, "localUrl") ?? "—";
        StartNodeButton.IsEnabled = !nodeRunning;
        StopNodeButton.IsEnabled = nodeRunning;

        _members.Clear();
        foreach (var member in JsonState.Array(company, "members"))
        {
            _members.Add(new CompanyMemberRow(
                JsonState.String(member, "playerId") ?? string.Empty,
                JsonState.String(member, "employeeNumber") ?? "—",
                JsonState.String(member, "displayName") ?? "Sem nome",
                JsonState.String(member, "role") ?? "Driver",
                JsonState.String(member, "permissions") ?? "—",
                JsonState.Bool(member, "isSelf"),
                JsonState.Bool(member, "canChangeRole"),
                JsonState.Bool(member, "canRemove")));
        }

        MemberCountText.Text = $"{_members.Count} funcionário{(_members.Count == 1 ? string.Empty : "s")}";

        var roles = JsonState.Array(companyNetwork, "assignableRoles")
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToArray();

        RoleComboBox.ItemsSource = roles;
        InviteRoleComboBox.ItemsSource = roles.Length > 0
            ? roles
            : new[] { "Driver" };
        if (RoleComboBox.SelectedIndex < 0 && roles.Length > 0)
        {
            RoleComboBox.SelectedIndex = 0;
        }
        if (InviteRoleComboBox.SelectedIndex < 0)
        {
            InviteRoleComboBox.SelectedIndex = 0;
        }

        var invite = JsonState.Property(companyNetwork, "invite");
        InvitePayloadTextBox.Text =
            JsonState.String(invite, "payload") ??
            JsonState.String(invite, "code") ??
            string.Empty;

        var canInvite = JsonState.Bool(company, "canInvite");
        CreateInviteButton.IsEnabled = canInvite && nodeRunning;
        ChangeRoleButton.IsEnabled = JsonState.Bool(company, "canManageRoles");
        RemoveMemberButton.IsEnabled = JsonState.Bool(company, "canRemoveMembers");

        SubtitleText.Text = companyName is null
            ? "Crie ou entre em uma Empresa Online para ativar crachá, equipe e CCO."
            : $"Empresa Online ativa · identidade {JsonState.String(JsonState.Property(companyNetwork, "identity"), "displayName") ?? "NavBR"}";
    }

    private async Task RunAsync(string command, object? payload = null)
    {
        try
        {
            if (CommandHandler is null)
            {
                return;
            }

            await CommandHandler(command, payload);
            ShowNotice("Operação concluída.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowNotice(ex.Message, InfoBarSeverity.Error);
        }
    }

    private void ShowNotice(string message, InfoBarSeverity severity)
    {
        NoticeBar.Message = message;
        NoticeBar.Severity = severity;
        NoticeBar.IsOpen = true;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("refreshCompanyNetwork");

    private async void StartNode_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("startCompanyNode");

    private async void StopNode_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("stopCompanyNode");

    private async void CreateInvite_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "createCompanyInvite",
            new { role = InviteRoleComboBox.SelectedItem?.ToString() ?? "Driver" });

    private async void JoinCompany_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "joinCompany",
            new
            {
                nodeUrl = JoinNodeUrlTextBox.Text.Trim(),
                inviteCode = JoinInviteCodeTextBox.Text.Trim()
            });

    private async void ChangeRole_Click(object sender, RoutedEventArgs e)
    {
        if (MembersList.SelectedItem is not CompanyMemberRow member ||
            member.IsSelf ||
            !member.CanChangeRole)
        {
            ShowNotice(
                "Selecione um funcionário que você tenha permissão para alterar.",
                InfoBarSeverity.Warning);
            return;
        }

        await RunAsync(
            "changeCompanyMemberRole",
            new
            {
                playerId = member.PlayerId,
                role = RoleComboBox.SelectedItem?.ToString() ?? "Driver"
            });
    }

    private async void RemoveMember_Click(object sender, RoutedEventArgs e)
    {
        if (MembersList.SelectedItem is not CompanyMemberRow member ||
            member.IsSelf ||
            !member.CanRemove)
        {
            ShowNotice(
                "Selecione um funcionário que você tenha permissão para remover.",
                InfoBarSeverity.Warning);
            return;
        }

        await RunAsync(
            "removeCompanyMember",
            new { playerId = member.PlayerId });
    }
}

public sealed record CompanyMemberRow(
    string PlayerId,
    string EmployeeNumber,
    string DisplayName,
    string Role,
    string Permissions,
    bool IsSelf,
    bool CanChangeRole,
    bool CanRemove);
