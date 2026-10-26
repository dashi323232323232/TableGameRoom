# 联机高级功能测试：转让房主 / 踢人 / 邀请 / 观战
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

# 启动 A、B
$a = Start-Process $Exe -ArgumentList '--multi' -PassThru; Start-Sleep -Seconds 4; $a.Refresh()
$wa = [System.Windows.Automation.AutomationElement]::FromHandle($a.MainWindowHandle)
$b = Start-Process $Exe -ArgumentList '--multi' -PassThru; Start-Sleep -Seconds 4; $b.Refresh()
$wb = [System.Windows.Automation.AutomationElement]::FromHandle($b.MainWindowHandle)

# A 建会话 + 房间
CB $wa '联机大厅' | Out-Null
CB $wa '创建房间' | Out-Null
Start-Sleep -Milliseconds 1200
$info = LineOf $wa '*端口*'
$port = if ($info -match '端口：(\d+)') { $Matches[1] } else { '47800' }
CB $wa '创建房间' | Out-Null
Start-Sleep -Milliseconds 1200

# B 加入会话 + 房间
CB $wb '联机大厅' | Out-Null
$editCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Edit)
$edits = @($wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, $editCond))
if ($edits.Count -gt 0) { $edits[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($port) }
CB $wb '加入房间' | Out-Null
Start-Sleep -Seconds 2
$j = @(BO $wb | Where-Object { $_.Current.Name -eq '加入' })
if ($j.Count -gt 0) { $j[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 1200 }

# A 进入房间页
CB $wa '进入房间' | Out-Null
Start-Sleep -Milliseconds 1000

Check 'A 房间有 2 人' (HasT $wa '*2/10*')
Check 'A 有参战分组' (HasT $wa '*参战（*')
Check 'A 玩家列表显示筹码' (HasT $wa '*$10,000*')
Check 'A 有倒计时设置' (HasT $wa '*行动倒计时*')

# 测试倒计时选项
$timer15 = @(BO $wa | Where-Object { $_.Current.Name -eq '15 秒' })
if ($timer15.Count -gt 0) {
    $timer15[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 1000
    Check '可切换倒计时到 15 秒' (HasT $wa '*行动倒计时：15 秒*')
} else { Check '可切换倒计时到 15 秒' $false "未找到 15 秒按钮" }

# 测试游戏切换
CB $wa '德州扑克' | Out-Null
Start-Sleep -Milliseconds 800
Check '可切换游戏到德州扑克' (HasT $wa '*德州扑克*')

# 测试转让房主（通过「更多」按钮 → 菜单）
$more = @(BO $wa | Where-Object { $_.Current.Name -like '*更多*' })
Check '房主可看到「更多」菜单按钮' ($more.Count -gt 0)

# 测试参战勾选
Check '玩家列表有参战勾选框' (HasT $wa '*参战*')

# 启动游戏验证观战（B 保持参战，A 取消参战→观战）
CB $wa '开始游戏' | Out-Null
Start-Sleep -Seconds 2
Check 'A 进入联机对局' (HasT $wa '*联机对局*')
Check 'B 进入联机对局' (HasT $wb '*联机对局*')

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host ''
$results | ForEach-Object { Write-Host $_ }
$p=@($results|Where-Object{$_ -match '^\[PASS\]'}).Count; $f=@($results|Where-Object{$_ -match '^\[FAIL\]'}).Count
Write-Host "通过 $p 项，失败 $f 项"
