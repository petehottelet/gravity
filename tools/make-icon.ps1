# Draws the Gravity app icon (a page with a small orange "mass") at several sizes
# and writes a multi-resolution .ico with PNG-compressed entries.
param([string]$Out = (Join-Path $PSScriptRoot '..\src\Gravity.ico'))

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $u = $size / 40.0
    $blue = [System.Drawing.Color]::FromArgb(255, 0, 95, 184)
    $orange = [System.Drawing.Color]::FromArgb(255, 224, 108, 0)
    $page = New-RoundRect (6 * $u) (1 * $u) (24 * $u) (31 * $u) ([Math]::Max(1, 3.5 * $u))
    $g.FillPath((New-Object System.Drawing.SolidBrush $blue), $page)
    if ($size -ge 24) {
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(215, 255, 255, 255)), ([Math]::Max(1, 2.4 * $u))
        $g.DrawLine($pen, 11 * $u, 10 * $u, 25 * $u, 10 * $u)
        $g.DrawLine($pen, 11 * $u, 15.5 * $u, 25 * $u, 15.5 * $u)
        $g.DrawLine($pen, 11 * $u, 21 * $u, 20 * $u, 21 * $u)
    }
    $ring = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([Math]::Max(1, 2 * $u))
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $orange), 22 * $u, 24 * $u, 15 * $u, 15 * $u)
    $g.DrawEllipse($ring, 22 * $u, 24 * $u, 15 * $u, 15 * $u)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = @()
foreach ($s in $sizes) { $images += , (New-IconPng $s) }

$fs = [System.IO.File]::Create($Out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$images[$i].Length); $w.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Close()
Write-Host "Wrote $Out"
