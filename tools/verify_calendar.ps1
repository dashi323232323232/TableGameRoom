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
New-Item -ItemType Directory -Force -Path $out | Out-Null
$log = Join-Path $out 'uia.txt'
"== UIA $(Get-Date -Format o)" | Out-File -Encoding utf8 $log

$proc = Get-Process TableGameRoom -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { "FAIL: process not running" | Tee-Object -Append $log; exit 1 }

# second instance test
$second = Start-Process 'D:\dsh\xin\publish\TableGameRoom.exe' -PassThru
$sw = [Diagnostics.Stopwatch]::StartNew()
$exited = $second.WaitForExit(8000)
$sw.Stop()
"second-instance exited=$exited ms=$($sw.ElapsedMilliseconds) code=$(if($exited){$second.ExitCode}else{'running'})" | Out-File -Append -Encoding utf8 $log
if (-not $exited) { try { $second.Kill() } catch {} }

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { "FAIL: window not found" | Tee-Object -Append $log; exit 1 }
$wr = New-Object W+RECT
[void][W]::GetWindowRect([IntPtr]$win.Current.NativeWindowHandle, [ref]$wr)
"win rect $($wr.Left),$($wr.Top),$($wr.Right),$($wr.Bottom) size $($wr.Right-$wr.Left)x$($wr.Bottom-$wr.Top)" | Out-File -Append -Encoding utf8 $log

$btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
foreach ($b in $all) { "BTN '$($b.Current.Name)' @$($b.Current.BoundingRectangle)" | Out-File -Append -Encoding utf8 $log }

function Shot([string]$name) {
  $r = New-Object W+RECT
  [void][W]::GetWindowRect([IntPtr]$win.Current.NativeWindowHandle, [ref]$r)
  $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
  $bmp = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size $w, $h))
  $g.Dispose()
  $bmp.Save((Join-Path $out $name), [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  "shot=$name" | Out-File -Append -Encoding utf8 $log
}

function Invoke-Btn($el) {
  try { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
  catch { "invoke-fail $($el.Current.Name)" | Out-File -Append -Encoding utf8 $log }
  Start-Sleep -Milliseconds 350
}

[void][W]::SetForegroundWindow([IntPtr]$win.Current.NativeWindowHandle)
Start-Sleep -Milliseconds 400
Shot 'cal_01_initial.png'

# enter 存档 mode if present
$m = $all | Where-Object { $_.Current.Name -match '存档|继续' } | Select-Object -First 1
if ($m) { Invoke-Btn $m; Shot 'cal_02_lobby.png' } else { "no mode button, assume lobby" | Out-File -Append -Encoding utf8 $log }

function Get-Buttons { (($win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $btnCond)) | ForEach-Object { $_ }) }
function Click-Day([string]$n) {
  $bs = Get-Buttons
  $cand = $bs | Where-Object { $_.Current.Name -eq $n -and $_.Current.BoundingRectangle.Top -gt ($wr.Top + 200) }
  $c = $cand | Sort-Object { $_.Current.BoundingRectangle.Top } | Select-Object -Last 1
  return $c
}

# month nav twice then back
$nav = (Get-Buttons) | Where-Object { $_.Current.Name -in @([char]0x203A, [char]0x2039) }
"nav count=$($nav.Count)" | Out-File -Append -Encoding utf8 $log
$next = $nav | Where-Object { $_.Current.Name -eq [char]0x203A } | Select-Object -First 1
$prev = $nav | Where-Object { $_.Current.Name -eq [char]0x2039 } | Select-Object -First 1
Invoke-Btn $next; Shot 'cal_03_next_month.png'
$b = Get-Buttons
"after-next headers: " + (($b | Where-Object { $_.Current.Name -match '^\d{4} 年' } | ForEach-Object { $_.Current.Name }) -join ',') | Out-File -Append -Encoding utf8 $log
"after-next 返回大厅 count=" + (($b | Where-Object { $_.Current.Name -eq '返回大厅' }).Count) | Out-File -Append -Encoding utf8 $log
Invoke-Btn $prev; Shot 'cal_04_prev_back.png'
$b = Get-Buttons
"after-prev headers: " + (($b | Where-Object { $_.Current.Name -match '^\d{4} 年' } | ForEach-Object { $_.Current.Name }) -join ',') | Out-File -Append -Encoding utf8 $log
"after-prev 返回大厅 count=" + (($b | Where-Object { $_.Current.Name -eq '返回大厅' }).Count) | Out-File -Append -Encoding utf8 $log

# click a day -> should NOT jump page
$d = Click-Day '15'
if ($d) { Invoke-Btn $d; Shot 'cal_05_day15.png' } else { "day15 not found" | Out-File -Append -Encoding utf8 $log }
$b = Get-Buttons
"after-day15 返回大厅 count=" + (($b | Where-Object { $_.Current.Name -eq '返回大厅' }).Count) + " 日历历史 count=" + (($b | Where-Object { $_.Current.Name -eq '日历历史' }).Count) | Out-File -Append -Encoding utf8 $log

# segments
$seg = Get-Buttons | Where-Object { $_.Current.Name -eq '次数' } | Select-Object -First 1
if ($seg) { Invoke-Btn $seg; Shot 'cal_06_count_mode.png' } else { "次数 segment not found" | Out-File -Append -Encoding utf8 $log }
$seg2 = Get-Buttons | Where-Object { $_.Current.Name -eq '流水' } | Select-Object -First 1
if ($seg2) { Invoke-Btn $seg2; Shot 'cal_07_turnover_mode.png' } else { "流水 segment not found" | Out-File -Append -Encoding utf8 $log }

# hover empty day -> tooltip screenshot
$h = Click-Day '1'
if ($h) {
  $rr = $h.Current.BoundingRectangle
  $cx = [int]($rr.Left + $rr.Width / 2); $cy = [int]($rr.Top + $rr.Height / 2)
  [void][W]::SetCursorPos($cx, $cy)
  [void][W]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
  [void][W]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 1800
  $scr = [System.Windows.Forms.SystemInformation]::VirtualScreen
  $full = New-Object System.Drawing.Bitmap $scr.Width, $scr.Height
  $g = [System.Drawing.Graphics]::FromImage($full)
  $g.CopyFromScreen($scr.Left, $scr.Top, 0, 0, (New-Object System.Drawing.Size $scr.Width, $scr.Height))
  $g.Dispose()
  $full.Save((Join-Path $out 'cal_08_hover_tooltip.png'), [System.Drawing.Imaging.ImageFormat]::Png)
  $full.Dispose()
  "hover tooltip at $cx,$cy" | Out-File -Append -Encoding utf8 $log
  $tipEl = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))
  $tc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '')
  $txts = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))
  ($txts | ForEach-Object { if ($_.Current.Name -match '对局|流水|收入|消耗') { 'TOOLTIPTEXT: ' + $_.Current.Name } }) | Out-File -Append -Encoding utf8 $log
  # move away
  [void][W]::SetCursorPos(10, 10)
}

# measure right rail geometry via UIA elements in right column
$allEls = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$right = $allEls | Where-Object { $_.Current.BoundingRectangle.Left -gt ($wr.Left + 830) -and $_.Current.Name -ne '' }
foreach ($e in $right) { ("RIGHT '$($e.Current.Name)' type=$($e.Current.ControlType.ProgrammaticName) rect=$($e.Current.BoundingRectangle)") | Out-File -Append -Encoding utf8 $log }
"win bottom=$($wr.Bottom) right=$($wr.Right)" | Out-File -Append -Encoding utf8 $log
"== DONE" | Out-File -Append -Encoding utf8 $log
Write-Output "done"
