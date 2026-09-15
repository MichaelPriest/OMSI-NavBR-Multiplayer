[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OmsiRoot
)

$ErrorActionPreference = 'Stop'

function Assert-OmsiClosed {
    $running = Get-Process -Name 'Omsi' -ErrorAction SilentlyContinue
    if ($running) {
        throw 'Feche o OMSI antes de instalar ou atualizar o plugin NavBR.'
    }
}

$root = [System.IO.Path]::GetFullPath($OmsiRoot)
$omsiExe = Join-Path $root 'Omsi.exe'
if (-not (Test-Path -LiteralPath $omsiExe -PathType Leaf)) {
    throw "Omsi.exe não foi encontrado em: $root"
}

Assert-OmsiClosed

$sourceRoot = $PSScriptRoot
$nativeDll = Join-Path $sourceRoot 'NavBR.OmsiPlugin.dll'
$opl = Join-Path $sourceRoot 'NavBR.OmsiPlugin.opl'
if (-not (Test-Path -LiteralPath $nativeDll -PathType Leaf)) {
    throw "Pacote inválido: NavBR.OmsiPlugin.dll não encontrado em $sourceRoot"
}
if (-not (Test-Path -LiteralPath $opl -PathType Leaf)) {
    throw "Pacote inválido: NavBR.OmsiPlugin.opl não encontrado em $sourceRoot"
}

$pluginsRoot = Join-Path $root 'plugins'
New-Item -ItemType Directory -Path $pluginsRoot -Force | Out-Null

$allowedExtensions = @('.dll', '.json', '.opl')
$files = Get-ChildItem -LiteralPath $sourceRoot -File | Where-Object {
    $allowedExtensions -contains $_.Extension.ToLowerInvariant()
}

if (-not $files) {
    throw 'Nenhum arquivo instalável foi encontrado no pacote experimental.'
}

$manifestPath = Join-Path $pluginsRoot 'NavBR.OmsiPlugin.install-manifest.txt'
$installed = New-Object System.Collections.Generic.List[string]

foreach ($file in $files) {
    $destination = Join-Path $pluginsRoot $file.Name
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    $installed.Add($file.Name)
}

@(
    '# OMSI NavBR Plugin experimental - arquivos instalados'
    "# Instalado em: $(Get-Date -Format o)"
    $installed
) | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host ''
Write-Host 'Plugin experimental NavBR instalado.' -ForegroundColor Green
Write-Host "OMSI: $root"
Write-Host "Destino: $pluginsRoot"
Write-Host "Arquivos: $($installed.Count)"
Write-Host ''
Write-Host 'Após iniciar o OMSI, confira o log:'
Write-Host '%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-plugin.log'
