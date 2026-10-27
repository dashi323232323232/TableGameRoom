param([string]$Tag = 'before', [string]$Exe = 'D:\dsh\xin\bin\Release\net8.0-windows\TableGameRoom.exe')
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WT {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
"@
$outDir = 'D:\dsh\xin\tools\shots'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
$p = Start-Process $Exe -PassThru
Start-Sleep -Seconds 4
$p.Refresh()
$h = $p.MainWindowHandle
[WT]::SetWindowPos($h, [IntPtr]::Zero, 120, 90, 0, 0, 0x0001 -bor 0x0004) | Out-Null  # NOSIZE|NOZORDER
Start-Sleep -Milliseconds 300
[WT]::SetForegroundWindow($h) | Out-Null
# 置顶，避免被其他窗口遮挡导致截图混入别的窗口内容
[WT]::SetWindowPos($h, [IntPtr](-1), 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0004) | Out-Null
Start-Sleep -Milliseconds 700
$p.Refresh()
$h = $p.MainWindowHandle
$r = New-Object WT+RECT
[WT]::GetWindowRect($h, [ref]$r) | Out-Null
$dpi = [WT]::GetDpiForWindow($h)
$scale = $dpi / 96.0
$w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
"hwnd=$h rect=$($r.Left),$($r.Top),$w x $ht dpi=$dpi scale=$scale"

# 截取窗口外扩 8px，便于看到窗口边缘之外
$pad = 8
$x0 = $r.Left - $pad; $y0 = $r.Top - $pad
$cw = $w + $pad * 2; $ch = $ht + $pad * 2
$bmp = New-Object System.Drawing.Bitmap $cw, $ch
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($x0, $y0, 0, 0, (New-Object System.Drawing.Size $cw, $ch))
$g.Dispose()
$full = Join-Path $outDir ("title-$Tag-full.png")
$bmp.Save($full, [System.Drawing.Imaging.ImageFormat]::Png)

# 标题栏放大 4 倍（最近邻，便于量像素）
$titleH = [int](46 * $scale)
$crop = New-Object System.Drawing.Bitmap ($cw * 4), ($titleH * 4)
$g2 = [System.Drawing.Graphics]::FromImage($crop)
$g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g2.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g2.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, ($cw * 4), ($titleH * 4)), (New-Object System.Drawing.Rectangle 0, 0, $cw, $titleH), [System.Drawing.GraphicsUnit]::Pixel)
$g2.Dispose()
$zoom = Join-Path $outDir ("title-$Tag-zoom.png")
$crop.Save($zoom, [System.Drawing.Imaging.ImageFormat]::Png)
$crop.Dispose()

# 采样窗口顶边附近的像素颜色（窗口内 x=中心、x=左边+20）
$cx = $pad + [int]($w / 2)
$samples = @()
foreach ($dy in 0..8) {
  $y = $pad + $dy
  $c = $bmp.GetPixel($cx, $y)
  $samples += ("row+{0} #{1:X2}{2:X2}{3:X2}" -f $dy, $c.R, $c.G, $c.B)
}
"pixels(center-x): " + ($samples -join '  ')
$titleColor = $bmp.GetPixel($cx, $pad + 20)
"title@+20 #{0:X2}{1:X2}{2:X2}" -f $titleColor.R, $titleColor.G, $titleColor.B
if ($titleColor.R -gt 120 -and $titleColor.G -gt 120) { "WARN: 标题栏位置疑似被遮挡（不是深色）" }
$bmp.Dispose()

# 截整个屏幕，便于观察桌面环境
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$scr = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$gs = [System.Drawing.Graphics]::FromImage($scr)
$gs.CopyFromScreen($b.X, $b.Y, 0, 0, $scr.Size)
$gs.Dispose()
$scr.Save((Join-Path $outDir ("desktop-$Tag.png")), [System.Drawing.Imaging.ImageFormat]::Png)
$scr.Dispose()

"full=$full"
"zoom=$zoom"
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
'DONE'
