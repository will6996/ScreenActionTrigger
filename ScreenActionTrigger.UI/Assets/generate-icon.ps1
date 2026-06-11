# Gera Assets\AppIcon.ico a partir de Assets\app-icon.png (multi-tamanho)
param(
    [double]$HorizontalStretch = 1.22
)

$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$pngPath = Join-Path $dir "app-icon.png"
$icoPath = Join-Path $dir "AppIcon.ico"

if (-not (Test-Path $pngPath)) { throw "Arquivo não encontrado: $pngPath" }

Add-Type -AssemblyName System.Drawing
$sizes = @(256, 128, 64, 48, 32, 16)
$png = [System.Drawing.Image]::FromFile($pngPath)
$mem = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($mem)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
$dataList = @()

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # Fundo escuro igual ao ícone original
    $g.Clear([System.Drawing.Color]::FromArgb(255, 13, 17, 23))

    # Estica lateralmente para corrigir proporção estreita
    $g.TranslateTransform($size / 2.0, $size / 2.0)
    $g.ScaleTransform($HorizontalStretch, 1.0)
    $g.TranslateTransform(-$size / 2.0, -$size / 2.0)
    $g.DrawImage($png, 0, 0, $size, $size)
    $g.Dispose()

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
$png.Dispose(); $mem.Dispose()
Write-Host "Gerado: $icoPath (stretch horizontal: ${HorizontalStretch}x)"
