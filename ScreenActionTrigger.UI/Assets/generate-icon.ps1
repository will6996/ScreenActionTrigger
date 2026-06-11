# Gera AppIcon.ico e app-icon.png — exatamente 256×256, preenchimento total, sem barras
$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$icoPath = Join-Path $dir "AppIcon.ico"
$pngPath = Join-Path $dir "app-icon.png"

Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size) {
    # Bitmap quadrado exato, 32bpp
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

    # Preenche o canvas inteiro (0,0) → (size,size) — sem margens
    $g.FillRectangle(
        (New-Object System.Drawing.SolidBrush $bg),
        0, 0, $size, $size)

    $cx = $size / 2.0
    $cy = $size / 2.0

    # Símbolo grande — ocupa ~78% do lado (sem faixas vazias nas bordas)
    $radius = $size * 0.36
    $stroke = [Math]::Max(2.0, $size * 0.034)
    $pen = New-Object System.Drawing.Pen $cyan, $stroke
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    # Anel
    $g.DrawEllipse($pen,
        [float]($cx - $radius), [float]($cy - $radius),
        [float](2 * $radius), [float](2 * $radius))

    # Centro
    $dotR = $size * 0.055
    $brush = New-Object System.Drawing.SolidBrush $cyan
    $g.FillEllipse($brush,
        [float]($cx - $dotR), [float]($cy - $dotR),
        [float](2 * $dotR), [float](2 * $dotR))

    # Ticks — mesmo comprimento, próximos da borda
    $tickLen = $size * 0.11
    $tickGap = $radius + ($size * 0.03)
    $g.DrawLine($pen, $cx, $cy - $tickGap - $tickLen, $cx, $cy - $tickGap)
    $g.DrawLine($pen, $cx, $cy + $tickGap, $cx, $cy + $tickGap + $tickLen)
    $g.DrawLine($pen, $cx - $tickGap - $tickLen, $cy, $cx - $tickGap, $cy)
    $g.DrawLine($pen, $cx + $tickGap, $cy, $cx + $tickGap + $tickLen, $cy)

    # Raio
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

# ── PNG mestre 256×256 ──────────────────────────────────────────────────────
$master = New-IconBitmap 256
if ($master.Width -ne 256 -or $master.Height -ne 256) {
    throw "PNG mestre deve ser 256×256, obtido $($master.Width)×$($master.Height)"
}
$master.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "PNG: $pngPath ($($master.Width)×$($master.Height))"

# ── ICO multi-tamanho (cada entrada é quadrada e preenchida) ───────────────
$sizes = @(256, 128, 64, 48, 32, 16)
$mem = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($mem)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
$dataList = @()

foreach ($size in $sizes) {
    $bmp = if ($size -eq 256) { $master } else { New-IconBitmap $size }
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray(); $dataList += ,$bytes
    $w = if ($size -eq 256) { 0 } else { [byte]$size }
    $h = if ($size -eq 256) { 0 } else { [byte]$size }
    $writer.Write([byte]$w); $writer.Write([byte]$h)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]$offset)
    $offset += $bytes.Length
    if ($size -ne 256) { $bmp.Dispose() }
    $ms.Dispose()
}

foreach ($bytes in $dataList) { $writer.Write($bytes) }
[System.IO.File]::WriteAllBytes($icoPath, $mem.ToArray())
$mem.Dispose()
$master.Dispose()

Write-Host "ICO: $icoPath ($((Get-Item $icoPath).Length) bytes, $($sizes.Count) tamanhos)"
