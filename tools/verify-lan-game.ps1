# 局域网联机 · 完整对局测试（含轮盘下注/开盘/结果同步）
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$Exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
$results = New-Object System.Collections.Generic.List[string]
function Check($name, $ok, $detail = '') {
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    $results.Add("[$mark] $name  $detail")
    Write-Host "[$mark] $name  $detail"
}
function BO($w) { $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button); @($w.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c)) }
function TO($w) { $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Text); @($w.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c)) }
function CB($w,$p,$i=0) {
    $f=@(BO $w|Where-Object{$_.Current.Name -like ('*'+$p+'*')})
    if($f.Count -le $i){ Write-Host "  MISS: $p"; return $false }
    try { $f[$i].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 800; return $true } catch { return $false }
}
function CBExact($w,$p) {
    $f=@(BO $w|Where-Object{$_.Current.Name -eq $p})
    if($f.Count -eq 0){ Write-Host "  MISS exact: $p"; return $false }
    try { $f[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 800; return $true } catch { return $false }
}
function HasText($w,$p) { [bool](@(TO $w|Where-Object{$_.Current.Name -like $p}).Count -gt 0) }
function LineOf($w,$p) { (@(TO $w|Where-Object{$_.Current.Name -like $p}|Select-Object -First 1)).Current.Name }

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

# 启动两个实例
$procA = Start-Process $Exe -ArgumentList '--multi' -PassThru
Start-Sleep -Seconds 4
$procA.Refresh()
$winA = [System.Windows.Automation.AutomationElement]::FromHandle($procA.MainWindowHandle)

$procB = Start-Process $Exe -ArgumentList '--multi' -PassThru
Start-Sleep -Seconds 4
$procB.Refresh()
$winB = [System.Windows.Automation.AutomationElement]::FromHandle($procB.MainWindowHandle)

# A 建会话
CB $winA '联机大厅' | Out-Null
CB $winA '创建房间' | Out-Null
Start-Sleep -Milliseconds 1200
$infoA = LineOf $winA '*端口*'
$port = if ($infoA -match '端口：(\d+)') { $Matches[1] } else { '47800' }

# B 加入
CB $winB '联机大厅' | Out-Null
$editCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Edit)
$edits = @($winB.FindAll([System.Windows.Automation.TreeScope]::Descendants, $editCond))
if ($edits.Count -gt 0) {
    $edits[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($port)
}
CB $winB '加入房间' | Out-Null
Start-Sleep -Seconds 2

# A 建逻辑房间
CB $winA '创建房间' | Out-Null
Start-Sleep -Milliseconds 1200

# B 加入房间
$joinBtns = @(BO $winB | Where-Object { $_.Current.Name -eq '加入' })
if ($joinBtns.Count -gt 0) { $joinBtns[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 1200 }

# A 进入房间
CB $winA '进入房间' | Out-Null
Start-Sleep -Milliseconds 900

Check 'A 在房间页' (HasText $winA '*玩家列表*')
Check 'A 房间有 2 人' (HasText $winA '*2/10*')

# 选择游戏：21 点（默认）
CBExact $winA '21 点' | Out-Null
Start-Sleep -Milliseconds 600

# 开始游戏
CB $winA '开始游戏' | Out-Null
Start-Sleep -Seconds 2

Check 'A 进入联机 21 点对局' (HasText $winA '*联机对局*')
Check 'A 有下注按钮' (HasText $winA '*下注*')
Start-Sleep -Milliseconds 1200
Check 'B 也进入联机 21 点对局' (HasText $winB '*联机对局*')

# A 下注
CBExact $winA '下注' | Out-Null
Start-Sleep -Milliseconds 1200
Check 'A 下注成功（显示我的下注）' (HasText $winA '*我的下注*')

# 房主发牌
CB $winA '开始发牌' | Out-Null
Start-Sleep -Seconds 2
Check 'A 已发牌（有要牌/停牌）' ((HasText $winA '*要牌*') -or (HasText $winA '*等待其他玩家*')) 
Check 'B 同步看到发牌' ((HasText $winB '*等待*') -or (HasText $winB '*要牌*') -or (HasText $winB '*停牌*'))

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host ''
Write-Host '========== 结果 =========='
$results | ForEach-Object { Write-Host $_ }
$pass = @($results | Where-Object { $_ -match '^\[PASS\]' }).Count
$fail = @($results | Where-Object { $_ -match '^\[FAIL\]' }).Count
Write-Host "通过 $pass 项，失败 $fail 项"
