$ErrorActionPreference = "Stop"

function Require-Text {
    param(
        [string]$Path,
        [string[]]$Patterns
    )

    if (-not (Test-Path $Path)) {
        throw "Required UI source not found: $Path"
    }

    $content = Get-Content $Path -Raw
    foreach ($pattern in $Patterns) {
        if (-not $content.Contains($pattern)) {
            throw "Missing required user surface '$pattern' in $Path"
        }
    }
}

function Reject-Text {
    param(
        [string]$Path,
        [string[]]$Patterns
    )

    if (-not (Test-Path $Path)) {
        throw "Required UI source not found: $Path"
    }

    $content = Get-Content $Path -Raw
    foreach ($pattern in $Patterns) {
        if ($content.Contains($pattern)) {
            throw "Retired legacy user surface '$pattern' must not return in $Path"
        }
    }
}

Require-Text "src/NavBR.Client/Windows/Alpha12FigmaShellInstaller.cs" @(
    "window.OpenNavigation3D",
    "window.LaunchOmsiForShell",
    "alpha14-hud-move",
    "alpha14-topbar-hud-move",
    "alpha14-hud-editor",
    "alpha14-connectivity",
    "alpha14-nat",
    "alpha14-external-port",
    "FeedbackWindow",
    "Alpha12ProfessionalShellInstaller.OperationsPanelName",
    "Alpha12ProfessionalShellInstaller.ToolsPanelName"
)

Require-Text "src/NavBR.Client/App.xaml.cs" @(
    "OmsiProfilesUiInstaller.Install(mainWindow)",
    "Alpha12ExperienceInstaller.Install(mainWindow)",
    "DispatcherInstaller.Install(mainWindow)",
    "VirtualCompanyInstaller.Install(mainWindow)",
    "CompanyNetworkInstaller.Install(mainWindow)",
    "CompanyMembersInstaller.Install(mainWindow)",
    "DriverProfileInstaller.Install(mainWindow)",
    "SessionHealthInstaller.Install(mainWindow)"
)

Require-Text "src/NavBR.Client/Multiplayer/MultiplayerWindow.xaml" @(
    'x:Name="PublicRoomsButton"',
    'x:Name="RelayEnabledCheckBox"',
    'x:Name="RoleplayTab"',
    'x:Name="PlayersTab"'
)

Require-Text "src/NavBR.Client/Multiplayer/MultiplayerWindow.xaml.cs" @(
    "InitializePublicRoomBrowser();",
    "InitializeVoiceChannels();",
    "InitializeRelayUi();",
    "InitializeRoleplayCharacterSelector();"
)

Require-Text "src/NavBR.Client/App.xaml" @(
    'x:Key="NavComboBoxStyle"',
    'x:Key="NavComboBoxItemStyle"',
    '<ControlTemplate TargetType="{x:Type ComboBox}">'
)

Require-Text "src/NavBR.Client/App.xaml.cs" @(
    "NavBRControlThemeInstaller.Attach(window);"
)

Require-Text "src/NavBR.Client/MainWindow.xaml" @(
    'ShowInTaskbar="False"',
    'Opacity="0"',
    'Left="-32000"',
    'Top="-32000"'
)

Require-Text "src/NavBR.Client/MainWindow.OmsiLaunch.cs" @(
    'NavigatePrimaryWebShell("settings-installations")'
)

Reject-Text "src/NavBR.Client/MainWindow.WebShell.cs" @(
    '"showLegacyShell"',
    '"openOmsiProfiles"',
    "ShowLegacyShellForWeb"
)

Reject-Text "ui/navbr-web/src/App.tsx" @(
    'sendCommand("showLegacyShell")',
    "Interface WPF"
)

Reject-Text "ui/navbr-web/src/navbrBridge.ts" @(
    '"showLegacyShell"',
    '"openOmsiProfiles"'
)

Require-Text "src/NavBR.Client/Multiplayer/WindowsFirewallService.cs" @(
    "-Profile Any",
    "EnsureInboundRuleDetailedAsync",
    "BuildEncodedPowerShellArguments"
)

Require-Text "src/NavBR.Client/MainWindow.OmsiLaunch.cs" @(
    "OmsiInstallationProfileStore.DiscoverAndMerge",
    "OmsiLauncherService.Launch"
)

Require-Text "src/NavBR.MultiplayerSimulator/Program.cs" @(
    "LocalServerBootstrap.EnsureAsync",
    "AutoStartLocalServer",
    "NavBR.Server.exe"
)

foreach ($legacy in @(
    "src/NavBR.Client/Multiplayer/MultiplayerWindow.RoomWizard.cs",
    "src/NavBR.Client/Multiplayer/MultiplayerWindow.RelayWizardPolish.cs"
)) {
    if (Test-Path $legacy) {
        throw "Legacy multiplayer overlay must stay removed: $legacy"
    }
}

Write-Host "Alpha.14 user-visible surfaces validated."
