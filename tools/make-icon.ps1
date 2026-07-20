#requires -Version 5.1
<#
.SYNOPSIS
  Generates Deno Video Player .ico files. The default app icon is a near-black
  square with a centred DENO-green play triangle. File icons can be generated
  for video, audio, and image associations.

.NOTES
  Standalone — uses only System.Drawing (built into Windows .NET). No ImageMagick.
#>

[CmdletBinding()]
param(
    [string]$OutPath = "",
    [ValidateSet("App", "Video", "Audio", "Image")]
    [string]$Kind = "App"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
} else {
    $scriptRoot = $PSScriptRoot
}
if ([string]::IsNullOrWhiteSpace($OutPath)) {
    if ($Kind -eq "App") {
        $OutPath = Join-Path $scriptRoot "..\icon.ico"
    } else {
        $OutPath = Join-Path $scriptRoot ("..\Assets\Icons\file-{0}.ico" -f $Kind.ToLowerInvariant())
    }
}
$OutPath = [System.IO.Path]::GetFullPath($OutPath)
$OutDir = [System.IO.Path]::GetDirectoryName($OutPath)
if (-not [string]::IsNullOrWhiteSpace($OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

function Fill-PlayTriangle($g, [int]$size, $brush) {
    $points = @(
        [System.Drawing.PointF]::new([float]($size * 0.32), [float]($size * 0.22)),
        [System.Drawing.PointF]::new([float]($size * 0.32), [float]($size * 0.78)),
        [System.Drawing.PointF]::new([float]($size * 0.80), [float]($size * 0.50))
    )
    $g.FillPolygon($brush, $points)
}

function Fill-VideoGlyph($g, [int]$size, $brush) {
    $x = [float]($size * 0.18)
    $y = [float]($size * 0.24)
    $w = [float]($size * 0.64)
    $h = [float]($size * 0.52)
    $g.FillRectangle($brush, $x, $y, $w, $h)

    $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 11, 14, 12))
    try {
        $hole = [float]([Math]::Max(1, $size * 0.055))
        $gap = [float]($size * 0.16)
        for ($i = 0; $i -lt 3; $i++) {
            $hy = [float]($y + $size * 0.08 + $gap * $i)
            $g.FillRectangle($bgBrush, [float]($x + $size * 0.045), $hy, $hole, $hole)
            $g.FillRectangle($bgBrush, [float]($x + $w - $size * 0.10), $hy, $hole, $hole)
        }

        $points = @(
            [System.Drawing.PointF]::new([float]($size * 0.45), [float]($size * 0.38)),
            [System.Drawing.PointF]::new([float]($size * 0.45), [float]($size * 0.63)),
            [System.Drawing.PointF]::new([float]($size * 0.64), [float]($size * 0.505))
        )
        $g.FillPolygon($bgBrush, $points)
    } finally {
        $bgBrush.Dispose()
    }
}

function Fill-AudioGlyph($g, [int]$size, $brush) {
    $stemWidth = [float]([Math]::Max(1.0, $size * 0.075))
    $g.FillRectangle($brush, [float]($size * 0.55), [float]($size * 0.20), $stemWidth, [float]($size * 0.52))

    $flagPoints = @(
        [System.Drawing.PointF]::new([float]($size * 0.60), [float]($size * 0.20)),
        [System.Drawing.PointF]::new([float]($size * 0.82), [float]($size * 0.28)),
        [System.Drawing.PointF]::new([float]($size * 0.82), [float]($size * 0.43)),
        [System.Drawing.PointF]::new([float]($size * 0.60), [float]($size * 0.35))
    )
    $g.FillPolygon($brush, $flagPoints)

    $g.FillEllipse($brush, [float]($size * 0.27), [float]($size * 0.60), [float]($size * 0.33), [float]($size * 0.22))
}

function Fill-ImageGlyph($g, [int]$size, $brush) {
    $penWidth = [float]([Math]::Max(1.0, $size * 0.065))
    $pen = New-Object System.Drawing.Pen $brush.Color, $penWidth
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    try {
        $g.DrawRectangle($pen, [float]($size * 0.20), [float]($size * 0.24), [float]($size * 0.60), [float]($size * 0.50))
        $g.FillEllipse($brush, [float]($size * 0.59), [float]($size * 0.32), [float]($size * 0.12), [float]($size * 0.12))

        $mountains = @(
            [System.Drawing.PointF]::new([float]($size * 0.25), [float]($size * 0.68)),
            [System.Drawing.PointF]::new([float]($size * 0.44), [float]($size * 0.49)),
            [System.Drawing.PointF]::new([float]($size * 0.55), [float]($size * 0.61)),
            [System.Drawing.PointF]::new([float]($size * 0.64), [float]($size * 0.54)),
            [System.Drawing.PointF]::new([float]($size * 0.76), [float]($size * 0.68))
        )
        $g.DrawLines($pen, $mountains)
    } finally {
        $pen.Dispose()
    }
}

function New-IconPng([int]$size, [string]$kind) {
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

    # DENO green glyph (#57E389)
    $accent = [System.Drawing.Color]::FromArgb(255, 87, 227, 137)
    $brush = New-Object System.Drawing.SolidBrush $accent
    try {
        switch ($kind) {
            "Video" { Fill-VideoGlyph $g $size $brush }
            "Audio" { Fill-AudioGlyph $g $size $brush }
            "Image" { Fill-ImageGlyph $g $size $brush }
            default { Fill-PlayTriangle $g $size $brush }
        }
    } finally {
        $brush.Dispose()
    }

    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs  = @{}
foreach ($s in $sizes) {
    $pngs[$s] = New-IconPng $s $Kind
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
