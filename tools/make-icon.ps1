#requires -Version 5.1
<#
.SYNOPSIS
  Generates icon.ico for Deno Player: near-black square with a centred DENO-green
  play triangle. Multi-resolution (16, 24, 32, 48, 64, 128, 256).

.NOTES
  Standalone — uses only System.Drawing (built into Windows .NET). No ImageMagick.
#>

[CmdletBinding()]
param(
    [string]$OutPath = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
} else {
    $scriptRoot = $PSScriptRoot
}
if ([string]::IsNullOrWhiteSpace($OutPath)) {
    $OutPath = Join-Path $scriptRoot "..\icon.ico"
}
$OutDir = Split-Path $OutPath -Parent
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$OutPath = [System.IO.Path]::GetFullPath($OutPath)

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    # Background: near-black (#0B0E0C)
    $bg = [System.Drawing.Color]::FromArgb(255, 11, 14, 12)
    $g.Clear($bg)

    # Subtle dark green outline ring (only for >=32 sizes to keep small ones clean)
    if ($size -ge 32) {
        $borderCol = [System.Drawing.Color]::FromArgb(255, 30, 40, 28)  # #1E281C
        $borderPen = New-Object System.Drawing.Pen $borderCol, ([Math]::Max(1, [int]($size / 64)))
        $inset = [Math]::Max(1, [int]($size / 32))
        $g.DrawRectangle($borderPen, $inset, $inset, ($size - 2 * $inset - 1), ($size - 2 * $inset - 1))
        $borderPen.Dispose()
    }

    # DENO green play triangle (#57E389)
    $accent = [System.Drawing.Color]::FromArgb(255, 87, 227, 137)
    $points = @(
        [System.Drawing.PointF]::new([float]($size * 0.32), [float]($size * 0.22)),
        [System.Drawing.PointF]::new([float]($size * 0.32), [float]($size * 0.78)),
        [System.Drawing.PointF]::new([float]($size * 0.80), [float]($size * 0.50))
    )
    $brush = New-Object System.Drawing.SolidBrush $accent
    $g.FillPolygon($brush, $points)
    $brush.Dispose()

    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs  = @{}
foreach ($s in $sizes) {
    $pngs[$s] = New-IconPng $s
    Write-Host ("  rendered {0}x{0}  ({1} bytes)" -f $s, $pngs[$s].Length) -ForegroundColor DarkGray
}

# Assemble .ico (ICONDIR + ICONDIRENTRY[] + PNG data[])
$fs = [System.IO.File]::Open($OutPath, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter $fs
try {
    # ICONDIR
    $bw.Write([UInt16]0)              # Reserved
    $bw.Write([UInt16]1)              # Type = icon
    $bw.Write([UInt16]$sizes.Count)   # Count

    # ICONDIRENTRYs
    $offset = 6 + 16 * $sizes.Count
    foreach ($s in $sizes) {
        $w = if ($s -ge 256) { 0 } else { $s }    # 0 means 256 in ICO format
        $h = $w
        $bw.Write([byte]$w)                       # Width
        $bw.Write([byte]$h)                       # Height
        $bw.Write([byte]0)                        # Color palette (0 = no palette)
        $bw.Write([byte]0)                        # Reserved
        $bw.Write([UInt16]1)                      # Color planes
        $bw.Write([UInt16]32)                     # Bits per pixel
        $bw.Write([UInt32]$pngs[$s].Length)       # Size of image data
        $bw.Write([UInt32]$offset)                # Offset to image data
        $offset += $pngs[$s].Length
    }

    foreach ($s in $sizes) {
        $bw.Write($pngs[$s])
    }
}
finally {
    $bw.Close()
    $fs.Close()
}

Write-Host ""
Write-Host ">>> Wrote $OutPath" -ForegroundColor Green
