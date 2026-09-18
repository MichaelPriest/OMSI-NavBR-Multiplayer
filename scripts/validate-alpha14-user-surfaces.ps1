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

Require-Text "src/NavBR.Client/Windows/Alpha12FigmaShellInstaller.cs" @(
    "window.OpenNavigation3D",
    "alpha14-hud-move",
    "alpha14-hud-editor",
    "alpha14-connectivity",
    "alpha14-nat",
    "alpha14-external-port",
    "FeedbackWindow",
    "Alpha12OperationsMenuPanel",
    "Alpha11ToolsPanel"
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
    '<ControlTemplate TargetType="{x:Type ComboBox}">',
    '<Style TargetType="{x:Type ComboBoxItem}">'
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
