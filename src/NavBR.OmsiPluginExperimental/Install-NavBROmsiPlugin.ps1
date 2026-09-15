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

function Read-TrackedFiles([string]$manifestPath) {
    $tracked = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        return $tracked
    }

    foreach ($line in Get-Content -LiteralPath $manifestPath) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) {
            continue
        }

        $fileName = [System.IO.Path]::GetFileName($line.Trim())
        if (-not [string]::IsNullOrWhiteSpace($fileName)) {
            $null = $tracked.Add($fileName)
        }
    }

    return $tracked
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
$files = @(Get-ChildItem -LiteralPath $sourceRoot -File | Where-Object {
    $allowedExtensions -contains $_.Extension.ToLowerInvariant()
})

if ($files.Count -eq 0) {
    throw 'Nenhum arquivo instalável foi encontrado no pacote experimental.'
}

$manifestPath = Join-Path $pluginsRoot 'NavBR.OmsiPlugin.install-manifest.txt'
$previouslyTracked = Read-TrackedFiles $manifestPath
$newNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($file in $files) {
    $null = $newNames.Add($file.Name)
}

# Preflight: não sobrescreve arquivos que já existiam antes do NavBR e não
# aparecem no manifesto da instalação anterior.
foreach ($file in $files) {
    $destination = Join-Path $pluginsRoot $file.Name
    if ((Test-Path -LiteralPath $destination -PathType Leaf) -and
        -not $previouslyTracked.Contains($file.Name)) {
        throw "Instalação interrompida: já existe um arquivo não rastreado em '$destination'. Nenhum arquivo foi sobrescrito."
    }
}

# Remove somente sobras de uma instalação NavBR anterior que não fazem mais
# parte do pacote atual.
foreach ($oldName in $previouslyTracked) {
    if ($newNames.Contains($oldName)) {
        continue
    }

    $obsolete = Join-Path $pluginsRoot $oldName
    if (Test-Path -LiteralPath $obsolete -PathType Leaf) {
        Remove-Item -LiteralPath $obsolete -Force
    }
}

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
