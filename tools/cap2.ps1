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
function Line([string]$pat) { (Texts | Where-Object { $_.Current.Name -like $pat } | Select-Object -First 1).Current.Name }
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
    if ($b) { try { $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { } }
    Start-Sleep -Milliseconds 700
    return $m.Text
}
function ClickBtn([string]$part, [int]$index = 0) {
    $f = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($f.Count -le $index) { "  MISS: $part"; return }
    try { $f[$index].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
    catch { Start-Sleep -Milliseconds 300; try { $f[$index].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { "  INVOKE-ERR: $($_.Exception.Message)" } }
    Start-Sleep -Milliseconds 900
    $mt = CloseModal
    if ($mt) { "  [弹窗] $mt" }
}
function EnterCard([string]$title) {
    $t = Texts | Where-Object { $_.Current.Name -like ('*' + $title + '*') } | Select-Object -First 1
    $r = $t.Current.BoundingRectangle
    $btn = Buttons | Where-Object { $_.Current.Name -like '*进入筛选*' -and $_.Current.BoundingRectangle.Top -ge $r.Top } |
        Sort-Object { [math]::Abs($_.Current.BoundingRectangle.Left - $r.Left) } | Select-Object -First 1
    try { $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { "  INVOKE-ERR: $($_.Exception.Message)" }
    Start-Sleep -Milliseconds 900
}
function SetStake([string]$v) {
    $e = All ([System.Windows.Automation.ControlType]::Edit) | Select-Object -First 1
    if (-not $e) { '  MISS 下注框'; return }
    $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($v)
    Start-Sleep -Milliseconds 250
}

'=== 德州扑克（双人桌 · 休闲档）==='
ClickBtn '选择智能模式'
"  大厅: $([bool](Line '*选择玩法*'))"
EnterCard '德州扑克'
"  筛选页: $([bool](Line '*筛选 · 德州扑克*'))"
ClickBtn '休闲：$10–$500'
ClickBtn '双人桌'
ClickBtn '开始游戏'
"  牌桌: $([bool](Line '*阶段：翻牌前*'))"
SetStake '500'
ClickBtn '开始牌局'
"  开局后: $(Line '*你的下注*')"
ClickBtn '跟注 $500'
"  跟注1次: $(Line '*你的下注*')"
ClickBtn '跟注 $500'
"  跟注2次: $(Line '*你的下注*')"
$before = Line '*你的下注*'
ClickBtn '加注 $1,000'
"  加注后: $(Line '*你的下注*')"
"  累计 $1,500 未被单次上限拦截: $([bool]($before -like '*你的下注：$1,500*'))"

'=== 炸金花（3-7 人 · 休闲档）==='
ClickBtn '返回游戏大厅'
"  大厅: $([bool](Line '*选择玩法*'))"
EnterCard '炸金花'
"  筛选页: $([bool](Line '*筛选 · 炸金花*'))"
ClickBtn '3–7 人'
ClickBtn '开始游戏'
"  牌桌: $([bool](Line '*桌面筹码堆*'))"
SetStake '500'
ClickBtn '新开一局'
"  开局后: $(Line '*你的下注*')"
ClickBtn '跟注 $500'
ClickBtn '跟注 $500'
$before = Line '*你的下注*'
"  两次跟注后: $before"
ClickBtn '加注 $1,000'
"  加注后: $(Line '*你的下注*')"
"  累计 $1,500 未被单次上限拦截: $([bool]($before -like '*你的下注：$1,500*'))"

Stop-Process -Id $p.Id -Force
'DONE'
