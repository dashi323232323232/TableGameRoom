param([string]$Exe = 'D:\dsh\xin\publish\TableGameRoom.exe')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class W {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  public const uint DOWN = 0x0002, UP = 0x0004;
}
"@
$script:fail = 0
function WinRect($hwnd) { $r = New-Object W+RECT; [void][W]::GetWindowRect($hwnd, [ref]$r); return $r }
function ClickAt($x, $y) {
  [void][W]::SetCursorPos($x, $y); Start-Sleep -Milliseconds 120
  [W]::mouse_event([W]::DOWN, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
  [W]::mouse_event([W]::UP, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 300
}
function FindBtn($hwnd, $name) {
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
  $cond = New-Object System.Windows.Automation.AndCondition(
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)),
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
  return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}
function BtnCenter($el) {
  $r = $el.Current.BoundingRectangle
  return @([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
}
function Check($name, $ok, $detail) {
  if ($ok) { Write-Output ("PASS  {0}  {1}" -f $name, $detail) }
  else { $script:fail++; Write-Output ("FAIL  {0}  {1}" -f $name, $detail) }
}
function TopPixel($hwnd) {
  $r = WinRect $hwnd
  $bmp = New-Object System.Drawing.Bitmap 1, 1
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen([int](($r.Left + $r.Right) / 2), $r.Top + 1, 0, 0, (New-Object System.Drawing.Size 1, 1))
  $c = $bmp.GetPixel(0, 0); $g.Dispose(); $bmp.Dispose()
  return ('#{0:X2}{1:X2}{2:X2}' -f $c.R, $c.G, $c.B)
}

$p = Start-Process -FilePath $Exe -PassThru
$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 60 -and $hwnd -eq [IntPtr]::Zero; $i++) { Start-Sleep -Milliseconds 250; $p.Refresh(); $hwnd = $p.MainWindowHandle }
Start-Sleep -Milliseconds 1200
[void][W]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 400
$r0 = WinRect $hwnd
Write-Output ("window0 = {0},{1} {2}x{3}" -f $r0.Left, $r0.Top, ($r0.Right - $r0.Left), ($r0.Bottom - $r0.Top))

# --- drag ---
$sx = $r0.Left + 600; $sy = $r0.Top + 20
[void][W]::SetCursorPos($sx, $sy); Start-Sleep -Milliseconds 150
[W]::mouse_event([W]::DOWN, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 120
for ($i = 1; $i -le 12; $i++) { [void][W]::SetCursorPos($sx + [int](120 * $i / 12), $sy + [int](80 * $i / 12)); Start-Sleep -Milliseconds 25 }
[W]::mouse_event([W]::UP, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 400
$r1 = WinRect $hwnd
$dx = $r1.Left - $r0.Left; $dy = $r1.Top - $r0.Top
$sameSize = (($r1.Right - $r1.Left) -eq ($r0.Right - $r0.Left)) -and (($r1.Bottom - $r1.Top) -eq ($r0.Bottom - $r0.Top))
Check 'drag-title' (($dx -ge 115) -and ($dx -le 125) -and ($dy -ge 75) -and ($dy -le 85) -and $sameSize) ("moved dx={0} dy={1} sizeUnchanged={2}" -f $dx, $dy, $sameSize)

# --- resize via right edge ---
$r2 = WinRect $hwnd
$w2 = $r2.Right - $r2.Left
$ex = $r2.Right - 3; $ey = $r2.Top + 400
[void][W]::SetCursorPos($ex, $ey); Start-Sleep -Milliseconds 150
[W]::mouse_event([W]::DOWN, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 120
for ($i = 1; $i -le 10; $i++) { [void][W]::SetCursorPos($ex + [int](100 * $i / 10), $ey); Start-Sleep -Milliseconds 25 }
[W]::mouse_event([W]::UP, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 400
$r3 = WinRect $hwnd
$w3 = $r3.Right - $r3.Left
$h3 = $r3.Bottom - $r3.Top
$cr = New-Object W+RECT; [void][W]::GetClientRect($hwnd, [ref]$cr)
$frame0 = (($cr.Right - $cr.Left) -eq $w3) -and (($cr.Bottom - $cr.Top) -eq $h3)
$topPx = TopPixel $hwnd
Check 'resize-right-edge' (($w3 - $w2) -ge 95) ("width {0} -> {1} (delta {2}) height {3}" -f $w2, $w3, ($w3 - $w2), $h3)
Check 'frame-zero-after-resize' $frame0 ("client={0}x{1} window={2}x{3}" -f ($cr.Right - $cr.Left), ($cr.Bottom - $cr.Top), $w3, $h3)
Check 'no-white-top-after-resize' ($topPx -eq '#202A30') ("top pixel {0}" -f $topPx)

# --- minimize ---
$minBtn = FindBtn $hwnd '最小化'
$mc = BtnCenter $minBtn
ClickAt $mc[0] $mc[1]
Start-Sleep -Milliseconds 600
$iconic = [W]::IsIconic($hwnd)
Check 'minimize' $iconic ("IsIconic={0} btn=({1},{2})" -f $iconic, $mc[0], $mc[1])

# --- restore ---
[void][W]::ShowWindow($hwnd, 9)
Start-Sleep -Milliseconds 900
[void][W]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 300
$r4 = WinRect $hwnd
$restored = -not [W]::IsIconic($hwnd)
$sameRect = ($r4.Left -eq $r3.Left) -and ($r4.Top -eq $r3.Top) -and ($r4.Right -eq $r3.Right) -and ($r4.Bottom -eq $r3.Bottom)
Check 'restore' ($restored -and $sameRect) ("IsIconic={0} rect={1},{2} {3}x{4}" -f (-not $restored), $r4.Left, $r4.Top, ($r4.Right - $r4.Left), ($r4.Bottom - $r4.Top))

# --- close ---
$closeBtn = FindBtn $hwnd '关闭'
$cc = BtnCenter $closeBtn
ClickAt $cc[0] $cc[1]
Start-Sleep -Milliseconds 800
$p.Refresh()
$exited = $p.HasExited
Check 'close' $exited ("HasExited={0} btn=({1},{2})" -f $exited, $cc[0], $cc[1])

Write-Output ("RESULT fail={0}" -f $script:fail)
if (-not $p.HasExited) { $p.Kill() }
exit $script:fail
