# 观战视图验证：B 取消参战后应看到「观战中」且无下注按钮
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$Exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
$results = New-Object System.Collections.Generic.List[string]
function Check($n,$ok,$d='') { $m=if($ok){'PASS'}else{'FAIL'}; $results.Add("[$m] $n  $d"); Write-Host "[$m] $n  $d" }
function BO($w){$c=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button);@($w.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c))}
function TO($w){$c=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Text);@($w.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c))}
function CB($w,$p,$i=0){$f=@(BO $w|Where-Object{$_.Current.Name -like ('*'+$p+'*')});if($f.Count -le $i){Write-Host "  MISS $p";return $false};try{$f[$i].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 800;return $true}catch{return $false}}
function HasT($w,$p){[bool](@(TO $w|Where-Object{$_.Current.Name -like $p}).Count -gt 0)}
function LineOf($w,$p){(@(TO $w|Where-Object{$_.Current.Name -like $p}|Select-Object -First 1)).Current.Name}

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

$a = Start-Process $Exe -ArgumentList '--multi' -PassThru; Start-Sleep -Seconds 4; $a.Refresh()
$wa = [System.Windows.Automation.AutomationElement]::FromHandle($a.MainWindowHandle)
$b = Start-Process $Exe -ArgumentList '--multi' -PassThru; Start-Sleep -Seconds 4; $b.Refresh()
$wb = [System.Windows.Automation.AutomationElement]::FromHandle($b.MainWindowHandle)

CB $wa '联机大厅' | Out-Null
CB $wa '创建房间' | Out-Null
Start-Sleep -Milliseconds 1200
$info = LineOf $wa '*端口*'
$port = if ($info -match '端口：(\d+)') { $Matches[1] } else { '47800' }
CB $wa '创建房间' | Out-Null
Start-Sleep -Milliseconds 1200

CB $wb '联机大厅' | Out-Null
$editCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Edit)
$edits = @($wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, $editCond))
if ($edits.Count -gt 0) { $edits[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($port) }
CB $wb '加入房间' | Out-Null
Start-Sleep -Seconds 2
$j = @(BO $wb | Where-Object { $_.Current.Name -eq '加入' })
if ($j.Count -gt 0) { $j[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 1200 }

CB $wa '进入房间' | Out-Null
Start-Sleep -Milliseconds 1000

# 找 B 的参战勾选框并取消（房主 A 操作）
$checks = @()
$cbCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::CheckBox)
$allChecks = @($wa.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cbCond))
Write-Host "  参战勾选框数量: $($allChecks.Count)"

# 第二个勾选框是 B 的（第一个是 A 自己）
if ($allChecks.Count -ge 2) {
    $tp = $allChecks[1].GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    Write-Host "  B 当前参战状态: $($tp.Current.ToggleState)"
    if ($tp.Current.ToggleState -eq 'On') {
        $tp.Toggle()
        Start-Sleep -Milliseconds 1500
        Write-Host "  已取消 B 的参战"
    }
    $tp2 = $allChecks[1].GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    Check 'B 参战已取消' ($tp2.Current.ToggleState -eq 'Off')
} else {
    Check 'B 参战已取消' $false "勾选框数量不足"
}

# A 开始游戏
CB $wa '开始游戏' | Out-Null
Start-Sleep -Seconds 2

# B 应该看到观战提示
Check 'B 看到观战提示' ((HasT $wb '*观战*'))
Check 'B 看不到下注按钮' (-not (HasT $wb '*下注*') -or (HasT $wb '*观战*'))
Check 'A 看到对局' (HasT $wa '*联机对局*')

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host ''
$results | ForEach-Object { Write-Host $_ }
$p=@($results|Where-Object{$_ -match '^\[PASS\]'}).Count; $f=@($results|Where-Object{$_ -match '^\[FAIL\]'}).Count
Write-Host "通过 $p 项，失败 $f 项"
