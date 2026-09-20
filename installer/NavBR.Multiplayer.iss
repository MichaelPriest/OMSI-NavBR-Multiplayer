#define MyAppName "OMSI NavBR Multiplayer"
#define MyAppPublisher "NavBR"
#define MyAppExeName "OMSI.NavBR.Multiplayer.exe"

#ifndef MyAppVersion
  #define MyAppVersion "0.3.0-alpha.16"
#endif
#ifndef SourceRoot
  #error SourceRoot must be provided by the build.
#endif
#ifndef SimulatorRoot
  #error SimulatorRoot must be provided by the build.
#endif
#ifndef PluginRoot
  #error PluginRoot must be provided by the build.
#endif
#ifndef SetupIcon
  #error SetupIcon must be provided by the build.
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif

[Setup]
AppId={{7A35F7B3-2BCF-46CE-93D7-69F850D9B934}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion=0.3.0.0
VersionInfoProductName={#MyAppName}
VersionInfoDescription=NavBR multiplayer client and OMSI integration test package
DefaultDirName={localappdata}\Programs\OMSI NavBR Multiplayer
DefaultGroupName=OMSI NavBR Multiplayer
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=OMSI-NavBR-Multiplayer-{#MyAppVersion}-Setup-win-x86
SetupIconFile={#SetupIcon}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked
Name: "simulatorshortcut"; Description: "Criar atalho para o simulador multiplayer"; GroupDescription: "Ferramentas de teste:"; Flags: unchecked

[Files]
Source: "{#SourceRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SimulatorRoot}\*"; DestDir: "{app}\Simulator"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PluginRoot}\*"; DestDir: "{app}\Plugin"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\OMSI NavBR Multiplayer"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Simulador Multiplayer (6 players)"; Filename: "{app}\Simulator\run-online.cmd"; WorkingDir: "{app}\Simulator"
Name: "{group}\Desinstalar OMSI NavBR Multiplayer"; Filename: "{uninstallexe}"
Name: "{autodesktop}\OMSI NavBR Multiplayer"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{autodesktop}\NavBR Simulador Multiplayer"; Filename: "{app}\Simulator\run-online.cmd"; WorkingDir: "{app}\Simulator"; Tasks: simulatorshortcut

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir OMSI NavBR Multiplayer"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Simulator"
Type: filesandordirs; Name: "{app}\Plugin"
