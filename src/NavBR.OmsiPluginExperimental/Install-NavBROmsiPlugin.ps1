[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OmsiRoot
)

$ErrorActionPreference = 'Stop'
$RequiredRuntimeMajor = 10

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

        if ($reader.ReadUInt16() -ne 0x5A4D) {
            return $false
        }

        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -lt 0 -or $peOffset -gt ($stream.Length - 6)) {
            return $false
        }

        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) {
            return $false
        }

        # IMAGE_FILE_MACHINE_I386
        return $reader.ReadUInt16() -eq 0x014C
    }
    catch {
        return $false
    }
    finally {
        if ($reader) {
            $reader.Dispose()
        }
        elseif ($stream) {
            $stream.Dispose()
        }
    }
}

function Add-UniqueRuntimeRoot(
    [System.Collections.Generic.List[string]]$roots,
    [System.Collections.Generic.HashSet[string]]$seen,
    [string]$candidate) {

    if ([string]::IsNullOrWhiteSpace($candidate)) {
        return
    }

    try {
        $normalized = [System.IO.Path]::GetFullPath($candidate.Trim())
        if ($seen.Add($normalized)) {
            $roots.Add($normalized)
        }
    }
    catch {
        # Candidato inválido: segue procurando outros locais oficiais.
    }
}

function Get-DotNetX86CandidateRoots {
    $roots = [System.Collections.Generic.List[string]]::new()
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    Add-UniqueRuntimeRoot $roots $seen ([Environment]::GetEnvironmentVariable('DOTNET_ROOT_X86'))
    Add-UniqueRuntimeRoot $roots $seen ([Environment]::GetEnvironmentVariable('DOTNET_ROOT(x86)'))

    foreach ($registryPath in @(
        'HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x86',
        'HKLM:\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x86'
    )) {
        try {
            $installLocation = (Get-ItemProperty -LiteralPath $registryPath -ErrorAction Stop).InstallLocation
            Add-UniqueRuntimeRoot $roots $seen $installLocation
        }
        catch {
            # O runtime pode estar registrado em outro local ou não estar instalado.
        }
    }

    $programFilesX86 = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        Add-UniqueRuntimeRoot $roots $seen (Join-Path $programFilesX86 'dotnet')
    }

    if (-not [Environment]::Is64BitOperatingSystem) {
        $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
        if (-not [string]::IsNullOrWhiteSpace($programFiles)) {
            Add-UniqueRuntimeRoot $roots $seen (Join-Path $programFiles 'dotnet')
        }
    }

    return $roots
}

function Get-LatestMajorVersionDirectory([string]$parent, [int]$major) {
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        return $null
    }

    $matches = foreach ($directory in Get-ChildItem -LiteralPath $parent -Directory -ErrorAction SilentlyContinue) {
        try {
            $version = [Version]$directory.Name
            if ($version.Major -eq $major) {
                [pscustomobject]@{
                    Directory = $directory
                    Version = $version
                }
            }
        }
        catch {
            # Ignora pastas que não representam uma versão de runtime.
        }
    }

    return $matches |
        Sort-Object Version -Descending |
        Select-Object -First 1
}

function Find-DotNet10X86Runtime {
    foreach ($root in Get-DotNetX86CandidateRoots) {
        $dotnetExe = Join-Path $root 'dotnet.exe'
        if (-not (Test-X86PortableExecutable $dotnetExe)) {
            continue
        }

        $runtime = Get-LatestMajorVersionDirectory \
            (Join-Path $root 'shared\Microsoft.NETCore.App') \
            $RequiredRuntimeMajor
        if ($null -eq $runtime) {
            continue
        }

        $hostFxr = Get-LatestMajorVersionDirectory \
            (Join-Path $root 'host\fxr') \
            $RequiredRuntimeMajor
        if ($null -eq $hostFxr) {
            continue
        }

        return [pscustomobject]@{
            Root = $root
            RuntimeVersion = $runtime.Version.ToString()
            HostFxrVersion = $hostFxr.Version.ToString()
        }
    }

    return $null
}

function Assert-DotNet10X86Runtime {
    $runtime = Find-DotNet10X86Runtime
    if ($null -eq $runtime) {
        throw @"
Microsoft .NET 10 Runtime x86 não foi encontrado.

O OMSI 2 é 32-bit e o plugin NavBR experimental também é x86. Um runtime .NET x64 não substitui o runtime x86 para esta DLL.

Instale o Microsoft .NET 10 Runtime x86 e execute o instalador novamente:
https://dotnet.microsoft.com/download/dotnet/10.0

Nenhum arquivo do plugin foi copiado para o OMSI.
"@
    }

    Write-Host "Runtime .NET x86 OK: Microsoft.NETCore.App $($runtime.RuntimeVersion)" -ForegroundColor Green
    Write-Host "hostfxr x86: $($runtime.HostFxrVersion)"
    Write-Host "dotnet x86: $($runtime.Root)"
    return $runtime
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
$runtimeConfig = Join-Path $sourceRoot 'NavBR.OmsiPluginExperimental.runtimeconfig.json'
if (-not (Test-Path -LiteralPath $nativeDll -PathType Leaf)) {
    throw "Pacote inválido: NavBR.OmsiPlugin.dll não encontrado em $sourceRoot"
}
if (-not (Test-Path -LiteralPath $opl -PathType Leaf)) {
    throw "Pacote inválido: NavBR.OmsiPlugin.opl não encontrado em $sourceRoot"
}
if (-not (Test-Path -LiteralPath $runtimeConfig -PathType Leaf)) {
    throw "Pacote inválido: NavBR.OmsiPluginExperimental.runtimeconfig.json não encontrado em $sourceRoot"
}

# Preflight obrigatório antes de tocar na pasta de plugins do OMSI.
$dotnetX86 = Assert-DotNet10X86Runtime

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
    "# Runtime x86: Microsoft.NETCore.App $($dotnetX86.RuntimeVersion)"
    "# dotnet x86: $($dotnetX86.Root)"
    $installed
) | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host ''
Write-Host 'Plugin experimental NavBR instalado.' -ForegroundColor Green
Write-Host "OMSI: $root"
Write-Host "Destino: $pluginsRoot"
Write-Host "Arquivos: $($installed.Count)"
Write-Host "Runtime x86: Microsoft.NETCore.App $($dotnetX86.RuntimeVersion)"
Write-Host ''
Write-Host 'Após iniciar o OMSI, confira o painel PLUGIN BRIDGE v1 • EXP e o log:'
Write-Host '%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-plugin.log'
