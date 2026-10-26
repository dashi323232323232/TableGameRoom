Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

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
function Buttons { @(All ([System.Windows.Automation.ControlType]::Button)) }
function Texts { @(All ([System.Windows.Automation.ControlType]::Text)) }
function ClickBtn([string]$part, [int]$index = 0) {
    $f = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($f.Count -le $index) { "  MISS: $part"; return }
    try { $f[$index].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { "  ERR: $($_.Exception.Message)" }
    Start-Sleep -Milliseconds 900
}
function EnterCard([string]$title) {
    $t = Texts | Where-Object { $_.Current.Name -like ('*' + $title + '*') } | Select-Object -First 1
    $r = $t.Current.BoundingRectangle
    $btn = Buttons | Where-Object { $_.Current.Name -like '*进入筛选*' -and $_.Current.BoundingRectangle.Top -ge $r.Top } |
        Sort-Object { [math]::Abs($_.Current.BoundingRectangle.Left - $r.Left) } | Select-Object -First 1
    $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 900
}

ClickBtn '选择智能模式'
EnterCard '德州扑克'
ClickBtn '开始游戏'
$e = All ([System.Windows.Automation.ControlType]::Edit) | Select-Object -First 1
$e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('1')
ClickBtn '开始牌局'

$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$tops = @([System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $cond))
"  顶层窗口数: $($tops.Count)"
foreach ($t in $tops) {
    "   [$($t.Current.ClassName)] '$($t.Current.Name)'"
    $tc = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
    $t.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tc) | ForEach-Object { "        T: $($_.Current.Name)" }
}
Stop-Process -Id $p.Id -Force
'DONE'
