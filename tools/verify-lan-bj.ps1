# 联机 21 点测试
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$Exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
$results = New-Object System.Collections.Generic.List[string]
function Check($n,$ok,$d='') { $m = if($ok){'PASS'}else{'FAIL'}; $results.Add("[$m] $n  $d"); Write-Host "[$m] $n  $d" }
function BO($w) { $c=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button); @($w.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c)) }
function TO($w) { $c=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Text); @($w.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c)) }
function CB($w,$p,$i=0) { $f=@(BO $w|Where-Object{$_.Current.Name -like ('*'+$p+'*')}); if($f.Count -le $i){Write-Host "  MISS $p";return $false}; try{$f[$i].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 800;return $true}catch{return $false} }
function CBExact($w,$p) { $f=@(BO $w|Where-Object{$_.Current.Name -eq $p}); if($f.Count -eq 0){Write-Host "  MISS exact $p";return $false}; try{$f[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 800;return $true}catch{return $false} }
function HasT($w,$p) { [bool](@(TO $w|Where-Object{$_.Current.Name -like $p}).Count -gt 0) }

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600
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
$join = @(BO $wb | Where-Object { $_.Current.Name -eq '加入' })
if ($join.Count -gt 0) { $join[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 1200 }
CB $wa '进入房间' | Out-Null
Start-Sleep -Milliseconds 900

# 选 21 点
CB $wa '21 点' | Out-Null
Start-Sleep -Milliseconds 700
Check 'A 选择 21 点' (HasT $wa '*21 点*')

# 开始
CB $wa '开始游戏' | Out-Null
Start-Sleep -Seconds 2
Check 'A 进入联机 21 点' (HasT $wa '*联机对局*')
Check 'A 有下注按钮' (HasT $wa '*下注*')

# A 下注
CBExact $wa '下注' | Out-Null
Start-Sleep -Milliseconds 1000

# 房主发牌
CB $wa '开始发牌' | Out-Null
Start-Sleep -Seconds 2
Check 'A 已发牌（有要牌/停牌）' ((HasT $wa '*要牌*') -or (HasT $wa '*停牌*'))
Check 'A 显示庄家' (HasT $wa '*庄 家*')

# 停牌
CB $wa '停牌' | Out-Null
Start-Sleep -Seconds 2
Check 'A 本局结算' ((HasT $wa '*结果*') -or (HasT $wa '*爆牌*') -or (HasT $wa '*平局*') -or (HasT $wa '*输*') -or (HasT $wa '*赢*'))

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Host ''
$results | ForEach-Object { Write-Host $_ }
$p=@($results|Where-Object{$_ -match '^\[PASS\]'}).Count; $f=@($results|Where-Object{$_ -match '^\[FAIL\]'}).Count
Write-Host "通过 $p 项，失败 $f 项"
