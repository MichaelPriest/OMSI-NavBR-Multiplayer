param(
    [string]$Path = "src/NavBR.Client/Assets/NavBR.ico"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path)) {
    throw "Icon file not found: $Path"
}

$resolvedPath = (Resolve-Path -LiteralPath $Path).Path
$bytes = [System.IO.File]::ReadAllBytes($resolvedPath)
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

# Repair malformed size metadata first so System.Drawing can load the original source.
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

    $actualSize = [uint32]($nextOffset - $current.ImageOffset)
    if ($actualSize -ne $current.DeclaredSize) {
        [BitConverter]::GetBytes($actualSize).CopyTo($bytes, $current.EntryOffset + 8)
    }
}

[System.IO.File]::WriteAllBytes($resolvedPath, $bytes)

# Windows displays the same application icon at many sizes. The original asset contained
# a single 64x64 frame, which produced blurry/odd taskbar and Explorer rendering. Build a
# proper multi-resolution ICO from the official NavBR artwork while preserving its design.
Add-Type -AssemblyName System.Drawing

$sourceIcon = [System.Drawing.Icon]::new($resolvedPath)
try {
    $sourceBitmap = $sourceIcon.ToBitmap()
    try {
        $sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
        $frames = New-Object System.Collections.Generic.List[object]

        foreach ($size in $sizes) {
            $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try {
                $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
                try {
                    $graphics.Clear([System.Drawing.Color]::Transparent)
                    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                    $graphics.DrawImage($sourceBitmap, 0, 0, $size, $size)
                }
                finally {
                    $graphics.Dispose()
                }

                $stream = [System.IO.MemoryStream]::new()
                try {
                    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                    $frames.Add([pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() })
                }
                finally {
                    $stream.Dispose()
                }
            }
            finally {
                $bitmap.Dispose()
            }
        }

        $output = [System.IO.MemoryStream]::new()
        $writer = [System.IO.BinaryWriter]::new($output)
        try {
            $writer.Write([uint16]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]$frames.Count)

            $offset = 6 + (16 * $frames.Count)
            foreach ($frame in $frames) {
                $dimension = if ($frame.Size -ge 256) { [byte]0 } else { [byte]$frame.Size }
                $writer.Write($dimension)
                $writer.Write($dimension)
                $writer.Write([byte]0)
                $writer.Write([byte]0)
                $writer.Write([uint16]1)
                $writer.Write([uint16]32)
                $writer.Write([uint32]$frame.Bytes.Length)
                $writer.Write([uint32]$offset)
                $offset += $frame.Bytes.Length
            }

            foreach ($frame in $frames) {
                $writer.Write([byte[]]$frame.Bytes)
            }

            $writer.Flush()
            [System.IO.File]::WriteAllBytes($resolvedPath, $output.ToArray())
        }
        finally {
            $writer.Dispose()
            $output.Dispose()
        }
    }
    finally {
        $sourceBitmap.Dispose()
    }
}
finally {
    $sourceIcon.Dispose()
}

$finalBytes = [System.IO.File]::ReadAllBytes($resolvedPath)
$finalCount = [BitConverter]::ToUInt16($finalBytes, 4)
if ($finalCount -lt 8) {
    throw "Multi-resolution ICO generation failed: only $finalCount frame(s)."
}

$validatedIcon = [System.Drawing.Icon]::new($resolvedPath)
try {
    Write-Host "NavBR multi-resolution icon valid: $finalCount frames, $($finalBytes.Length) bytes"
}
finally {
    $validatedIcon.Dispose()
}
