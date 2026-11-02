Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class M2 {
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, int e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  public const uint LD = 0x0002, LU = 0x0004;
  public static void Click() { mouse_event(LD,0,0,0,0); System.Threading.Thread.Sleep(60); mouse_event(LU,0,0,0,0); }
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
function Activate { [M2]::SetForegroundWindow($h) | Out-Null; try { $win.SetFocus() } catch { }; Start-Sleep -Milliseconds 300 }
function ClickBtn([string]$part, [int]$index = 0) {
    Activate
    $f = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($f.Count -le $index) { "  MISS: $part"; return }
    $r = $f[$index].Current.BoundingRectangle
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
    Start-Sleep -Milliseconds 250
    [M2]::Click()
    Start-Sleep -Milliseconds 900
}
function EnterCard([string]$title) {
    Activate
    $t = Texts | Where-Object { $_.Current.Name -like ('*' + $title + '*') } | Select-Object -First 1
    $r = $t.Current.BoundingRectangle
    $btn = Buttons | Where-Object { $_.Current.Name -like '*进入筛选*' -and $_.Current.BoundingRectangle.Top -ge $r.Top } |
        Sort-Object { [math]::Abs($_.Current.BoundingRectangle.Left - $r.Left) } | Select-Object -First 1
    $b = $btn.Current.BoundingRectangle
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]($b.X + $b.Width / 2), [int]($b.Y + $b.Height / 2))
    Start-Sleep -Milliseconds 250
    [M2]::Click()
    Start-Sleep -Milliseconds 900
}
function Modal {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
    $tops = @([System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $cond))
    $d = @($tops | Where-Object { $_.Current.ClassName -ne 'Window' })
    if ($d.Count -eq 0) { return $null }
    $tc = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
    $texts = @($d[0].FindAll([System.Windows.Automation.TreeScope]::Descendants, $tc) | ForEach-Object { $_.Current.Name })
    return [pscustomobject]@{ Win = $d[0]; Text = ($texts -join ' | ') }
}
function CloseModal {
    $m = Modal
    if (-not $m) { return '' }
    $bc = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
    $b = $m.Win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $bc)
    if ($b) { $r = $b.Current.BoundingRectangle; [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2)); [M2]::Click() }
    Start-Sleep -Milliseconds 700
    return $m.Text
}

ClickBtn '选择智能模式'
EnterCard '德州扑克'
ClickBtn '休闲：$10–$500'
ClickBtn '双人桌'
ClickBtn '开始游戏'
$e = All ([System.Windows.Automation.ControlType]::Edit) | Select-Object -First 1
$e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('1')
ClickBtn '开始牌局'
$mt = CloseModal
"  下注 1 的弹窗: $mt"
$e = All ([System.Windows.Automation.ControlType]::Edit) | Select-Object -First 1
$e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('500')
ClickBtn '开始牌局'
"  开局行: $((Texts | Where-Object { $_.Current.Name -like '*你的下注*' } | Select-Object -First 1).Current.Name)"
Stop-Process -Id $p.Id -Force
'DONE'
