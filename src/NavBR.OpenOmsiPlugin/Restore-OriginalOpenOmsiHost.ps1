param(
    [Parameter(Mandatory = $true)]
    [string]$OpenOmsiRoot
)

$ErrorActionPreference = "Stop"

$root = [System.IO.Path]::GetFullPath($OpenOmsiRoot)
$targetExe = Join-Path $root "openomsi.exe"
$backupExe = Join-Path $root "openomsi.navbr-original.exe"
$manifest = Join-Path $root ".navbr-openomsi-host-test"
$targetSteam = Join-Path $root "steam_api64.dll"

if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
    throw "Manifesto do host NavBR não encontrado. Nenhuma restauração foi feita."
}
if (-not (Test-Path -LiteralPath $backupExe -PathType Leaf)) {
    throw "Backup openomsi.navbr-original.exe não encontrado. Nenhuma restauração foi feita."
}

$manifestText = Get-Content -LiteralPath $manifest -Raw
$steamAdded = $manifestText -match '(?m)^steamAdded=True\s*$'

Copy-Item -LiteralPath $backupExe -Destination $targetExe -Force
Remove-Item -LiteralPath $backupExe -Force
if ($steamAdded -and (Test-Path -LiteralPath $targetSteam -PathType Leaf)) {
    Remove-Item -LiteralPath $targetSteam -Force
}
Remove-Item -LiteralPath $manifest -Force

Write-Host "openOMSI original restaurado: $targetExe"
