# 邀请功能验证：A 创建房间 → 邀请 B → B 接受 → 进入房间
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

# A 建会话
CB $wa '联机大厅' | Out-Null
CB $wa '创建房间' | Out-Null
Start-Sleep -Milliseconds 1200
$info = LineOf $wa '*端口*'
$port = if ($info -match '端口：(\d+)') { $Matches[1] } else { '47800' }

# B 加入会话（但不进房间）
CB $wb '联机大厅' | Out-Null
$editCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Edit)
$edits = @($wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, $editCond))
if ($edits.Count -gt 0) { $edits[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($port) }
CB $wb '加入房间' | Out-Null
Start-Sleep -Seconds 2
Check 'B 已连入局域网' (HasT $wb '*在线：2 人*')

# A 创建逻辑房间
CB $wa '创建房间' | Out-Null
Start-Sleep -Milliseconds 1200
Check 'A 创建了房间' (HasT $wa '*· 1/10 人*')

# A 邀请 B（大厅玩家列表里的「邀请到我的房间」）
$inviteBtns = @(BO $wa | Where-Object { $_.Current.Name -like '*邀请到我的房间*' })
if ($inviteBtns.Count -gt 0) {
    $inviteBtns[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 1500
    Check 'A 发出邀请' $true
} else {
    Check 'A 发出邀请' $false "未找到邀请按钮"
}

# B 应该看到邀请
Start-Sleep -Milliseconds 1500
$bInvite = @(TO $wb | Where-Object { $_.Current.Name -like '*邀请*' })
Check 'B 收到邀请提示' ($bInvite.Count -gt 0) ($bInvite | Select-Object -First 1).Current.Name

# B 点击接受
$accept = @(BO $wb | Where-Object { $_.Current.Name -like '*接受并加入*' })
if ($accept.Count -gt 0) {
    $accept[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 2
    Check 'B 接受邀请后进入房间' ((HasT $wb '*玩家列表*') -or (HasT $wb '*2/10*'))
} else {
    Check 'B 接受邀请后进入房间' $false "未找到接受按钮"
}

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host ''
$results | ForEach-Object { Write-Host $_ }
$p=@($results|Where-Object{$_ -match '^\[PASS\]'}).Count; $f=@($results|Where-Object{$_ -match '^\[FAIL\]'}).Count
Write-Host "通过 $p 项，失败 $f 项"
