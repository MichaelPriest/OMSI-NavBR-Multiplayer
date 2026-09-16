[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginDirectory,

    [string]$OutputPath = "src/NavBR.Client/Assets/NavBR.OmsiPlugin.bundle.zip"
)

$ErrorActionPreference = 'Stop'

$pluginRoot = (Resolve-Path -LiteralPath $PluginDirectory).Path
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $outputFullPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

# Native AOT removes the managed runtime dependency, but the OMSI plugin still
# calls the native NavBR ABI bridge. The embedded payload must therefore contain
# both x86 DLLs plus the OMSI .opl descriptor.
$required = @(
    'NavBR.OmsiPlugin.dll',
    'NavBR.OmsiInterop.dll',
    'NavBR.OmsiPlugin.opl'
)

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("NavBR-PluginPayload-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force | Out-Null

try {
    foreach ($name in $required) {
        $source = Join-Path $pluginRoot $name
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Embedded Native AOT plugin payload is incomplete: $source"
        }

        Copy-Item -LiteralPath $source -Destination (Join-Path $temp $name) -Force
    }

    if (Test-Path -LiteralPath $outputFullPath) {
        Remove-Item -LiteralPath $outputFullPath -Force
    }

    Compress-Archive -Path (Join-Path $temp '*') -DestinationPath $outputFullPath -CompressionLevel Optimal

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($outputFullPath)
    try {
        $entryNames = @($archive.Entries | ForEach-Object { $_.Name })
        foreach ($name in $required) {
            if ($entryNames -notcontains $name) {
                throw "Embedded plugin bundle verification failed: missing $name"
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    $hash = (Get-FileHash -LiteralPath $outputFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $size = (Get-Item -LiteralPath $outputFullPath).Length

    Write-Host "Embedded Native AOT plugin payload ready: $outputFullPath"
    Write-Host "Files: $($required.Count)  Bytes: $size  SHA256: $hash"
}
finally {
    if (Test-Path -LiteralPath $temp) {
        Remove-Item -LiteralPath $temp -Recurse -Force
    }
}
