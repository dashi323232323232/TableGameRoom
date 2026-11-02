Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms

$exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
$outDir = 'D:\dsh\xin\tools\shots'
$crash = Join-Path $env:LOCALAPPDATA 'TableGameRoom\crash.log'
$save = Join-Path $env:LOCALAPPDATA 'TableGameRoom\progress.json'
$custom = Join-Path $env:LOCALAPPDATA 'TableGameRoom\custom-progress.json'

$json = Get-Content $save -Raw | ConvertFrom-Json
"  json 余额: $($json.Balance) 局数: $($json.Round) 历史条数: $($json.History.Count) 桌人数: $($json.TablePlayers) 档位: $($json.Tier)"

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 4
$p.Refresh()
$win = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

function All($type) {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type)
    $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
}
function Buttons { @(All ([System.Windows.Automation.ControlType]::Button) | Sort-Object { $_.Current.BoundingRectangle.Top }, { $_.Current.BoundingRectangle.Left }) }
function HasText([string]$needle) {
    return [bool](All ([System.Windows.Automation.ControlType]::Text) | Where-Object { $_.Current.Name -like ('*' + $needle + '*') } | Select-Object -First 1)
}
function Click([string]$part, [int]$index = 0) {
    $found = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($found.Count -le $index) { "  MISS button: $part"; return }
    $found[$index].SetFocus(); Start-Sleep -Milliseconds 120
    $found[$index].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 800
}

Click '存档模式'
"  重启后余额一致: $(HasText ('余额：$' + $json.Balance.ToString('N0')))"
"  重启后局数一致: $(HasText ('总局数：' + ($json.Round - 1)))"
"  重启后桌人数一致: $(HasText ('桌人数：' + $json.TablePlayers))"
"  历史记录已恢复: $(HasText '第 ')"
"  历史条数超限检查(<=40): $($json.History.Count -le 40)"

Write-Output '== 智能模式独立存档 =='
Click '切换模式'
Click '返回游戏大厅'
"  智能模式余额: $(HasText '余额：∞（智能模式）')"
"  custom-progress.json 存在: $(Test-Path $custom)"
if (Test-Path $custom) { "  内容: " + (Get-Content $custom -Raw) }
Click '退出到模式选择'

Write-Output '== 退出与窗口控制 =='
$minBtn = @(Buttons | Where-Object { $_.Current.Name -eq '—' })
$closeBtn = @(Buttons | Where-Object { $_.Current.Name -eq '×' })
"  最小化按钮: $($minBtn.Count) 关闭按钮: $($closeBtn.Count)"
"  crash.log 存在: $(Test-Path $crash)"
if (Test-Path $crash) { Get-Content $crash -Raw }

Stop-Process -Id $p.Id -Force
'DONE'
