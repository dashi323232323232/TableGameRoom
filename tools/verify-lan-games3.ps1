# 联机 炸金花 / 骗子 / 德州 冒烟测试
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$Exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
$results = New-Object System.Collections.Generic.List[string]
function Check($n,$ok,$d='') { $m=if($ok){'PASS'}else{'FAIL'}; $results.Add("[$m] $n  $d"); Write-Host "[$m] $n  $d" }
function BO($w){$c=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button);@($w.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c))}
function TO($w){$c=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Text);@($w.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c))}
function CB($w,$p,$i=0){$f=@(BO $w|Where-Object{$_.Current.Name -like ('*'+$p+'*')});if($f.Count -le $i){Write-Host "  MISS $p";return $false};try{$f[$i].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 800;return $true}catch{return $false}}
function HasT($w,$p){[bool](@(TO $w|Where-Object{$_.Current.Name -like $p}).Count -gt 0)}

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

function SetupTwoPlayers {
    $a = Start-Process $Exe -ArgumentList '--multi' -PassThru; Start-Sleep -Seconds 4; $a.Refresh()
    $wa = [System.Windows.Automation.AutomationElement]::FromHandle($a.MainWindowHandle)
    $b = Start-Process $Exe -ArgumentList '--multi' -PassThru; Start-Sleep -Seconds 4; $b.Refresh()
    $wb = [System.Windows.Automation.AutomationElement]::FromHandle($b.MainWindowHandle)
    CB $wa '联机大厅' | Out-Null
    CB $wa '创建房间' | Out-Null
    Start-Sleep -Milliseconds 1200
    CB $wb '联机大厅' | Out-Null
    CB $wb '加入房间' | Out-Null
    Start-Sleep -Seconds 2
    CB $wa '创建房间' | Out-Null
    Start-Sleep -Milliseconds 1200
    $j = @(BO $wb | Where-Object { $_.Current.Name -eq '加入' })
    if ($j.Count -gt 0) { $j[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 1200 }
    CB $wa '进入房间' | Out-Null
    Start-Sleep -Milliseconds 900
    return @($wa, $wb)
}

# ---------- 炸金花 ----------
Write-Host "=== 炸金花 ==="
$r = SetupTwoPlayers
$wa = $r[0]; $wb = $r[1]
CB $wa '炸金花' | Out-Null
Start-Sleep -Milliseconds 600
CB $wa '开始游戏' | Out-Null
Start-Sleep -Seconds 2
Check '炸金花 A 进入对局' (HasT $wa '*联机对局*')
Check '炸金花 B 进入对局' (HasT $wb '*联机对局*')
CB $wa '开始新局' | Out-Null
Start-Sleep -Seconds 2
Check '炸金花 A 已发牌（有跟注/看牌）' ((HasT $wa '*跟注*') -or (HasT $wa '*看牌*') -or (HasT $wa '*弃牌*'))
Check '炸金花 A 显示底池' (HasT $wa '*底池*')
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

# ---------- 骗子扑克 ----------
Write-Host "=== 骗子扑克 ==="
$r = SetupTwoPlayers
$wa = $r[0]; $wb = $r[1]
CB $wa '骗子扑克' | Out-Null
Start-Sleep -Milliseconds 600
CB $wa '开始游戏' | Out-Null
Start-Sleep -Seconds 2
Check '骗子 A 进入对局' (HasT $wa '*联机对局*')
CB $wa '开始新局' | Out-Null
Start-Sleep -Seconds 2
Check '骗子 A 已发牌（有手牌）' (HasT $wa '*我的手牌*')
Check '骗子 A 有声明选项' (HasT $wa '*声明*')
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

# ---------- 德州扑克 ----------
Write-Host "=== 德州扑克 ==="
$r = SetupTwoPlayers
$wa = $r[0]; $wb = $r[1]
CB $wa '德州扑克' | Out-Null
Start-Sleep -Milliseconds 600
CB $wa '开始游戏' | Out-Null
Start-Sleep -Seconds 2
Check '德州 A 进入对局' (HasT $wa '*联机对局*')
CB $wa '开始新局' | Out-Null
Start-Sleep -Seconds 2
Check '德州 A 已发牌（有跟注/弃牌）' ((HasT $wa '*跟注*') -or (HasT $wa '*弃牌*'))
Check '德州 A 显示公共牌' (HasT $wa '*公共牌*')
Check '德州 A 显示底池' (HasT $wa '*底池*')
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host ''
$results | ForEach-Object { Write-Host $_ }
$p=@($results|Where-Object{$_ -match '^\[PASS\]'}).Count; $f=@($results|Where-Object{$_ -match '^\[FAIL\]'}).Count
Write-Host "通过 $p 项，失败 $f 项"
