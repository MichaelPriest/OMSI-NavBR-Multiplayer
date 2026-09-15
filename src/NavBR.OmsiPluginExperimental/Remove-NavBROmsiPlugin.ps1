[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OmsiRoot
)

$ErrorActionPreference = 'Stop'

$running = Get-Process -Name 'Omsi' -ErrorAction SilentlyContinue
if ($running) {
    throw 'Feche o OMSI antes de remover o plugin NavBR.'
}

$root = [System.IO.Path]::GetFullPath($OmsiRoot)
$omsiExe = Join-Path $root 'Omsi.exe'
if (-not (Test-Path -LiteralPath $omsiExe -PathType Leaf)) {
    throw "Omsi.exe não foi encontrado em: $root"
}

$pluginsRoot = Join-Path $root 'plugins'
$manifestPath = Join-Path $pluginsRoot 'NavBR.OmsiPlugin.install-manifest.txt'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Manifesto de instalação não encontrado: $manifestPath. Nenhum arquivo foi removido automaticamente."
}

$entries = Get-Content -LiteralPath $manifestPath | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_) -and -not $_.TrimStart().StartsWith('#')
}

$removed = 0
foreach ($entry in $entries) {
    $fileName = [System.IO.Path]::GetFileName($entry.Trim())
    if ([string]::IsNullOrWhiteSpace($fileName)) {
        continue
    }

    $target = Join-Path $pluginsRoot $fileName
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        Remove-Item -LiteralPath $target -Force
        $removed++
    }
}

Remove-Item -LiteralPath $manifestPath -Force

Write-Host ''
Write-Host 'Plugin experimental NavBR removido.' -ForegroundColor Green
Write-Host "Arquivos removidos: $removed"
Write-Host "Pasta OMSI: $root"
