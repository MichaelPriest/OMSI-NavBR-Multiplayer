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

function Test-X86PortableExecutable([string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return $false
    }

    $stream = $null
    $reader = $null
    try {
        $stream = [System.IO.File]::Open(
            $path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::ReadWrite)
        $reader = [System.IO.BinaryReader]::new($stream)
        if ($reader.ReadUInt16() -ne 0x5A4D) { return $false }
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -lt 0 -or $peOffset -gt ($stream.Length - 6)) { return $false }
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) { return $false }
        return $reader.ReadUInt16() -eq 0x014C
    }
    catch {
        return $false
    }
    finally {
        if ($reader) { $reader.Dispose() }
        elseif ($stream) { $stream.Dispose() }
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

foreach ($required in @($nativeDll, $opl)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Pacote Native AOT inválido: arquivo não encontrado: $required"
    }
}

if (-not (Test-X86PortableExecutable $nativeDll)) {
    throw 'Pacote inválido: NavBR.OmsiPlugin.dll não é uma DLL x86 compatível com o OMSI 2.'
}

$pluginsRoot = Join-Path $root 'plugins'
New-Item -ItemType Directory -Path $pluginsRoot -Force | Out-Null

$files = @(
    Get-Item -LiteralPath $nativeDll,
    Get-Item -LiteralPath $opl
)

$manifestPath = Join-Path $pluginsRoot 'NavBR.OmsiPlugin.install-manifest.txt'
$previouslyTracked = Read-TrackedFiles $manifestPath
$newNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($file in $files) {
    $null = $newNames.Add($file.Name)
}

foreach ($file in $files) {
    $destination = Join-Path $pluginsRoot $file.Name
    if ((Test-Path -LiteralPath $destination -PathType Leaf) -and
        -not $previouslyTracked.Contains($file.Name)) {
        throw "Instalação interrompida: já existe um arquivo não rastreado em '$destination'. Nenhum arquivo foi sobrescrito."
    }
}

foreach ($oldName in $previouslyTracked) {
    if ($newNames.Contains($oldName)) { continue }
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
    '# Deployment: Native AOT x86 (self-contained; no .NET x86 runtime required)'
    $installed
) | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host ''
Write-Host 'Plugin experimental NavBR Native AOT instalado.' -ForegroundColor Green
Write-Host "OMSI: $root"
Write-Host "Destino: $pluginsRoot"
Write-Host "Arquivos: $($installed.Count)"
Write-Host 'Runtime .NET x86: não necessário (embutido no plugin Native AOT).'
Write-Host ''
Write-Host 'Após iniciar o OMSI, confira o painel PLUGIN BRIDGE v1 • EXP e o log:'
Write-Host '%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-plugin.log'
