Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WP {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref System.Drawing.Point p);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
}
"@
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400
$p = Start-Process 'D:\dsh\xin\bin\Release\net8.0-windows\TableGameRoom.exe' -PassThru
Start-Sleep -Seconds 3
$p.Refresh()
$h = $p.MainWindowHandle
$wr = New-Object WP+RECT; [WP]::GetWindowRect($h, [ref]$wr) | Out-Null
$cr = New-Object WP+RECT; [WP]::GetClientRect($h, [ref]$cr) | Out-Null
$pt = New-Object System.Drawing.Point 0,0; [WP]::ClientToScreen($h, [ref]$pt) | Out-Null
$ex = New-Object WP+RECT; $hr = [WP]::DwmGetWindowAttribute($h, 9, [ref]$ex, 16)
$style = [WP]::GetWindowLong($h, -16)
$exstyle = [WP]::GetWindowLong($h, -20)
"window rect : $($wr.Left),$($wr.Top) - $($wr.Right),$($wr.Bottom)  ($($wr.Right-$wr.Left)x$($wr.Bottom-$wr.Top))"
"client rect : $($cr.Right)x$($cr.Bottom)"
"client origin(screen): $($pt.X),$($pt.Y)"
"extended frame: $($ex.Left),$($ex.Top) - $($ex.Right),$($ex.Bottom) hr=$hr"
"style=0x{0:X8} exstyle=0x{1:X8}" -f $style, $exstyle
"dpi=$([WP]::GetDpiForWindow($h))"
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
'DONE'
