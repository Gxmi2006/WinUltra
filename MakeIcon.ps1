# Draws WinUltra.ico: a dark glass tile with the island's glowing pill and a floating bar.
# Run: powershell -NoProfile -File MakeIcon.ps1
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Draw([int]$n) {
    $bmp = New-Object System.Drawing.Bitmap $n, $n, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $n / 256.0

    # Tile: deep indigo to near black, rounded like a Windows 11 app icon.
    $pad = [Math]::Max(0.5, 8 * $s)
    $tile = RoundRect $pad $pad ($n - 2 * $pad) ($n - 2 * $pad) (56 * $s)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new(0, $n)), ([System.Drawing.Color]::FromArgb(255, 44, 48, 86)), ([System.Drawing.Color]::FromArgb(255, 10, 10, 20))
    $g.FillPath($bg, $tile)

    # A soft violet glow behind the pill.
    $glowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $glowPath.AddEllipse([float](40 * $s), [float](30 * $s), [float](176 * $s), [float](120 * $s))
    $glow = New-Object System.Drawing.Drawing2D.PathGradientBrush $glowPath
    $glow.CenterColor = [System.Drawing.Color]::FromArgb(150, 150, 110, 255)
    $glow.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 150, 110, 255))
    $g.SetClip($tile)
    $g.FillPath($glow, $glowPath)

    # The island: a glowing pill in blue, violet and pink.
    $pw = 150 * $s; $ph = 44 * $s; $px = ($n - $pw) / 2; $py = 82 * $s
    $pill = RoundRect $px $py $pw $ph ($ph / 2)
    $pb = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new($px, 0)), ([System.Drawing.PointF]::new($px + $pw, 0)), ([System.Drawing.Color]::White), ([System.Drawing.Color]::White)
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend 3
    $blend.Colors = @([System.Drawing.Color]::FromArgb(255, 111, 140, 255), [System.Drawing.Color]::FromArgb(255, 185, 124, 255), [System.Drawing.Color]::FromArgb(255, 255, 143, 207))
    $blend.Positions = @(0.0, 0.5, 1.0)
    $pb.InterpolationColors = $blend
    $g.FillPath($pb, $pill)
    if ($n -ge 32) {
        $shine = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, $py)), ([System.Drawing.PointF]::new(0, $py + $ph)), ([System.Drawing.Color]::FromArgb(110, 255, 255, 255)), ([System.Drawing.Color]::FromArgb(0, 255, 255, 255))
        $g.FillPath($shine, (RoundRect ($px + 6 * $s) ($py + 3 * $s) ($pw - 12 * $s) ($ph * 0.5) ($ph * 0.25)))
    }

    # A floating taskbar under it, faint.
    if ($n -ge 24) {
        $bw = 176 * $s; $bh = 22 * $s; $bx = ($n - $bw) / 2; $by = 186 * $s
        $bar = RoundRect $bx $by $bw $bh ($bh / 2)
        $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(70, 255, 255, 255))), $bar)
    }
    $g.ResetClip()

    # A bright rim along the top edge, like the island's glass.
    if ($n -ge 32) {
        $rim = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new(0, $n)), ([System.Drawing.Color]::FromArgb(120, 255, 255, 255)), ([System.Drawing.Color]::FromArgb(10, 255, 255, 255))
        $g.DrawPath((New-Object System.Drawing.Pen $rim, ([float][Math]::Max(1, 3 * $s))), $tile)
    }
    $g.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = @()
foreach ($n in $sizes) {
    $bmp = Draw $n
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($n -eq 256) { $bmp.Save((Join-Path $dir 'WinUltra.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    $images += , $ms.ToArray()
}

# ICO file: header, one directory entry per size, then the PNG data.
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $n = $sizes[$i]
    $b = if ($n -ge 256) { 0 } else { $n }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$images[$i].Length); $w.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $dir 'WinUltra.ico'), $out.ToArray())
"WinUltra.ico written ($($sizes -join ', ') px)"
