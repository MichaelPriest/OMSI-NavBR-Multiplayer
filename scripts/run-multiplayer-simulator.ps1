param(
    [string]$Server = "http://127.0.0.1:27730",
    [string]$Room = "navbr-sim",
    [ValidateRange(1, 32)]
    [int]$Players = 6,
    [ValidateSet("vehicles", "rp", "mixed")]
    [string]$Mode = "mixed",
    [string]$Map = "",
    [string]$MapId = "",
    [double]$X = 0,
    [double]$Y = 0,
    [double]$Z = 0,
    [double]$Radius = 90,
    [int]$Duration = 0,
    [switch]$Verify
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\src\NavBR.MultiplayerSimulator\NavBR.MultiplayerSimulator.csproj"

$argsList = @(
    "run",
    "--project", $project,
    "-c", "Release",
    "--",
    "--server", $Server,
    "--room", $Room,
    "--players", "$Players",
    "--mode", $Mode,
    "--x", $X.ToString([Globalization.CultureInfo]::InvariantCulture),
    "--y", $Y.ToString([Globalization.CultureInfo]::InvariantCulture),
    "--z", $Z.ToString([Globalization.CultureInfo]::InvariantCulture),
    "--radius", $Radius.ToString([Globalization.CultureInfo]::InvariantCulture),
    "--duration", "$Duration"
)

if (-not [string]::IsNullOrWhiteSpace($Map)) {
    $argsList += @("--map", $Map)
}
if (-not [string]::IsNullOrWhiteSpace($MapId)) {
    $argsList += @("--map-id", $MapId)
}
if ($Verify) {
    $argsList += "--verify"
}

Write-Host "NavBR Multiplayer Simulator"
Write-Host "Server : $Server"
Write-Host "Room   : $Room"
Write-Host "Players: $Players"
Write-Host "Mode   : $Mode"
Write-Host ""
Write-Host "Os jogadores simulados usam clientes SignalR reais e aparecem na Central Multiplayer."
Write-Host "Ctrl+C encerra o simulador."

& dotnet @argsList
exit $LASTEXITCODE
