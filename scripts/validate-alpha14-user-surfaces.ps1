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

Reject-Text "src/NavBR.Client/App.xaml" @(
    'StartupUri="MainWindow.xaml"'
)

Require-Text "src/NavBR.Client/App.xaml.cs" @(
    "ShutdownMode = ShutdownMode.OnExplicitShutdown;",
    "var nativeHost = new MainWindow();",
    "MainWindow = nativeHost;",
    "nativeHost.InitializeRoleplayForShell();",
    "TrayIcon.Attach(nativeHost);",
    "nativeHost.StartNativeRuntimeForReact();",
    "nativeHost.OpenPrimaryWebShell();",
    "if (window is not MainWindow)"
)

Reject-Text "src/NavBR.Client/App.xaml.cs" @(
    "nativeHost.Show();"
)

Require-Text "src/NavBR.Client/MainWindow.xaml.cs" @(
    "private const bool RetiredWpfVisualsEnabled = false;",
    "DriverStatisticsService",
    "_driverStatisticsService.Start();",
    "_driverStatisticsService.Dispose();",
    "StartNativeRuntimeForReact",
    "if (!RetiredWpfVisualsEnabled)"
)

Reject-Text "src/NavBR.Client/MainWindow.xaml.cs" @(
    "Loaded += async (_, _) => await RefreshOmsiStatusAsync();"
)

Reject-Text "src/NavBR.Client/App.xaml.cs" @(
    "Alpha12FigmaShellInstaller.Install(mainWindow)",
    "Alpha11VisualTuning.Apply(mainWindow)",
    "OmsiProfilesUiInstaller.Install(mainWindow)",
    "Alpha12ExperienceInstaller.Install(mainWindow)",
    "DispatcherInstaller.Install(mainWindow)",
    "VirtualCompanyInstaller.Install(mainWindow)",
    "CompanyNetworkInstaller.Install(mainWindow)",
    "CompanyMembersInstaller.Install(mainWindow)",
    "DriverProfileInstaller.Install(mainWindow)",
    "SessionHealthInstaller.Install(mainWindow)",
    "Alpha12FigmaNavigationModeInstaller.Install(mainWindow)",
    "Alpha12FigmaLiveDataInstaller.Install(mainWindow)",
    "Alpha12FigmaResponsiveShellInstaller.Install(mainWindow)",
    "Alpha12FigmaSystemSurfaceInstaller.Install(mainWindow)"
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

Require-Text "src/NavBR.Client/MainWindow.WebShell.cs" @(
    "GetPrimaryWebDialogOwner"
)

Reject-Text "src/NavBR.Client/MainWindow.WebSystem.cs" @(
    "dialog.ShowDialog(this)"
)

Reject-Text "src/NavBR.Client/MainWindow.WebGhost.cs" @(
    "dialog.ShowDialog(this)"
)

Reject-Text "src/NavBR.Client/Windows/NavBRTrayIconService.cs" @(
    "_mainWindow.Show();"
)

Reject-Text "src/NavBR.Client/MainWindow.WebShell.cs" @(
    '"showLegacyShell"',
    '"openOmsiProfiles"',
    '"openMultiplayerCentral"',
    '"openHudEditor"',
    "ShowLegacyShellForWeb"
)

Require-Text "src/NavBR.Client/MainWindow.Multiplayer.cs" @(
    'NavigatePrimaryWebShell("multiplayer")',
    'NavigatePrimaryWebShell("roleplay")',
    "window.ShowInTaskbar = false;",
    "window.ShowActivated = false;",
    "window.Opacity = 0d;",
    "window.Hide();"
)

Reject-Text "src/NavBR.Client/MainWindow.Multiplayer.cs" @(
    "_multiplayerWindow.Show();",
    "_multiplayerWindow.Activate();",
    "_multiplayerWindow?.ShowRoleplayTab();"
)

Require-Text "src/NavBR.Client/Operations/DispatcherInstaller.cs" @(
    'NavigatePrimaryWebShell("operations")'
)

Reject-Text "src/NavBR.Client/Operations/DispatcherInstaller.cs" @(
    "new DispatcherWindow(",
    "dispatcher.ShowDialog();"
)

Reject-Text "src/NavBR.Client/WebUI/bootstrap/app.js" @(
    '"openMultiplayerCentral"',
    '"showLegacyShell"'
)

Reject-Text "src/NavBR.Client/WebUI/bootstrap/index.html" @(
    "A interface clássica continua aberta",
    'id="nativeMultiplayerButton"'
)

Reject-Text "ui/navbr-web/src/App.tsx" @(
    'sendCommand("showLegacyShell")',
    "Interface WPF"
)

Require-Text "ui/navbr-web/src/App.tsx" @(
    'sendCommand("configureMultiplayerHotkeys"',
    'sendCommand("configureRelay"',
    'sendCommand("submitOperationalReport"',
    'sendCommand("resolveMyOperationalReports"',
    "Copiar convite",
    "Colar convite"
)

Reject-Text "ui/navbr-web/src/navbrBridge.ts" @(
    '"showLegacyShell"',
    '"openOmsiProfiles"',
    '"openMultiplayerCentral"',
    '"openHudEditor"'
)

Require-Text "ui/navbr-web/src/navbrBridge.ts" @(
    '"configureMultiplayerHotkeys"',
    '"configureRelay"',
    '"submitOperationalReport"',
    '"resolveMyOperationalReports"'
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
