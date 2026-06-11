# Gera AppIcon.ico e app-icon.png — 256×256, preenchimento total, sem barras
$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$icoPath = Join-Path $dir "AppIcon.ico"
$pngPath = Join-Path $dir "app-icon.png"

Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size,
        ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bmp.SetResolution(96, 96)

    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

    $bg   = [System.Drawing.Color]::FromArgb(255, 13, 17, 23)
    $cyan = [System.Drawing.Color]::FromArgb(255, 0, 170, 255)

    $g.FillRectangle((New-Object System.Drawing.SolidBrush $bg), 0, 0, $size, $size)

    $cx = $size / 2.0
    $cy = $size / 2.0
    $radius = $size * 0.36
    $stroke = [Math]::Max(2.0, $size * 0.034)
    $pen = New-Object System.Drawing.Pen $cyan, $stroke
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    $g.DrawEllipse($pen,
        [float]($cx - $radius), [float]($cy - $radius),
        [float](2 * $radius), [float](2 * $radius))

    $dotR = $size * 0.055
    $brush = New-Object System.Drawing.SolidBrush $cyan
    $g.FillEllipse($brush,
        [float]($cx - $dotR), [float]($cy - $dotR),
        [float](2 * $dotR), [float](2 * $dotR))

    $tickLen = $size * 0.11
    $tickGap = $radius + ($size * 0.03)
    $g.DrawLine($pen, $cx, $cy - $tickGap - $tickLen, $cx, $cy - $tickGap)
    $g.DrawLine($pen, $cx, $cy + $tickGap, $cx, $cy + $tickGap + $tickLen)
    $g.DrawLine($pen, $cx - $tickGap - $tickLen, $cy, $cx - $tickGap, $cy)
    $g.DrawLine($pen, $cx + $tickGap, $cy, $cx + $tickGap + $tickLen, $cy)

    $boltPen = New-Object System.Drawing.Pen $cyan, ([Math]::Max(2.0, $size * 0.028))
    $boltPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $boltPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $bx = $cx + $radius * 0.42
    $by = $cy + $radius * 0.42
    $u  = $size * 0.045
    $g.DrawLine($boltPen, $bx + $u*0.2, $by + $u*1.6, $bx - $u*0.4, $by + $u*0.8)
    $g.DrawLine($boltPen, $bx - $u*0.4, $by + $u*0.8, $bx + $u*0.8, $by - $u*0.2)
    $g.DrawLine($boltPen, $bx + $u*0.8, $by - $u*0.2, $bx - $u*0.2, $by - $u*1.2)

    $pen.Dispose(); $boltPen.Dispose(); $brush.Dispose()
    $g.Dispose()
    return $bmp
}

function Get-BitmapDibBytes([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width
    $h = $bmp.Height
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms

    # BITMAPINFOHEADER (formato ICO usa BMP 32bpp + máscara AND)
    $bw.Write([int32]40)
    $bw.Write([int32]$w)
    $bw.Write([int32]($h * 2))
    $bw.Write([int16]1)
    $bw.Write([int16]32)
    $bw.Write([int32]0)
    $bw.Write([int32]0)
    $bw.Write([int32]0)
    $bw.Write([int32]0)
    $bw.Write([int32]0)
    $bw.Write([int32]0)

    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $data = $bmp.LockBits($rect,
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $stride = $data.Stride
        $rowBytes = $w * 4
        $buffer = New-Object byte[] $rowBytes
        for ($y = $h - 1; $y -ge 0; $y--) {
            $src = [IntPtr]($data.Scan0.ToInt64() + ($y * $stride))
            [System.Runtime.InteropServices.Marshal]::Copy($src, $buffer, 0, $rowBytes)
            $bw.Write($buffer)
        }
    }
    finally {
        $bmp.UnlockBits($data)
    }

    $maskRowBytes = [Math]::Ceiling($w / 32.0) * 4
    $bw.Write((New-Object byte[] ($maskRowBytes * $h)))
    return $ms.ToArray()
}

$master = New-IconBitmap 256
if ($master.Width -ne 256 -or $master.Height -ne 256) {
    throw "PNG mestre deve ser 256×256, obtido $($master.Width)×$($master.Height)"
}
$master.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "PNG: $pngPath ($($master.Width)×$($master.Height))"

# BMP no ICO — compatível com o shell do Windows (evita barras com PNG mal declarado)
$sizes = @(256, 48, 32, 16)
$imageData = New-Object 'System.Collections.Generic.List[byte[]]'
$entries   = New-Object 'System.Collections.Generic.List[object]'

foreach ($size in $sizes) {
    $bmp = if ($size -eq 256) { $master } else { New-IconBitmap $size }
    $bytes = Get-BitmapDibBytes $bmp
    $imageData.Add($bytes) | Out-Null
    $entries.Add([PSCustomObject]@{
        Width  = if ($size -eq 256) { 0 } else { [byte]$size }
        Height = if ($size -eq 256) { 0 } else { [byte]$size }
        Length = $bytes.Length
    }) | Out-Null
    if ($size -ne 256) { $bmp.Dispose() }
}

$mem = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $mem
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$entries.Count)
$offset = 6 + (16 * $entries.Count)

for ($i = 0; $i -lt $entries.Count; $i++) {
    $entry = $entries[$i]
    $writer.Write([byte]$entry.Width); $writer.Write([byte]$entry.Height)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$entry.Length); $writer.Write([uint32]$offset)
    $offset += $entry.Length
}

for ($i = 0; $i -lt $imageData.Count; $i++) {
    $writer.Write($imageData[$i])
}

$icoBytes = $mem.ToArray()
[System.IO.File]::WriteAllBytes($icoPath, $icoBytes)
$mem.Dispose()
$master.Dispose()

Write-Host "ICO: $icoPath ($($icoBytes.Length) bytes, $($sizes.Count) tamanhos BMP)"
