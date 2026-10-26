Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class WU8 {
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, int e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr h, EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  public delegate bool EnumProc(IntPtr h, IntPtr p);
  public const uint LD = 0x0002, LU = 0x0004;
  public static void Click() { mouse_event(LD,0,0,0,0); System.Threading.Thread.Sleep(60); mouse_event(LU,0,0,0,0); }
  public static IntPtr Dialog(uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, p) => {
      uint wp; GetWindowThreadProcessId(h, out wp);
      if (wp == pid && IsWindowVisible(h)) {
        var cn = new StringBuilder(256); GetClassName(h, cn, 256);
        if (cn.ToString() == "#32770") { found = h; return false; }
      }
      return true;
    }, IntPtr.Zero);
    return found;
  }
  public static List<string> ChildTexts(IntPtr dlg) {
    var res = new List<string>();
    EnumChildWindows(dlg, (h, p) => {
      var cn = new StringBuilder(256); GetClassName(h, cn, 256);
      var tt = new StringBuilder(1024); GetWindowText(h, tt, 1024);
      if (tt.Length > 0 && cn.ToString() != "Button") res.Add(tt.ToString());
      return true;
    }, IntPtr.Zero);
    return res;
  }
}
"@

$exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 4
$p.Refresh()
$h = $p.MainWindowHandle
$win = [System.Windows.Automation.AutomationElement]::FromHandle($h)

function All($type) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type)
    $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c)
}
function Buttons { @(All ([System.Windows.Automation.ControlType]::Button)) }
function Texts { @(All ([System.Windows.Automation.ControlType]::Text)) }
function Activate { [WU8]::SetForegroundWindow($h) | Out-Null; try { $win.SetFocus() } catch { }; Start-Sleep -Milliseconds 250 }
function CloseModal {
    $d = [WU8]::Dialog([uint32]$p.Id)
    if ($d -eq [IntPtr]::Zero) { return '' }
    $t = ([WU8]::ChildTexts($d) -join ' / ')
    [WU8]::SetForegroundWindow($d) | Out-Null
    Start-Sleep -Milliseconds 250
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
    Start-Sleep -Milliseconds 700
    return $t
}
function ClickBtn([string]$part, [int]$index = 0) {
    Activate
    $f = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($f.Count -le $index) { "  MISS: $part"; return }
    $r = $f[$index].Current.BoundingRectangle
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
    Start-Sleep -Milliseconds 200
    [WU8]::Click()
    Start-Sleep -Milliseconds 900
    $m = CloseModal
    if ($m) { "  [弹窗] $m" }
}
function EnterCard([string]$title) {
    Activate
    $t = Texts | Where-Object { $_.Current.Name -like ('*' + $title + '*') } | Select-Object -First 1
    $r = $t.Current.BoundingRectangle
    $btn = Buttons | Where-Object { $_.Current.Name -like '*进入筛选*' -and $_.Current.BoundingRectangle.Top -ge $r.Top } |
        Sort-Object { [math]::Abs($_.Current.BoundingRectangle.Left - $r.Left) } | Select-Object -First 1
    $b = $btn.Current.BoundingRectangle
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]($b.X + $b.Width / 2), [int]($b.Y + $b.Height / 2))
    Start-Sleep -Milliseconds 200
    [WU8]::Click()
    Start-Sleep -Milliseconds 900
}

ClickBtn '选择智能模式'
EnterCard '21'
ClickBtn '休闲：$10–$500'
ClickBtn '开始游戏'
$e = All ([System.Windows.Automation.ControlType]::Edit) | Select-Object -First 1
$e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('500')
ClickBtn '开始新局'
'  文本:'
Texts | ForEach-Object { "    $($_.Current.Name)" }
'  按钮:'
Buttons | ForEach-Object { "    $($_.Current.Name)" }
Stop-Process -Id $p.Id -Force
'DONE'
