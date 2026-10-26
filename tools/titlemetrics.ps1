param([string]$Tag = 'after', [string]$Exe = 'D:\dsh\xin\bin\Release\net8.0-windows\TableGameRoom.exe')
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WM2 {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref System.Drawing.Point p);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
}
"@
$outDir = 'D:\dsh\xin\tools\shots'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
$p = Start-Process $Exe -PassThru
Start-Sleep -Seconds 4
$p.Refresh()
$hwnd = [IntPtr]$p.MainWindowHandle
[WM2]::SetWindowPos($hwnd, [IntPtr]::Zero, 120, 90, 0, 0, 0x0001 -bor 0x0004) | Out-Null
[WM2]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 900
$p.Refresh(); $hwnd = [IntPtr]$p.MainWindowHandle
$wr = New-Object WM2+RECT; [WM2]::GetWindowRect($hwnd, [ref]$wr) | Out-Null
$cr = New-Object WM2+RECT; [WM2]::GetClientRect($hwnd, [ref]$cr) | Out-Null
$pt = New-Object System.Drawing.Point 0,0; [WM2]::ClientToScreen($hwnd, [ref]$pt) | Out-Null
$bw = $wr.Right - $wr.Left; $bh = $wr.Bottom - $wr.Top
"win=${bw}x${bh} at $($wr.Left),$($wr.Top) client=$($cr.Right)x$($cr.Bottom) clientOrigin=$($pt.X),$($pt.Y) frame=$($pt.X-$wr.Left),$($pt.Y-$wr.Top) style=0x{0:X8}" -f [WM2]::GetWindowLong($hwnd, -16)

$bmp = New-Object System.Drawing.Bitmap $bw, $bh
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($wr.Left, $wr.Top, 0, 0, (New-Object System.Drawing.Size $bw, $bh))
$g.Dispose()
$bmp.Save((Join-Path $outDir ("win-$Tag.png")), [System.Drawing.Imaging.ImageFormat]::Png)

function IsBright($c) { return ($c.R -gt 140 -and $c.G -gt 140 -and $c.B -gt 140) }
function IsNearWhite($c) { return ($c.R -gt 225 -and $c.G -gt 225 -and $c.B -gt 225) }

# 顶部白线：沿中线逐行
$cx = [int]($bw / 2)
$whiteRows = 0
$rowDesc = @()
for ($y = 0; $y -lt 16; $y++) {
  $c = $bmp.GetPixel($cx, $y)
  $rowDesc += ("y{0}=#{1:X2}{2:X2}{3:X2}" -f $y, $c.R, $c.G, $c.B)
  if (IsNearWhite $c) { $whiteRows++ } else { break }
}
"topRows(center): " + ($rowDesc -join ' ')
"topNearWhiteRows=$whiteRows"
# 左边白线：标题栏中线逐列
$ty = [int](21)
$whiteCols = 0
$colDesc = @()
for ($x = 0; $x -lt 16; $x++) {
  $c = $bmp.GetPixel($x, $ty)
  $colDesc += ("x{0}=#{1:X2}{2:X2}{3:X2}" -f $x, $c.R, $c.G, $c.B)
  if (IsNearWhite $c) { $whiteCols++ } else { break }
}
"leftCols(title-y): " + ($colDesc -join ' ')
"leftNearWhiteCols=$whiteCols"

# 标题栏按钮（UIA 区域顶部 50px 内的按钮）
$winEl = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$btns = @($winEl.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond) |
    Where-Object { $_.Current.BoundingRectangle.Top -lt ($wr.Top + 50) } |
    Sort-Object { $_.Current.BoundingRectangle.Left })
"titleButtons=$($btns.Count)"
$idx = 0
foreach ($b in $btns) {
  $r = $b.Current.BoundingRectangle
  $idx++
  "  btn{0} name='{1}' rect=({2},{3},{4}x{5})" -f $idx, $b.Current.Name, [int]$r.X, [int]$r.Y, [int]$r.Width, [int]$r.Height
  $bx0 = [int]($r.X - $wr.Left); $by0 = [int]($r.Y - $wr.Top)
  $bx1 = $bx0 + [int]$r.Width; $by1 = $by0 + [int]$r.Height
  $minX = 9999; $minY = 9999; $maxX = -1; $maxY = -1; $count = 0
  for ($yy = [Math]::Max(0, $by0); $yy -lt [Math]::Min($bh, $by1); $yy++) {
    for ($xx = [Math]::Max(0, $bx0); $xx -lt [Math]::Min($bw, $bx1); $xx++) {
      $c = $bmp.GetPixel($xx, $yy)
      if (IsBright $c) {
        $count++
        if ($xx -lt $minX) { $minX = $xx }; if ($xx -gt $maxX) { $maxX = $xx }
        if ($yy -lt $minY) { $minY = $yy }; if ($yy -gt $maxY) { $maxY = $yy }
      }
    }
  }
  if ($count -gt 0) {
    $gw = $maxX - $minX + 1; $gh = $maxY - $minY + 1
    $gcx = $minX + ($gw - 1) / 2.0; $gcy = $minY + ($gh - 1) / 2.0
    $bcx = $bx0 + ($r.Width - 1) / 2.0; $bcy = $by0 + ($r.Height - 1) / 2.0
    "    glyphPixels={0} glyphBox=({1},{2}) {3}x{4} centerOffset=({5:N1},{6:N1})" -f $count, $minX, $minY, $gw, $gh, ($gcx - $bcx), ($gcy - $bcy)
  } else {
    "    glyphPixels=0"
  }
}
$bmp.Dispose()
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
'DONE'
