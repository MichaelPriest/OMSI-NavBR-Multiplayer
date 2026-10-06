param(
    [Parameter(Mandatory = $true)]
    [string]$OpenOmsiRoot
)

$ErrorActionPreference = "Stop"

$root = [System.IO.Path]::GetFullPath($OpenOmsiRoot)
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "Pasta do openOMSI não encontrada: $root"
}

$source = Split-Path -Parent $MyInvocation.MyCommand.Path
$patchedExe = Join-Path $source "openomsi.exe"
$targetExe = Join-Path $root "openomsi.exe"
$backupExe = Join-Path $root "openomsi.navbr-original.exe"
$manifest = Join-Path $root ".navbr-openomsi-host-test"
$sourceSteam = Join-Path $source "steam_api64.dll"
$targetSteam = Join-Path $root "steam_api64.dll"

if (-not (Test-Path -LiteralPath $patchedExe -PathType Leaf)) {
    throw "Pacote de host NavBR incompleto: openomsi.exe"
}

if (-not (Test-Path -LiteralPath $targetExe -PathType Leaf)) {
    throw "openomsi.exe original não encontrado em: $root"
}

if (-not (Test-Path -LiteralPath $backupExe -PathType Leaf)) {
    Copy-Item -LiteralPath $targetExe -Destination $backupExe -Force
    Write-Host "Backup criado: $backupExe"
} elseif (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
    throw "Já existe openomsi.navbr-original.exe sem manifesto NavBR. Instalação interrompida para não sobrescrever um backup desconhecido."
}

Copy-Item -LiteralPath $patchedExe -Destination $targetExe -Force

$steamAdded = $false
if ((Test-Path -LiteralPath $sourceSteam -PathType Leaf) -and -not (Test-Path -LiteralPath $targetSteam -PathType Leaf)) {
    Copy-Item -LiteralPath $sourceSteam -Destination $targetSteam -Force
    $steamAdded = $true
}

@(
    "NavBR openOMSI host test",
    "installed=$([DateTimeOffset]::Now.ToString('O'))",
    "backup=openomsi.navbr-original.exe",
    "steamAdded=$steamAdded"
) | Set-Content -LiteralPath $manifest -Encoding UTF8

Write-Host ""
Write-Host "Host NavBR instalado."
Write-Host "Abra normalmente: $targetExe"
Write-Host "No jogo: Ctrl+Alt+N abre/fecha o painel NavBR."
Write-Host "Para voltar ao openOMSI original, execute Restore-OriginalOpenOmsiHost.ps1."
