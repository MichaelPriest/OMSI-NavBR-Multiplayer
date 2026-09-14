param(
    [string]$Path = "src/NavBR.Client/Assets/NavBR.ico"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path)) {
    throw "Icon file not found: $Path"
}

$bytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path))
if ($bytes.Length -lt 22) {
    throw "ICO file is too small: $($bytes.Length) bytes"
}

$reserved = [BitConverter]::ToUInt16($bytes, 0)
$type = [BitConverter]::ToUInt16($bytes, 2)
$count = [BitConverter]::ToUInt16($bytes, 4)

if ($reserved -ne 0 -or $type -ne 1 -or $count -lt 1) {
    throw "Invalid ICO header (reserved=$reserved type=$type count=$count)."
}

$directoryEnd = 6 + (16 * $count)
if ($bytes.Length -lt $directoryEnd) {
    throw "ICO directory is truncated."
}

$entries = @()
for ($i = 0; $i -lt $count; $i++) {
    $entry = 6 + (16 * $i)
    $declaredSize = [BitConverter]::ToUInt32($bytes, $entry + 8)
    $imageOffset = [BitConverter]::ToUInt32($bytes, $entry + 12)

    if ($imageOffset -lt $directoryEnd -or $imageOffset -ge $bytes.Length) {
        throw "Invalid ICO image offset $imageOffset for entry $i."
    }

    $entries += [pscustomobject]@{
        Index = $i
        EntryOffset = $entry
        DeclaredSize = [uint32]$declaredSize
        ImageOffset = [uint32]$imageOffset
    }
}

$ordered = $entries | Sort-Object ImageOffset
for ($i = 0; $i -lt $ordered.Count; $i++) {
    $current = $ordered[$i]
    $nextOffset = if ($i + 1 -lt $ordered.Count) {
        [uint32]$ordered[$i + 1].ImageOffset
    } else {
        [uint32]$bytes.Length
    }

    if ($nextOffset -le $current.ImageOffset) {
        throw "Invalid ICO image ordering around entry $($current.Index)."
    }

    $actualSize = [uint32]($nextOffset - $current.ImageOffset)
    if ($actualSize -ne $current.DeclaredSize) {
        Write-Host "Repairing ICO entry $($current.Index): declared=$($current.DeclaredSize), actual=$actualSize"
        [BitConverter]::GetBytes($actualSize).CopyTo($bytes, $current.EntryOffset + 8)
    }
}

[System.IO.File]::WriteAllBytes((Resolve-Path -LiteralPath $Path), $bytes)

# Validate that Windows can actually load the repaired icon.
Add-Type -AssemblyName System.Drawing
$icon = [System.Drawing.Icon]::new((Resolve-Path -LiteralPath $Path))
try {
    Write-Host "NavBR icon valid: $($icon.Width)x$($icon.Height), $($bytes.Length) bytes"
}
finally {
    $icon.Dispose()
}
