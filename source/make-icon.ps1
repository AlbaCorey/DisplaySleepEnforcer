# Display Sleep Enforcer - force display sleep, including while locked.
# Copyright (C) 2026 ferdef150
#
# This program is free software: you can redistribute it and/or modify it under
# the terms of the GNU General Public License as published by the Free Software
# Foundation, either version 3 of the License, or (at your option) any later
# version.
#
# This program is distributed in the hope that it will be useful, but WITHOUT
# ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
# FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License along with
# this program. If not, see <https://www.gnu.org/licenses/>.
# ---------------------------------------------------------------------------
#  Generates source\DSE.ico - the Display Sleep Enforcer application icon.
#  Draws a monitor showing a crescent moon (i.e. "display asleep") at several
#  sizes and packs them into a single multi-resolution .ico file.
# ---------------------------------------------------------------------------
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedRect {
    param([single]$X, [single]$Y, [single]$W, [single]$H, [single]$R)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    if ($R -le 0) {
        $path.AddRectangle((New-Object System.Drawing.RectangleF($X, $Y, $W, $H)))
        return $path
    }
    $d = $R * 2
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $W - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $W - $d, $Y + $H - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $H - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [double]$Size
    $frameColor  = [System.Drawing.Color]::FromArgb(255, 71, 85, 105)
    $screenColor = [System.Drawing.Color]::FromArgb(255, 15, 23, 42)
    $moonColor   = [System.Drawing.Color]::FromArgb(255, 250, 204, 21)
    $starColor   = [System.Drawing.Color]::FromArgb(255, 226, 232, 240)

    $frameBrush  = New-Object System.Drawing.SolidBrush($frameColor)
    $screenBrush = New-Object System.Drawing.SolidBrush($screenColor)
    $moonBrush   = New-Object System.Drawing.SolidBrush($moonColor)
    $starBrush   = New-Object System.Drawing.SolidBrush($starColor)

    # Monitor geometry
    $pad = [Math]::Max(0.0, [Math]::Round($s * 0.06))
    $mx = $pad
    $my = $pad
    $mw = $s - 2 * $pad
    $mh = [Math]::Round($s * 0.60)
    $r  = [Math]::Max(1.0, $s * 0.07)

    $framePath = New-RoundedRect -X $mx -Y $my -W $mw -H $mh -R $r
    $g.FillPath($frameBrush, $framePath)

    # Screen
    $b = [Math]::Max(1.0, $s * 0.05)
    $sx = $mx + $b
    $sy = $my + $b
    $sw = $mw - 2 * $b
    $sh = $mh - 2 * $b
    $screenPath = New-RoundedRect -X $sx -Y $sy -W $sw -H $sh -R ([Math]::Max(1.0, $r - 1))
    $g.FillPath($screenBrush, $screenPath)

    # Stand
    $neckW = [Math]::Round($mw * 0.16)
    $neckH = [Math]::Round($s * 0.09)
    $neckX = $mx + ($mw - $neckW) / 2
    $neckY = $my + $mh
    $g.FillRectangle($frameBrush, [single]$neckX, [single]$neckY, [single]$neckW, [single]$neckH)

    $baseW = [Math]::Round($mw * 0.5)
    $baseH = [Math]::Max(1.0, [Math]::Round($s * 0.05))
    $baseX = $mx + ($mw - $baseW) / 2
    $baseY = $neckY + $neckH
    $g.FillRectangle($frameBrush, [single]$baseX, [single]$baseY, [single]$baseW, [single]$baseH)

    # Crescent moon inside the screen
    $rm = [Math]::Max(1.2, [Math]::Min($sw, $sh) * 0.26)
    $moonCx = $sx + $sw * 0.60
    $moonCy = $sy + $sh * 0.46
    $g.FillEllipse($moonBrush, [single]($moonCx - $rm), [single]($moonCy - $rm), [single]($rm * 2), [single]($rm * 2))

    $carveR = $rm * 0.92
    $carveCx = $moonCx + $rm * 0.52
    $carveCy = $moonCy - $rm * 0.30
    $g.FillEllipse($screenBrush, [single]($carveCx - $carveR), [single]($carveCy - $carveR), [single]($carveR * 2), [single]($carveR * 2))

    # A few stars (skip on very small sizes)
    if ($Size -ge 32) {
        $stars = @(
            @(0.20, 0.28, 0.028),
            @(0.33, 0.64, 0.020),
            @(0.16, 0.52, 0.016)
        )
        foreach ($st in $stars) {
            $srx = [Math]::Max(0.6, $sw * $st[2])
            $scx = $sx + $sw * $st[0]
            $scy = $sy + $sh * $st[1]
            $g.FillEllipse($starBrush, [single]($scx - $srx), [single]($scy - $srx), [single]($srx * 2), [single]($srx * 2))
        }
    }

    $frameBrush.Dispose(); $screenBrush.Dispose(); $moonBrush.Dispose(); $starBrush.Dispose()
    $framePath.Dispose(); $screenPath.Dispose()
    $g.Dispose()
    return $bmp
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = New-Object 'System.Collections.Generic.List[byte[]]'
foreach ($sz in $sizes) {
    $bmp = New-IconBitmap -Size $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $images.Add($ms.ToArray())
    $ms.Dispose()
    $bmp.Dispose()
}

$outPath = Join-Path $PSScriptRoot 'DSE.ico'
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([UInt16]0)              # reserved
$bw.Write([UInt16]1)              # type = icon
$bw.Write([UInt16]$sizes.Count)   # image count

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$dim)         # width  (0 => 256)
    $bw.Write([byte]$dim)         # height (0 => 256)
    $bw.Write([byte]0)            # palette
    $bw.Write([byte]0)            # reserved
    $bw.Write([UInt16]1)          # color planes
    $bw.Write([UInt16]32)         # bits per pixel
    $bw.Write([UInt32]$images[$i].Length)
    $bw.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $bw.Write($img) }
$bw.Flush()

[System.IO.File]::WriteAllBytes($outPath, $out.ToArray())
$bw.Dispose(); $out.Dispose()

Write-Host ("Wrote {0} ({1} bytes, {2} sizes)" -f $outPath, (Get-Item $outPath).Length, $sizes.Count)
