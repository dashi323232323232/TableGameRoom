# 验证：勾选真人 + 选总人数 → 开局后 AI 真的补位
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$Exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
$results = New-Object System.Collections.Generic.List[string]
function Check($name, $ok, $detail = '') {
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    $results.Add("[$mark] $name  $detail")
    Write-Host "[$mark] $name  $detail"
}

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

$procA = Start-Process $Exe -ArgumentList '--multi' -PassThru
Start-Sleep -Seconds 4
$procA.Refresh()
$winA = [System.Windows.Automation.AutomationElement]::FromHandle($procA.MainWindowHandle)

$procB = Start-Process $Exe -ArgumentList '--multi' -PassThru
Start-Sleep -Seconds 4
$procB.Refresh()
$winB = [System.Windows.Automation.AutomationElement]::FromHandle($procB.MainWindowHandle)

function BO($w) { $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button); @($w.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c)) }
function TO($w) { $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Text); @($w.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c)) }
function CB($w,$p,$i=0) {
    $f=@(BO $w|Where-Object{$_.Current.Name -like ('*'+$p+'*')})
    if($f.Count -le $i){ Write-Host "  MISS: $p"; return $false }
    try { $f[$i].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 900; return $true } catch { return $false }
}
function CBExact($w,$p) {
    $f=@(BO $w|Where-Object{$_.Current.Name -eq $p})
    if($f.Count -eq 0){ Write-Host "  MISS exact: $p"; return $false }
    try { $f[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 900; return $true } catch { return $false }
}
function HasText($w,$p) { [bool](@(TO $w|Where-Object{$_.Current.Name -like $p}).Count -gt 0) }
function LineOf($w,$p) { (@(TO $w|Where-Object{$_.Current.Name -like $p}|Select-Object -First 1)).Current.Name }

# 建会话
CB $winA '联机大厅' | Out-Null
CB $winA '创建房间' | Out-Null
Start-Sleep -Milliseconds 1500
$port = if ((LineOf $winA '*端口*') -match '端口：(\d+)') { $Matches[1] } else { '47800' }

CB $winB '联机大厅' | Out-Null
$ec = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Edit)
$edits = @($winB.FindAll([System.Windows.Automation.TreeScope]::Descendants, $ec))
if ($edits.Count -gt 0) { $edits[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($port) }
CB $winB '加入房间' | Out-Null
Start-Sleep -Seconds 2

# 建房
CB $winA '创建房间' | Out-Null
Start-Sleep -Milliseconds 1500
$jb = @(BO $winB | Where-Object { $_.Current.Name -eq '加入' })
if ($jb.Count -gt 0) { $jb[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 1500 }

CB $winA '进入房间' | Out-Null
Start-Sleep -Milliseconds 1000
Check '房间页有 2 名真人' (HasText $winA '*已勾选参战真人：2 人*') (LineOf $winA '*已勾选参战真人*')

# 选 5 人 → 补 3 个 AI
CBExact $winA '5（+3AI）' | Out-Null
Start-Sleep -Milliseconds 1000
Check '选 5 人显示 AI 补位 3 名' ((LineOf $winA '*本局人数*') -match 'AI 补位 3 名') (LineOf $winA '*本局人数*')

# 确保是 21 点
CBExact $winA '21 点' | Out-Null
Start-Sleep -Milliseconds 800

# 开始游戏
CB $winA '开始游戏' | Out-Null
Start-Sleep -Seconds 2
Check 'A 进入联机对局' (HasText $winA '*联机对局*')

# 下注并发牌
CBExact $winA '下注' | Out-Null
Start-Sleep -Milliseconds 1200
CB $winA '开始发牌' | Out-Null
Start-Sleep -Seconds 3

# 看是否有 AI 玩家出现
$aiLine = LineOf $winA '*AI *'
Check '开局后出现 AI 补位玩家' ($aiLine -ne $null) $aiLine
$aiCount = @(TO $winA | Where-Object { $_.Current.Name -like '*🤖 AI*' }).Count
Check 'AI 补位数量为 3' ($aiCount -eq 3) "找到 $aiCount 个 AI"

# B 端也应看到
Start-Sleep -Milliseconds 1200
$aiB = @(TO $winB | Where-Object { $_.Current.Name -like '*🤖 AI*' }).Count
Check 'B 端也看到 3 个 AI' ($aiB -eq 3) "B 找到 $aiB 个"

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host ''
Write-Host '========== 结果 =========='
$pass = @($results | Where-Object { $_ -match '^\[PASS\]' }).Count
$fail = @($results | Where-Object { $_ -match '^\[FAIL\]' }).Count
Write-Host "通过 $pass 项，失败 $fail 项"
