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
function DismissModal {
    $pidCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
    $tops = @([System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children, $pidCond))
    $dialogs = @($tops | Where-Object { $_.Current.ClassName -ne 'Window' })
    foreach ($d in $dialogs) {
        $ok = $d.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::Button)))
        if ($ok) { try { $ok.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { } }
    }
    return $dialogs.Count
}
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
"  筛选页: $(HasText '筛选 · 德州扑克')"

ClickName '休闲：$10–$500' | Out-Null
"  档位已切到休闲: $(HasText '✓ 休闲')"
ClickName '双人桌' | Out-Null
"  双人桌: $(HasText '双人桌固定 2 人')"

ClickName '开始游戏' | Out-Null
Start-Sleep -Milliseconds 500
"  档位显示: $((Texts | Where-Object { $_.Current.Name -like '*档位：*' } | Select-Object -First 1).Current.Name)"

$edit = All ([System.Windows.Automation.ControlType]::Edit) | Select-Object -First 1
$edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('500')
"  下注框: $($edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value)"

ClickName '开始牌局' | Out-Null
"  牌局已开始: $(HasText '你的下注：$500')  弹窗数: $(DismissModal)"
"  下注行: $((Texts | Where-Object { $_.Current.Name -like '*你的下注*' } | Select-Object -First 1).Current.Name)"

$before = (Texts | Where-Object { $_.Current.Name -like '*你的下注*' } | Select-Object -First 1).Current.Name
ClickName '跟注 500' | Out-Null
"  跟注后弹窗数: $(DismissModal)"
$after = (Texts | Where-Object { $_.Current.Name -like '*你的下注*' } | Select-Object -First 1).Current.Name
"  跟注前: $before"
"  跟注后: $after"
"  未被累计上限拦截: $(-not (HasText '累计下注上限'))"

Stop-Process -Id $p.Id -Force
'DONE'
