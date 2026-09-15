param(
    [string]$Path = "src/NavBR.Client/Assets/NavBR.ico"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath(
    [float]$x,
    [float]$y,
    [float]$width,
    [float]$height,
    [float]$radius) {

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = [Math]::Max(1.0, $radius * 2.0)
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-NavBRMasterIcon {
    $size = 512
    $bitmap = [System.Drawing.Bitmap]::new(
        $size,
        $size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

        # O símbolo deriva diretamente da terceira arte aprovada do projeto:
        # pin de navegação laranja + ônibus branco. Para ícone do Windows usamos
        # apenas o símbolo, sem textos pequenos, para permanecer legível em 16/32 px.
        $orange = [System.Drawing.Color]::FromArgb(255, 255, 132, 0)
        $orangeDark = [System.Drawing.Color]::FromArgb(255, 224, 83, 0)
        $orangeLight = [System.Drawing.Color]::FromArgb(255, 255, 188, 67)
        $dark = [System.Drawing.Color]::FromArgb(255, 18, 20, 24)
        $dark2 = [System.Drawing.Color]::FromArgb(255, 31, 35, 42)
        $white = [System.Drawing.Color]::FromArgb(255, 247, 247, 247)

        # Sombra curta, suficiente para separar o símbolo em fundos claros/escuros.
        $shadowBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(78, 0, 0, 0))
        try {
            $graphics.FillEllipse($shadowBrush, 113, 60, 302, 302)
            $shadowPoints = [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new(151, 246),
                [System.Drawing.PointF]::new(377, 246),
                [System.Drawing.PointF]::new(264, 458)
            )
            $graphics.FillPolygon($shadowBrush, $shadowPoints)
        }
        finally {
            $shadowBrush.Dispose()
        }

        # Pin externo.
        $tailBrush = [System.Drawing.SolidBrush]::new($orangeDark)
        try {
            $tailPoints = [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new(143, 229),
                [System.Drawing.PointF]::new(369, 229),
                [System.Drawing.PointF]::new(256, 450)
            )
            $graphics.FillPolygon($tailBrush, $tailPoints)
        }
        finally {
            $tailBrush.Dispose()
        }

        $pinBrush = [System.Drawing.SolidBrush]::new($orange)
        try {
            $graphics.FillEllipse($pinBrush, 96, 36, 320, 320)
        }
        finally {
            $pinBrush.Dispose()
        }

        $highlightPen = [System.Drawing.Pen]::new($orangeLight, 11)
        try {
            $graphics.DrawEllipse($highlightPen, 108, 48, 296, 296)
        }
        finally {
            $highlightPen.Dispose()
        }

        # Centro escuro.
        $centerBrush = [System.Drawing.SolidBrush]::new($dark)
        try {
            $graphics.FillEllipse($centerBrush, 139, 79, 234, 234)
        }
        finally {
            $centerBrush.Dispose()
        }

        $centerPen = [System.Drawing.Pen]::new($dark2, 5)
        try {
            $graphics.DrawEllipse($centerPen, 139, 79, 234, 234)
        }
        finally {
            $centerPen.Dispose()
        }

        # Ônibus branco central. Formas deliberadamente simples para não borrar em 16/32 px.
        $busBrush = [System.Drawing.SolidBrush]::new($white)
        $windowBrush = [System.Drawing.SolidBrush]::new($dark)
        try {
            $body = New-RoundedRectanglePath 181 129 150 143 22
            try {
                $graphics.FillPath($busBrush, $body)
            }
            finally {
                $body.Dispose()
            }

            # Espelhos.
            $graphics.FillEllipse($busBrush, 163, 166, 26, 56)
            $graphics.FillEllipse($busBrush, 323, 166, 26, 56)

            # Destino/topo e para-brisa.
            $roof = New-RoundedRectanglePath 201 111 110 26 10
            try {
                $graphics.FillPath($busBrush, $roof)
            }
            finally {
                $roof.Dispose()
            }

            $windshield = New-RoundedRectanglePath 199 153 114 60 9
            try {
                $graphics.FillPath($windowBrush, $windshield)
            }
            finally {
                $windshield.Dispose()
            }

            # Para-choque e detalhes frontais.
            $graphics.FillRectangle($windowBrush, 205, 232, 102, 12)
            $graphics.FillEllipse($windowBrush, 204, 247, 18, 18)
            $graphics.FillEllipse($windowBrush, 290, 247, 18, 18)

            # Rodas/apoio visual inferior.
            $graphics.FillEllipse($busBrush, 195, 260, 22, 29)
            $graphics.FillEllipse($busBrush, 295, 260, 22, 29)
        }
        finally {
            $busBrush.Dispose()
            $windowBrush.Dispose()
        }

        return $bitmap
    }
    finally {
        $graphics.Dispose()
    }
}

function Convert-ToPngFrame([System.Drawing.Bitmap]$master, [int]$size) {
    $bitmap = [System.Drawing.Bitmap]::new(
        $size,
        $size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.DrawImage($master, 0, 0, $size, $size)

        $stream = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            return $stream.ToArray()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$resolvedPath = [System.IO.Path]::GetFullPath($Path)
$directory = [System.IO.Path]::GetDirectoryName($resolvedPath)
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$master = New-NavBRMasterIcon
try {
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
    $frames = foreach ($size in $sizes) {
        [pscustomobject]@{
            Size = $size
            Bytes = Convert-ToPngFrame $master $size
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
    $master.Dispose()
}

$finalBytes = [System.IO.File]::ReadAllBytes($resolvedPath)
if ($finalBytes.Length -lt 64) {
    throw "Generated NavBR icon is unexpectedly small."
}

$reserved = [BitConverter]::ToUInt16($finalBytes, 0)
$type = [BitConverter]::ToUInt16($finalBytes, 2)
$count = [BitConverter]::ToUInt16($finalBytes, 4)
if ($reserved -ne 0 -or $type -ne 1 -or $count -ne 10) {
    throw "Generated NavBR icon has an invalid ICO header (reserved=$reserved type=$type count=$count)."
}

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $resolvedPath).Hash.ToLowerInvariant()
Write-Host "NavBR compact app icon generated: $count frames, $($finalBytes.Length) bytes, sha256=$hash"
