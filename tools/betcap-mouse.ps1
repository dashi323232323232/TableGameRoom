Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing

$exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 4
$p.Refresh()
$win = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

function All($type) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type)
    $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c)
}
function Buttons { @(All ([System.Windows.Automation.ControlType]::Button) | Sort-Object { $_.Current.BoundingRectangle.Top }, { $_.Current.BoundingRectangle.Left }) }
function Texts { @(All ([System.Windows.Automation.ControlType]::Text)) }
function Invoke-El($el) {
    try { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
    catch { Start-Sleep -Milliseconds 250; try { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { "    INVOKE-ERR: $($_.Exception.Message)" } }
    Start-Sleep -Milliseconds 900
}
function ClickName([string]$part, [int]$index = 0) {
    $f = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($f.Count -le $index) { "  MISS: $part"; return }
    Invoke-El $f[$index]
}
function EnterCard([string]$title) {
    $t = Texts | Where-Object { $_.Current.Name -like ('*' + $title + '*') } | Select-Object -First 1
    $r = $t.Current.BoundingRectangle
    $btn = Buttons | Where-Object { $_.Current.Name -like '*进入筛选*' -and $_.Current.BoundingRectangle.Top -ge $r.Top } |
        Sort-Object { [math]::Abs($_.Current.BoundingRectangle.Left - $r.Left) } | Select-Object -First 1
    Invoke-El $btn
}

ClickName '选择智能模式'
EnterCard '德州扑克'
ClickName '休闲：$10–$500'
ClickName '双人桌'
ClickName '开始游戏'
Start-Sleep -Milliseconds 500

$btn = Buttons | Where-Object { $_.Current.Name -like '*开始牌局*' } | Select-Object -First 1
"  按钮: '$($btn.Current.Name)' enabled=$($btn.Current.IsEnabled) rect=$($btn.Current.BoundingRectangle)"
"  patterns: $(($btn.GetSupportedPatterns() | ForEach-Object { $_.ProgrammaticName }) -join ', ')"

$edit = All ([System.Windows.Automation.ControlType]::Edit) | Select-Object -First 1
$edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('500')

# try real mouse click
$r = $btn.Current.BoundingRectangle
[System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
Start-Sleep -Milliseconds 300
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class M {
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, int e);
  public const uint LD = 0x0002, LU = 0x0004;
  public static void Click() { mouse_event(LD,0,0,0,0); System.Threading.Thread.Sleep(60); mouse_event(LU,0,0,0,0); }
}
"@
[M]::Click()
Start-Sleep -Milliseconds 1200

"  顶层窗口数: $(([System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)))).Count)"
"  下注行: $((Texts | Where-Object { $_.Current.Name -like '*你的下注*' } | Select-Object -First 1).Current.Name)"
'  按钮:'
Buttons | ForEach-Object { "    $($_.Current.Name)" }
Stop-Process -Id $p.Id -Force
'DONE'
