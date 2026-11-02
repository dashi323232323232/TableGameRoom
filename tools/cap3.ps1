Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class WU3 {
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, int e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  public delegate bool EnumProc(IntPtr h, IntPtr p);
  public const uint LD = 0x0002, LU = 0x0004;
  public static void Click() { mouse_event(LD,0,0,0,0); System.Threading.Thread.Sleep(60); mouse_event(LU,0,0,0,0); }
  public static List<long> Dialogs(uint pid) {
    var res = new List<long>();
    EnumWindows((h, p) => {
      uint wp; GetWindowThreadProcessId(h, out wp);
      if (wp == pid && IsWindowVisible(h)) {
        var cn = new StringBuilder(256); GetClassName(h, cn, 256);
        if (cn.ToString() == "#32770") res.Add(h.ToInt64());
      }
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
function Line([string]$pat) { (Texts | Where-Object { $_.Current.Name -like $pat } | Select-Object -First 1).Current.Name }
function Activate { [WU3]::SetForegroundWindow($h) | Out-Null; try { $win.SetFocus() } catch { }; Start-Sleep -Milliseconds 250 }

function ModalText {
    $dl = @([WU3]::Dialogs([uint32]$p.Id))
    if ($dl.Count -eq 0) { return $null }
    $el = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$dl[0])
    $tc = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
    $texts = @($el.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tc) | ForEach-Object { $_.Current.Name })
    return [pscustomobject]@{ El = $el; Text = ($texts -join ' / ') }
}
function CloseModal {
    $m = ModalText
    if (-not $m) { return '' }
    $bc = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
    $b = $m.El.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $bc)
    if ($b) { try { $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { } }
    Start-Sleep -Milliseconds 700
    return $m.Text
}
function ClickBtn([string]$part, [int]$index = 0) {
    Activate
    $f = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($f.Count -le $index) { "  MISS: $part"; return '' }
    $r = $f[$index].Current.BoundingRectangle
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
    Start-Sleep -Milliseconds 200
    [WU3]::Click()
    Start-Sleep -Milliseconds 800
    return (CloseModal)
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
    [WU3]::Click()
    Start-Sleep -Milliseconds 900
}
function SetStake([string]$v) {
    $e = All ([System.Windows.Automation.ControlType]::Edit) | Select-Object -First 1
    if (-not $e) { '  MISS 下注框'; return }
    $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($v)
    Start-Sleep -Milliseconds 250
}

'=== 德州扑克（双人桌 · 休闲档）==='
ClickBtn '选择智能模式' | Out-Null
"  大厅: $([bool](Line '*选择玩法*'))"
EnterCard '德州扑克'
ClickBtn '休闲：$10–$500' | Out-Null
ClickBtn '双人桌' | Out-Null
ClickBtn '开始游戏' | Out-Null
SetStake '500'
ClickBtn '开始牌局' | Out-Null
"  开局: $(Line '*你的下注*')"
ClickBtn '跟注 $500' | Out-Null
"  跟注1: $(Line '*你的下注*')"
ClickBtn '跟注 $500' | Out-Null
"  跟注2: $(Line '*你的下注*')"
$m = ClickBtn '加注 $1,000'
"  加注弹窗: $m"
"  加注后: $(Line '*你的下注*')"

'=== 炸金花（3-7 人 · 休闲档）==='
ClickBtn '返回游戏大厅' | Out-Null
EnterCard '炸金花'
ClickBtn '3–7 人' | Out-Null
ClickBtn '开始游戏' | Out-Null
SetStake '500'
ClickBtn '新开一局' | Out-Null
"  开局: $(Line '*你的下注*')"
ClickBtn '跟注 $500' | Out-Null
ClickBtn '跟注 $500' | Out-Null
"  跟注2: $(Line '*你的下注*')"
$m = ClickBtn '加注 $1,000'
"  加注弹窗: $m"
"  加注后: $(Line '*你的下注*')"

Stop-Process -Id $p.Id -Force
'DONE'
