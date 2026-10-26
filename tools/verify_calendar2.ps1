$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class W {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
[void][W]::SetProcessDpiAwarenessContext([IntPtr](-4))

$out = 'D:\dsh\xin\tools\shots\calverify'
$log = Join-Path $out 'uia2.txt'
"== UIA2 $(Get-Date -Format o)" | Out-File -Encoding utf8 $log
$proc = Get-Process TableGameRoom -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { "FAIL: process not running" | Tee-Object -Append $log; exit 1 }
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
[void][W]::SetForegroundWindow([IntPtr]$win.Current.NativeWindowHandle)
Start-Sleep -Milliseconds 500

$wr = New-Object W+RECT
[void][W]::GetWindowRect([IntPtr]$win.Current.NativeWindowHandle, [ref]$wr)
"win $($wr.Left),$($wr.Top),$($wr.Right),$($wr.Bottom)" | Out-File -Append -Encoding utf8 $log

function Shot([string]$name) {
  $r = New-Object W+RECT
  [void][W]::GetWindowRect([IntPtr]$win.Current.NativeWindowHandle, [ref]$r)
  $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
  $bmp = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size $w, $h))
  $g.Dispose(); $bmp.Save((Join-Path $out $name), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  "shot=$name saved" | Out-File -Append -Encoding utf8 $log
}
function Invoke-El($el) {
  try { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
  catch { ("invoke-fail '$($el.Current.Name)' " + $_.Exception.Message) | Out-File -Append -Encoding utf8 $log }
  Start-Sleep -Milliseconds 400
}
function Get-BtnList {
  $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
  $r = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c)
  return @($r)
}
function Get-TextList {
  $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
  $r = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c)
  return @($r)
}
# enter mode
$m = Get-BtnList | Where-Object { $_.Current.Name -eq '选择普通模式' } | Select-Object -First 1
if ($m) { Invoke-El $m }
Shot 'u2_01_lobby.png'
$b = Get-BtnList
"lobby btn count=$($b.Count)" | Out-File -Append -Encoding utf8 $log
foreach ($x in $b) { ("B '$($x.Current.Name)' @$($x.Current.BoundingRectangle)") | Out-File -Append -Encoding utf8 $log }
foreach ($x in Get-TextList) { if ($x.Current.Name -match '年 \d+ 月|日历历史') { ("T '$($x.Current.Name)'") | Out-File -Append -Encoding utf8 $log } }

$next = Get-BtnList | Where-Object { $_.Current.Name -eq [string][char]0x203A } | Select-Object -First 1
$prev = Get-BtnList | Where-Object { $_.Current.Name -eq [string][char]0x2039 } | Select-Object -First 1
if ($next) { Invoke-El $next; Shot 'u2_02_next_month.png' }
$hdrs = (Get-TextList | Where-Object { $_.Current.Name -match '年 \d+ 月' } | ForEach-Object { $_.Current.Name }) -join ' | '
"after-next month text: $hdrs" | Out-File -Append -Encoding utf8 $log
$b = Get-BtnList
"after-next 返回大厅 count=" + (($b | Where-Object { $_.Current.Name -eq '返回大厅' }).Count) + " 游戏卡 count=" + (($b | Where-Object { $_.Current.Name -match '进入|开始|玩法' }).Count) + " btnTotal=$($b.Count)" | Out-File -Append -Encoding utf8 $log
if ($next) { Invoke-El $next; Shot 'u2_03_next2_month.png' }
if ($prev) { Invoke-El $prev; Invoke-El $prev; Shot 'u2_04_back_month.png' }
$hdrs = (Get-TextList | Where-Object { $_.Current.Name -match '年 \d+ 月' } | ForEach-Object { $_.Current.Name }) -join ' | '
"after-back month text: $hdrs" | Out-File -Append -Encoding utf8 $log

# click day 15
$d = Get-BtnList | Where-Object { $_.Current.Name -eq '15' -and $_.Current.BoundingRectangle.Left -gt ($wr.Left + 850) } | Select-Object -First 1
if ($d) { Invoke-El $d; Shot 'u2_05_day15.png' } else { "day15 right-rail not found" | Out-File -Append -Encoding utf8 $log }
$b = Get-BtnList
"after-day15 返回大厅 count=" + (($b | Where-Object { $_.Current.Name -eq '返回大厅' }).Count) + " btnTotal=$($b.Count)" | Out-File -Append -Encoding utf8 $log
foreach ($x in $b) { if ($x.Current.BoundingRectangle.Left -gt ($wr.Left + 850)) { ("RAIL B '$($x.Current.Name)' @$($x.Current.BoundingRectangle)") | Out-File -Append -Encoding utf8 $log } }
foreach ($x in Get-TextList) { if ($x.Current.Name -match '年 \d+ 月|次$|投入') { ("T2 '$($x.Current.Name)'") | Out-File -Append -Encoding utf8 $log } }

# segments
$segc = Get-BtnList | Where-Object { $_.Current.Name -eq '次数' } | Select-Object -First 1
if ($segc) { Invoke-El $segc; Shot 'u2_06_count.png' } else { "次数 not found" | Out-File -Append -Encoding utf8 $log }
$segf = Get-BtnList | Where-Object { $_.Current.Name -eq '流水' } | Select-Object -First 1
if ($segf) { Invoke-El $segf; Shot 'u2_07_turnover.png' } else { "流水 not found" | Out-File -Append -Encoding utf8 $log }

# hover empty day 1
$h = Get-BtnList | Where-Object { $_.Current.Name -eq '1' -and $_.Current.BoundingRectangle.Left -gt ($wr.Left + 850) } | Select-Object -First 1
if ($h) {
  $rr = $h.Current.BoundingRectangle
  $cx = [int]($rr.Left + $rr.Width / 2); $cy = [int]($rr.Top + $rr.Height / 2)
  [void][W]::SetCursorPos($cx, $cy); Start-Sleep -Milliseconds 300
  [void][W]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
  [void][W]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 2000
  $scr = [System.Windows.Forms.SystemInformation]::VirtualScreen
  $full = New-Object System.Drawing.Bitmap $scr.Width, $scr.Height
  $g = [System.Drawing.Graphics]::FromImage($full)
  $g.CopyFromScreen($scr.Left, $scr.Top, 0, 0, (New-Object System.Drawing.Size $scr.Width, $scr.Height))
  $g.Dispose(); $full.Save((Join-Path $out 'u2_08_hover_day1.png'), [System.Drawing.Imaging.ImageFormat]::Png); $full.Dispose()
  "hover day1 at $cx,$cy" | Out-File -Append -Encoding utf8 $log
  foreach ($x in Get-TextList) { if ($x.Current.Name -match '对局|流水|收入|消耗') { ("TOOLTIP: " + $x.Current.Name.Replace("`n",' / ')) | Out-File -Append -Encoding utf8 $log } }
  [void][W]::SetCursorPos(5, 5)
}
# geometry: rightmost/bottommost rail elements
$all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$maxRight = ($all | ForEach-Object { $_.Current.BoundingRectangle.Right } | Measure-Object -Maximum).Maximum
$maxBottom = ($all | ForEach-Object { $_.Current.BoundingRectangle.Bottom } | Measure-Object -Maximum).Maximum
"maxRight=$maxRight (content area right approx $($wr.Right - 6)) maxBottom=$maxBottom (content bottom approx $($wr.Bottom - 6))" | Out-File -Append -Encoding utf8 $log
"== DONE" | Out-File -Append -Encoding utf8 $log
Write-Output done
