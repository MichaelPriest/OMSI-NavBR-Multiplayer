param(
    [Parameter(Mandatory = $true)]
    [string]$OpenOmsiRoot
)

$ErrorActionPreference = "Stop"

$root = [System.IO.Path]::GetFullPath($OpenOmsiRoot)
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "Content root do openOMSI não encontrado: $root"
}

$marker = Join-Path $root ".openomsi-content"
if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
    Write-Warning "O marcador .openomsi-content não foi encontrado. Confirme que este é o content root do openOMSI."
}

$source = Split-Path -Parent $MyInvocation.MyCommand.Path
$pluginsRoot = Join-Path $root "Plugins"
$target = Join-Path $pluginsRoot "NavBR.OpenOmsi"
$manifest = Join-Path $target "NavBR.OpenOmsiPlugin.install-manifest.txt"
$files = @(
    "NavBR.OpenOmsiPlugin.dll",
    "NavBR.OpenOmsiPlugin.opl",
    "main.lua"
)

foreach ($file in $files) {
    $sourceFile = Join-Path $source $file
    if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) {
        throw "Pacote NavBR OpenOMSI incompleto: $file"
    }
}

if ((Test-Path -LiteralPath $target) -and -not (Test-Path -LiteralPath $manifest)) {
    $unknown = @(Get-ChildItem -LiteralPath $target -Force -ErrorAction SilentlyContinue)
    if ($unknown.Count -gt 0) {
        throw "A pasta '$target' já contém arquivos não rastreados. A instalação foi interrompida para não sobrescrever conteúdo de terceiros."
    }
}

New-Item -ItemType Directory -Path $target -Force | Out-Null

foreach ($file in $files) {
    Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $target $file) -Force
}

@(
    "# NavBR for openOMSI - arquivos instalados",
    "# Instalado em: $([DateTimeOffset]::Now.ToString('O'))",
    "# Deployment: Native AOT win-x64 + standard OMSI .opl ABI",
    "NavBR.OpenOmsiPlugin.dll",
    "NavBR.OpenOmsiPlugin.opl",
    "main.lua"
) | Set-Content -LiteralPath $manifest -Encoding UTF8

$configDir = Join-Path $env:LOCALAPPDATA "NavBR"
$configFile = Join-Path $configDir "openomsi-content-root.txt"
New-Item -ItemType Directory -Path $configDir -Force | Out-Null
Set-Content -LiteralPath $configFile -Value $root -Encoding UTF8

Write-Host "NavBR for openOMSI instalado em: $target"
Write-Host "Content root registrado em: $configFile"
Write-Host "Inicie/reinicie o openOMSI para carregar o plugin."
