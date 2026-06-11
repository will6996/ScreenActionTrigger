# Gera Assets\AppIcon.ico desenhando o símbolo com proporções corretas (sem barras laterais)
$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$icoPath = Join-Path $dir "AppIcon.ico"
$pngPath = Join-Path $dir "app-icon.png"

Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    $bg = [System.Drawing.Color]::FromArgb(255, 13, 17, 23)
    $cyan = [System.Drawing.Color]::FromArgb(255, 0, 170, 255)
    $g.Clear($bg)

    $cx = $size / 2.0
    $cy = $size / 2.0
    $radius = $size * 0.27
    $stroke = [Math]::Max(2.0, $size * 0.028)
    $pen = New-Object System.Drawing.Pen $cyan, $stroke
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    # Anel (círculo perfeito)
    $g.DrawEllipse($pen, $cx - $radius, $cy - $radius, 2 * $radius, 2 * $radius)

    # Centro
    $dotR = $size * 0.045
    $brush = New-Object System.Drawing.SolidBrush $cyan
    $g.FillEllipse($brush, $cx - $dotR, $cy - $dotR, 2 * $dotR, 2 * $dotR)

    # Tick marks (mesmo comprimento horizontal e vertical)
    $tickLen = $size * 0.09
    $tickGap = $radius + ($size * 0.04)
    $g.DrawLine($pen, $cx, $cy - $tickGap - $tickLen, $cx, $cy - $tickGap)
    $g.DrawLine($pen, $cx, $cy + $tickGap, $cx, $cy + $tickGap + $tickLen)
    $g.DrawLine($pen, $cx - $tickGap - $tickLen, $cy, $cx - $tickGap, $cy)
    $g.DrawLine($pen, $cx + $tickGap, $cy, $cx + $tickGap + $tickLen, $cy)

    # Raio (canto inferior direito)
    $boltPen = New-Object System.Drawing.Pen $cyan, ([Math]::Max(2.0, $size * 0.022))
    $boltPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $boltPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $bx = $cx + $radius * 0.55
    $by = $cy + $radius * 0.55
    $g.DrawLine($boltPen, $bx + $size*0.01, $by + $size*0.08, $bx - $size*0.02, $by + $size*0.04)
    $g.DrawLine($boltPen, $bx - $size*0.02, $by + $size*0.04, $bx + $size*0.04, $by - $size*0.01)
    $g.DrawLine($boltPen, $bx + $size*0.04, $by - $size*0.01, $bx - $size*0.01, $by - $size*0.06)

    $pen.Dispose(); $boltPen.Dispose(); $brush.Dispose(); $g.Dispose()
    return $bmp
}

$sizes = @(256, 128, 64, 48, 32, 16)
$mem = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($mem)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
$dataList = @()

foreach ($size in $sizes) {
    $bmp = New-IconBitmap $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray(); $dataList += ,$bytes
    $w = if ($size -eq 256) { 0 } else { $size }
    $h = if ($size -eq 256) { 0 } else { $size }
    $writer.Write([byte]$w); $writer.Write([byte]$h)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]$offset)
    $offset += $bytes.Length
    $bmp.Dispose(); $ms.Dispose()
}

foreach ($bytes in $dataList) { $writer.Write($bytes) }
[System.IO.File]::WriteAllBytes($icoPath, $mem.ToArray())
$mem.Dispose()

# Exporta PNG de referência 256px
$ref = New-IconBitmap 256
$ref.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$ref.Dispose()

Write-Host "Gerado: $icoPath e $pngPath"
