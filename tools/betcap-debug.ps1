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
function Buttons { @(All ([System.Windows.Automation.ControlType]::Button) | Sort-Object { $_.Current.BoundingRectangle.Top }, { $_.Current.BoundingRectangle.Left }) }
function Texts { @(All ([System.Windows.Automation.ControlType]::Text)) }
function HasText([string]$n) { [bool](Texts | Where-Object { $_.Current.Name -like ('*' + $n + '*') } | Select-Object -First 1) }
function Invoke-El($el) {
    try { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
    catch { Start-Sleep -Milliseconds 250; $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
    Start-Sleep -Milliseconds 800
}
function ClickName([string]$part, [int]$index = 0) {
    $f = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($f.Count -le $index) { "  MISS: $part"; return $false }
    Invoke-El $f[$index]
    return $true
}
function EnterCard([string]$title) {
    $t = Texts | Where-Object { $_.Current.Name -like ('*' + $title + '*') } | Select-Object -First 1
    $r = $t.Current.BoundingRectangle
    $btn = Buttons | Where-Object { $_.Current.Name -like '*进入筛选*' -and $_.Current.BoundingRectangle.Top -ge $r.Top } |
        Sort-Object { [math]::Abs($_.Current.BoundingRectangle.Left - $r.Left) } | Select-Object -First 1
    Invoke-El $btn
}

ClickName '选择智能模式' | Out-Null
EnterCard '德州扑克'
ClickName '双人桌' | Out-Null
ClickName '开始游戏' | Out-Null
Start-Sleep -Milliseconds 600

$edits = @(All ([System.Windows.Automation.ControlType]::Edit))
"  下注框数量: $($edits.Count)"
foreach ($e in $edits) {
    $v = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    "    当前值: '$v'"
    $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('500')
}
Start-Sleep -Milliseconds 300
foreach ($e in $edits) {
    $v = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    "    设置后: '$v'"
}

'  按钮列表:'
Buttons | ForEach-Object { "    $($_.Current.Name)" }

ClickName '开始牌局' | Out-Null
Start-Sleep -Milliseconds 600

'  顶层窗口:'
[System.Windows.Automation.AutomationElement]::RootElement.FindAll(
    [System.Windows.Automation.TreeScope]::Children,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id))) |
    ForEach-Object { "    [$($_.Current.ClassName)] '$($_.Current.Name)'" }

'  点击后按钮:'
Buttons | ForEach-Object { "    $($_.Current.Name)" }
'  文本(前12):'
Texts | Select-Object -First 12 | ForEach-Object { "    $($_.Current.Name)" }

Stop-Process -Id $p.Id -Force
'DONE'
