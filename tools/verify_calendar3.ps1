$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace W3 -Name U -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@
[void][W3.U]::SetProcessDpiAwarenessContext([IntPtr](-4))
$out = 'D:\dsh\xin\tools\shots\calverify'
$log = Join-Path $out 'uia3.txt'
"== UIA3 $(Get-Date -Format o)" | Out-File -Encoding utf8 $log
$proc = Get-Process TableGameRoom -ErrorAction SilentlyContinue | Select-Object -First 1
$root = [System.Windows.Automation.AutomationElement]::RootElement
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)))
[void][W3.U]::SetForegroundWindow([IntPtr]$win.Current.NativeWindowHandle); Start-Sleep -Milliseconds 600
$wr = New-Object W3.U+RECT; [void][W3.U]::GetWindowRect([IntPtr]$win.Current.NativeWindowHandle, [ref]$wr)
"win $($wr.Left),$($wr.Top),$($wr.Right),$($wr.Bottom)" | Out-File -Append -Encoding utf8 $log
function Get-BtnList { return @($win.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))) }
function Invoke-El($el) { try { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { ("invoke-fail " + $_.Exception.Message) | Out-File -Append -Encoding utf8 $log }; Start-Sleep -Milliseconds 400 }
function Shot([string]$name) { $r = New-Object W3.U+RECT; [void][W3.U]::GetWindowRect([IntPtr]$win.Current.NativeWindowHandle, [ref]$r); $w = $r.Right-$r.Left; $h = $r.Bottom-$r.Top; $bmp = New-Object System.Drawing.Bitmap $w,$h; $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($r.Left,$r.Top,0,0,(New-Object System.Drawing.Size $w,$h)); $g.Dispose(); $bmp.Save((Join-Path $out $name),[System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose(); "shot=$name" | Out-File -Append -Encoding utf8 $log }

$m = Get-BtnList | Where-Object { $_.Current.Name -eq '选择普通模式' } | Select-Object -First 1
if ($m) { Invoke-El $m }
Start-Sleep -Milliseconds 600
$b = Get-BtnList
$rail = @($b | Where-Object { $_.Current.Name -match '^\d+$' -or $_.Current.Name -in @('流水','次数',([string][char]0x2039),([string][char]0x203A)) })
"rail count=$($rail.Count)" | Out-File -Append -Encoding utf8 $log
$maxR = ($rail | ForEach-Object { $_.Current.BoundingRectangle.Right } | Measure-Object -Maximum).Maximum
$maxB = ($rail | ForEach-Object { $_.Current.BoundingRectangle.Bottom } | Measure-Object -Maximum).Maximum
"railMaxRight=$maxR railMaxBottom=$maxB winInnerRight=$($wr.Right-8) winInnerBottom=$($wr.Bottom-8)" | Out-File -Append -Encoding utf8 $log
$all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$visBeyondR = @($all | Where-Object { $r = $_.Current.BoundingRectangle; -not $r.IsEmpty -and $r.Right -gt ($wr.Right - 8) -and $r.Left -lt $wr.Right })
"elements whose right edge exceeds inner right: $($visBeyondR.Count)" | Out-File -Append -Encoding utf8 $log
foreach ($e in $visBeyondR) { ("OOB '$($e.Current.Name)' $($e.Current.ControlType.ProgrammaticName) $($e.Current.BoundingRectangle)") | Out-File -Append -Encoding utf8 $log }
# is right rail clipped vertically: check whether any rail element bottom > scrollviewer viewport; find scrollbar presence
$sb = @($all | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.ScrollBar' })
"scrollbars=$($sb.Count)" | Out-File -Append -Encoding utf8 $log
foreach ($s in $sb) { ("SB $($s.Current.BoundingRectangle)") | Out-File -Append -Encoding utf8 $log }
Shot 'u3_01_lobby.png'

# month nav
$next = $rail | Where-Object { $_.Current.Name -eq [string][char]0x203A } | Select-Object -First 1
Invoke-El $next
$b2 = Get-BtnList
$mt = @($win.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))) | Where-Object { $_.Current.Name -match '年 \d+ 月' }
$mts = ($mt | ForEach-Object { $_.Current.Name }) -join ','
"after next: monthTexts=$mts btnTotal=$($b2.Count) cards=$(@($b2 | Where-Object { $_.Current.Name -eq '进入筛选 →' }).Count)" | Out-File -Append -Encoding utf8 $log
Shot 'u3_02_next.png'
Invoke-El $next; Invoke-El $next
$b3 = Get-BtnList
"after next x3: btnTotal=$($b3.Count) cards=" + (@($b3 | Where-Object { $_.Current.Name -eq '进入筛选 →' }).Count) | Out-File -Append -Encoding utf8 $log
# back 3 months
$prev = Get-BtnList | Where-Object { $_.Current.Name -eq [string][char]0x2039 } | Select-Object -First 1
$prev = (Get-BtnList | Where-Object { $_.Current.Name -eq '流水' } | Select-Object -First 1).Current.BoundingRectangle
Invoke-El ((Get-BtnList | Where-Object { $_.Current.Name -eq [string][char]0x2039 } | Select-Object -First 1))
Start-Sleep -Milliseconds 300
Invoke-El ((Get-BtnList | Where-Object { $_.Current.Name -eq [string][char]0x2039 } | Select-Object -First 1))
Start-Sleep -Milliseconds 300
Invoke-El ((Get-BtnList | Where-Object { $_.Current.Name -eq [string][char]0x2039 } | Select-Object -First 1))
$b4 = Get-BtnList
"after prev x3: btnTotal=$($b4.Count) cards=" + (@($b4 | Where-Object { $_.Current.Name -eq '进入筛选 →' }).Count) + " 返回大厅=" + (@($b4 | Where-Object { $_.Current.Name -eq '返回大厅' }).Count) | Out-File -Append -Encoding utf8 $log
Shot 'u3_03_back3.png'

# click day 15, screenshot, verify no page jump
$d = Get-BtnList | Where-Object { $_.Current.Name -eq '15' } | Select-Object -First 1
if ($d) { Invoke-El $d }
Start-Sleep -Milliseconds 500
$b5 = Get-BtnList
"after day15: btnTotal=$($b5.Count) cards=" + (@($b5 | Where-Object { $_.Current.Name -eq '进入筛选 →' }).Count) + " 返回大厅=" + (@($b5 | Where-Object { $_.Current.Name -eq '返回大厅' }).Count) | Out-File -Append -Encoding utf8 $log
Shot 'u3_04_day15.png'

# count mode + click 14
$seg = Get-BtnList | Where-Object { $_.Current.Name -eq '次数' } | Select-Object -First 1
Invoke-El $seg
$d14 = Get-BtnList | Where-Object { $_.Current.Name -eq '14' } | Select-Object -First 1
if ($d14) { Invoke-El $d14 }
Shot 'u3_05_count_day14.png'
Invoke-El (Get-BtnList | Where-Object { $_.Current.Name -eq '流水' } | Select-Object -First 1)
Shot 'u3_06_turnover_day14.png'

# hover empty day 20 (all october empty here)
$h = Get-BtnList | Where-Object { $_.Current.Name -eq '20' } | Select-Object -First 1
if ($h) {
  $rr = $h.Current.BoundingRectangle
  $cx = [int]($rr.Left + $rr.Width/2); $cy = [int]($rr.Top + $rr.Height/2)
  [void][W3.U]::SetCursorPos($cx,$cy); Start-Sleep -Milliseconds 500
  [void][W3.U]::mouse_event(0x0002,0,0,0,[UIntPtr]::Zero); [void][W3.U]::mouse_event(0x0004,0,0,0,[UIntPtr]::Zero)
  Start-Sleep -Milliseconds 2500
  $scr = [System.Windows.Forms.SystemInformation]::VirtualScreen
  $full = New-Object System.Drawing.Bitmap $scr.Width,$scr.Height
  $g = [System.Drawing.Graphics]::FromImage($full); $g.CopyFromScreen($scr.Left,$scr.Top,0,0,(New-Object System.Drawing.Size $scr.Width,$scr.Height)); $g.Dispose()
  $full.Save((Join-Path $out 'u3_07_hover20.png'),[System.Drawing.Imaging.ImageFormat]::Png); $full.Dispose()
  "hover day20 at $cx,$cy" | Out-File -Append -Encoding utf8 $log
  # capture cell pixel colors while hovering off
  [void][W3.U]::SetCursorPos(5,5)
}
# sample pixel colors of calendar cells (day cells) from last window shot
$r = New-Object W3.U+RECT; [void][W3.U]::GetWindowRect([IntPtr]$win.Current.NativeWindowHandle, [ref]$r)
$bmp2 = New-Object System.Drawing.Bitmap ($r.Right-$r.Left),($r.Bottom-$r.Top)
$g2 = [System.Drawing.Graphics]::FromImage($bmp2); $g2.CopyFromScreen($r.Left,$r.Top,0,0,(New-Object System.Drawing.Size ($r.Right-$r.Left),($r.Bottom-$r.Top))); $g2.Dispose()
$bmp2.Save((Join-Path $out 'u3_08_final.png'),[System.Drawing.Imaging.ImageFormat]::Png)
foreach ($nm in @('1','9','14','20','31')) {
  $el = Get-BtnList | Where-Object { $_.Current.Name -eq $nm } | Select-Object -First 1
  if ($el) { $br = $el.Current.BoundingRectangle; $px = [int]($br.Left+5); $py = [int]($br.Top+5); $c = $bmp2.GetPixel($px-$r.Left, $py-$r.Top); "cell $nm corner px=#$($c.R.ToString('X2'))$($c.G.ToString('X2'))$($c.B.ToString('X2'))" | Out-File -Append -Encoding utf8 $log }
}
$bmp2.Dispose()
"== DONE" | Out-File -Append -Encoding utf8 $log
Write-Output done
