# Captures the GlassSweeper window to a PNG with TRANSPARENT rounded corners, so
# the README image shows the window's rounded corners cleanly on any background
# (no wallpaper showing in the corners, no rectangular crop).
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT r, int size);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$out = $args[0]
$proc = Get-Process | Where-Object { $_.ProcessName -like '*GlassSweeper*' -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $proc) { Write-Error "GlassSweeper window not found"; exit 1 }
$hwnd = $proc.MainWindowHandle
Write-Host "hwnd=$hwnd title=$($proc.MainWindowTitle)"
[Win]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 400

# DWMWA_EXTENDED_FRAME_BOUNDS = 9 -> visible bounds (excludes the drop shadow).
$r = New-Object Win+RECT
[Win]::DwmGetWindowAttribute($hwnd, 9, [ref]$r, [System.Runtime.InteropServices.Marshal]::SizeOf($r)) | Out-Null
$w = $r.Right - $r.Left
$h = $r.Bottom - $r.Top
Write-Host "window bounds: $($r.Left),$($r.Top) ${w}x${h}"

$shot = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($shot)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$g.Dispose()

# Mask to a rounded rectangle (Windows 11 top-level corner radius ~8px) with an
# antialiased edge, leaving the corners transparent.
$radius = 8
$result = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$rg = [System.Drawing.Graphics]::FromImage($result)
$rg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$rg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$d = $radius * 2
$path.AddArc(0, 0, $d, $d, 180, 90)
$path.AddArc($w - $d, 0, $d, $d, 270, 90)
$path.AddArc($w - $d, $h - $d, $d, $d, 0, 90)
$path.AddArc(0, $h - $d, $d, $d, 90, 90)
$path.CloseFigure()
$brush = New-Object System.Drawing.TextureBrush($shot)
$rg.FillPath($brush, $path)
$brush.Dispose(); $path.Dispose(); $rg.Dispose()

$result.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$result.Dispose(); $shot.Dispose()
Write-Host "wrote $out"
