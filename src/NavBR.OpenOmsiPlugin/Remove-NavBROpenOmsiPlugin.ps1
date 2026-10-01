param(
    [Parameter(Mandatory = $true)]
    [string]$OpenOmsiRoot
)

$ErrorActionPreference = "Stop"

$root = [System.IO.Path]::GetFullPath($OpenOmsiRoot)
$target = Join-Path (Join-Path $root "plugins") "NavBR.OpenOmsi"
$manifest = Join-Path $target "NavBR.OpenOmsiPlugin.install-manifest.txt"

if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
    throw "Manifesto do NavBR OpenOMSI não encontrado. Nenhum arquivo foi removido."
}

$tracked = Get-Content -LiteralPath $manifest |
    Where-Object { $_ -and -not $_.TrimStart().StartsWith("#") } |
    ForEach-Object { [System.IO.Path]::GetFileName($_.Trim()) } |
    Where-Object { $_ }

foreach ($file in $tracked) {
    $path = Join-Path $target $file
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        Remove-Item -LiteralPath $path -Force
    }
}

Remove-Item -LiteralPath $manifest -Force
if ((Test-Path -LiteralPath $target) -and @(Get-ChildItem -LiteralPath $target -Force).Count -eq 0) {
    Remove-Item -LiteralPath $target -Force
}

Write-Host "NavBR for openOMSI removido."
