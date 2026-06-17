# Generates the GlassSweeper app-icon asset set.
# Windows-Fluent take on SwiftSweeper's icon: purple diagonal gradient on a
# rounded square, with a crisp vector flag (pole + red pennant) instead of an
# emoji. Renders 1024px masters, then high-quality downscales to every MSIX
# asset size and assembles a multi-resolution .ico.
Add-Type -AssemblyName System.Drawing

$assets = "C:\Users\benja\GlassSweeper\GlassSweeper.App\Assets"

# SwiftSweeper purple gradient (top-left light -> bottom-right dark).
$purpleHi = [System.Drawing.Color]::FromArgb(255, 92, 36, 150)   # 0.32,0.10,0.55-ish, a touch brighter
$purpleLo = [System.Drawing.Color]::FromArgb(255, 40, 12, 70)    # 0.18,0.05,0.30-ish
$poleCol  = [System.Drawing.Color]::FromArgb(255, 238, 234, 246) # cool near-white
$flagHi   = [System.Drawing.Color]::FromArgb(255, 255, 107, 107) # #FF6B6B
$flagLo   = [System.Drawing.Color]::FromArgb(255, 224, 69, 90)   # #E0455A

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Set-Quality($g) {
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
}

# Draws the flag art (pole + pennant) into a 1024-space, scaled by $s with offset.
function Draw-Flag($g, [float]$s, [float]$ox, [float]$oy) {
    # Pole (rounded), centered composition tuned on the 1024 master.
    $poleX = 346 * $s + $ox; $poleY = 237 * $s + $oy
    $poleW = 44 * $s;        $poleH = 550 * $s
    $polePath = New-RoundedPath $poleX $poleY $poleW $poleH (22 * $s)

    # Pennant triangle pointing right, attached at the top of the pole.
    $tip = New-Object System.Drawing.Drawing2D.GraphicsPath
    [System.Drawing.PointF[]]$pts = @(
        (New-Object System.Drawing.PointF((390 * $s + $ox), (255 * $s + $oy))),
        (New-Object System.Drawing.PointF((672 * $s + $ox), (362 * $s + $oy))),
        (New-Object System.Drawing.PointF((390 * $s + $ox), (486 * $s + $oy)))
    )
    $tip.AddPolygon($pts)

    # Soft drop shadow (no blur in GDI+, so a low-alpha offset read).
    $shadow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(45, 0, 0, 0))
    $sg = $g.Save()
    $g.TranslateTransform(6 * $s, 8 * $s)
    $g.FillPath($shadow, $polePath)
    $g.FillPath($shadow, $tip)
    $g.Restore($sg)
    $shadow.Dispose()

    # Pennant with a subtle vertical gradient for depth.
    $tipRect = New-Object System.Drawing.RectangleF((390 * $s + $ox), (255 * $s + $oy), (282 * $s), (231 * $s))
    $flagBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($tipRect, $flagHi, $flagLo, 90.0)
    $g.FillPath($flagBrush, $tip)
    $flagBrush.Dispose()

    # Pole.
    $poleBrush = New-Object System.Drawing.SolidBrush $poleCol
    $g.FillPath($poleBrush, $polePath)
    $poleBrush.Dispose()

    $polePath.Dispose(); $tip.Dispose()
}

# Full square icon: rounded purple gradient + top-left highlight + flag.
function Render-Icon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    Set-Quality $g
    $s = $size / 1024.0
    $radius = 184 * $s

    $clip = New-RoundedPath 0 0 $size $size $radius
    $g.SetClip($clip)

    $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $purpleHi, $purpleLo, [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $g.FillRectangle($grad, $rect)
    $grad.Dispose()

    # Top-left radial highlight.
    $hr = $size * 0.62
    $hx = $size * 0.30; $hy = $size * 0.20
    $hp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $hp.AddEllipse(($hx - $hr), ($hy - $hr), ($hr * 2), ($hr * 2))
    $hb = New-Object System.Drawing.Drawing2D.PathGradientBrush($hp)
    $hb.CenterColor = [System.Drawing.Color]::FromArgb(60, 255, 255, 255)
    $hb.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 255, 255, 255))
    $g.FillPath($hb, $hp)
    $hb.Dispose(); $hp.Dispose()

    Draw-Flag $g $s 0 0

    $g.ResetClip()
    $g.Dispose(); $clip.Dispose()
    return $bmp
}

# Flag-only on transparent (for unplated / lock-screen variants).
function Render-FlagOnly([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    Set-Quality $g
    $s = $size / 1024.0
    Draw-Flag $g $s 0 0
    $g.Dispose()
    return $bmp
}

function Save-Png($bmp, [string]$path) {
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "wrote $path"
}

# Scale a master bitmap into a new WxH canvas (transparent), centered & contained.
function Compose-Centered($master, [int]$w, [int]$h, [double]$fill) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    Set-Quality $g
    $target = [Math]::Min($w, $h) * $fill
    $x = ($w - $target) / 2.0; $y = ($h - $target) / 2.0
    $g.DrawImage($master, $x, $y, $target, $target)
    $g.Dispose()
    return $bmp
}

# Wide tile: full-bleed rounded gradient + centered flag.
function Render-Wide([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    Set-Quality $g
    $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $purpleHi, $purpleLo, [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $g.FillRectangle($grad, $rect)
    $grad.Dispose()
    $flagMaster = Render-FlagOnly 1024
    $target = $h * 0.66
    $x = ($w - $target) / 2.0; $y = ($h - $target) / 2.0
    $g.DrawImage($flagMaster, $x, $y, $target, $target)
    $flagMaster.Dispose()
    $g.Dispose()
    return $bmp
}

$masterIcon = Render-Icon 1024
$masterFlag = Render-FlagOnly 1024

# --- Square logos (full icon) ---
(Compose-Centered $masterIcon 88 88 1.0)   | ForEach-Object { Save-Png $_ "$assets\Square44x44Logo.scale-200.png"; $_.Dispose() }
(Compose-Centered $masterIcon 300 300 1.0) | ForEach-Object { Save-Png $_ "$assets\Square150x150Logo.scale-200.png"; $_.Dispose() }
(Compose-Centered $masterIcon 50 50 1.0)   | ForEach-Object { Save-Png $_ "$assets\StoreLogo.png"; $_.Dispose() }

# --- Unplated / lock-screen (flag only, transparent) ---
(Compose-Centered $masterFlag 24 24 1.0) | ForEach-Object { Save-Png $_ "$assets\Square44x44Logo.targetsize-24_altform-unplated.png"; $_.Dispose() }
(Compose-Centered $masterFlag 48 48 1.0) | ForEach-Object { Save-Png $_ "$assets\Square44x44Logo.targetsize-48_altform-lightunplated.png"; $_.Dispose() }
(Compose-Centered $masterFlag 48 48 1.0) | ForEach-Object { Save-Png $_ "$assets\LockScreenLogo.scale-200.png"; $_.Dispose() }

# --- Wide tile + splash ---
(Render-Wide 620 300)                          | ForEach-Object { Save-Png $_ "$assets\Wide310x150Logo.scale-200.png"; $_.Dispose() }
(Compose-Centered $masterIcon 1240 600 0.55)   | ForEach-Object { Save-Png $_ "$assets\SplashScreen.scale-200.png"; $_.Dispose() }

# --- Multi-resolution .ico (PNG-encoded entries; Vista+ supports this) ---
$icoSizes = @(256, 128, 64, 48, 32, 24, 16)
$pngs = @()
foreach ($sz in $icoSizes) {
    $b = Compose-Centered $masterIcon $sz $sz 1.0
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , @{ size = $sz; bytes = $ms.ToArray() }
    $ms.Dispose(); $b.Dispose()
}
$icoPath = "$assets\AppIcon.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0)            # reserved
$bw.Write([uint16]1)            # type = icon
$bw.Write([uint16]$pngs.Count)  # image count
$offset = 6 + (16 * $pngs.Count)
foreach ($p in $pngs) {
    $dim = if ($p.size -ge 256) { 0 } else { $p.size }
    $bw.Write([byte]$dim)       # width
    $bw.Write([byte]$dim)       # height
    $bw.Write([byte]0)          # palette
    $bw.Write([byte]0)          # reserved
    $bw.Write([uint16]1)        # planes
    $bw.Write([uint16]32)       # bpp
    $bw.Write([uint32]$p.bytes.Length)
    $bw.Write([uint32]$offset)
    $offset += $p.bytes.Length
}
foreach ($p in $pngs) { $bw.Write($p.bytes) }
$bw.Flush(); $fs.Close()
Write-Host "wrote $icoPath ($($pngs.Count) sizes)"

# Save a 1024 master preview for visual review.
Save-Png $masterIcon "$env:TEMP\glasssweeper_icon_preview.png"

$masterIcon.Dispose(); $masterFlag.Dispose()
Write-Host "DONE"
