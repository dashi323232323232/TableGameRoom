param([string]$Exe = 'D:\dsh\xin\bin\Release\net8.0-windows\TableGameRoom.exe')
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WB {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
}
"@
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400
$p = Start-Process $Exe -PassThru
Start-Sleep -Seconds 4
$p.Refresh()
$h = $p.MainWindowHandle
"hwnd=$h"
$wr = New-Object WB+RECT; [WB]::GetWindowRect($h, [ref]$wr) | Out-Null
$cr = New-Object WB+RECT; [WB]::GetClientRect($h, [ref]$cr) | Out-Null
$pt = New-Object WB+POINT; [WB]::ClientToScreen($h, [ref]$pt) | Out-Null
"window=$($wr.Left),$($wr.Top),$($wr.Right-$wr.Left)x$($wr.Bottom-$wr.Top) client=$($cr.Right)x$($cr.Bottom) clientOrigin=$($pt.X),$($pt.Y)"
$root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$btns = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
"buttons=$($btns.Count)"
foreach ($b in $btns) {
    $r = $b.Current.BoundingRectangle
    "  name='$($b.Current.Name)' rect=$([int]$r.X),$([int]$r.Y),$([int]$r.Width)x$([int]$r.Height) offscreen=$($b.Current.IsOffscreen) enabled=$($b.Current.IsEnabled)"
}
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
'DONE'
