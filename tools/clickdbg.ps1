Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WUD {
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, int e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  public struct RECT { public int L, T, R, B; }
  public const uint LD = 0x0002, LU = 0x0004;
  public static void Click() { mouse_event(LD,0,0,0,0); System.Threading.Thread.Sleep(80); mouse_event(LU,0,0,0,0); }
}
"@
$exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 5
$p.Refresh()
$h = $p.MainWindowHandle
"hwnd: $h"
$r = New-Object WUD+RECT
[WUD]::GetWindowRect($h, [ref]$r) | Out-Null
"rect: $($r.L),$($r.T) - $($r.R),$($r.B)"
[WUD]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 400
"fg: $([WUD]::GetForegroundWindow())"
$win = [System.Windows.Automation.AutomationElement]::FromHandle($h)
$c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$btns = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c)
foreach ($b in $btns) {
  $br = $b.Current.BoundingRectangle
  "btn '$($b.Current.Name)' at $([int]$br.X),$([int]$br.Y) $([int]$br.Width)x$([int]$br.Height) off=$($b.Current.IsOffscreen) enabled=$($b.Current.IsEnabled)"
}
$t = $btns | Where-Object { $_.Current.Name -like '*选择智能模式*' } | Select-Object -First 1
$tr = $t.Current.BoundingRectangle
$cx = [int]($tr.X + $tr.Width / 2); $cy = [int]($tr.Y + $tr.Height / 2)
"click at $cx,$cy"
[System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point($cx, $cy)
Start-Sleep -Milliseconds 300
"cursor now: $([System.Windows.Forms.Cursor]::Position)"
[WUD]::Click()
Start-Sleep -Milliseconds 1500
$texts = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))
($texts | ForEach-Object { $_.Current.Name }) -join ' || '
Stop-Process -Id $p.Id -Force
'DONE'
