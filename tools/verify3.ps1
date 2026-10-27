. D:\dsh\xin\tools\uia.ps1
StartApp
ClickUntil '选择智能模式' '*选择玩法*' | Out-Null

# ---------- 21 点双倍下注 ----------
EnterCard '21 点' | Out-Null
ClickBtn '开始游戏' | Out-Null
SetStake '100'
ClickBtn '开始新局' | Out-Null
Start-Sleep -Milliseconds 500
$before = Line '*本局下注*'
ClickBtn '双倍下注' | Out-Null
Start-Sleep -Milliseconds 700
$after = (Line '*本局下注*')
Check '双倍下注后下注翻倍' ($after -like '*$200*' -or (Has '*净收益*')) "$before -> $after"
Check '双倍后结束玩家操作' ((Has '*净收益*') -or -not (Has '*要牌*')) (Line '*净收益*')

# ---------- 下注校验 ----------
ClickBtn '返回游戏大厅' | Out-Null
Start-Sleep -Milliseconds 400
EnterCard '炸金花' | Out-Null
ClickBtn '休闲：$10–$500' | Out-Null
ClickBtn '开始游戏' | Out-Null
Start-Sleep -Milliseconds 500
SetStake '5'
$m1 = ClickBtn '新开一局'
Check '低于档位下限被拦截' ($m1 -like '*最低*') $m1
SetStake '999999'
$m2 = ClickBtn '新开一局'
Check '超过档位上限被拦截' ($m2 -like '*上限*') $m2
SetStake '50'
ClickBtn '新开一局' | Out-Null
Start-Sleep -Milliseconds 500
Check '合法下注可开局' (@(Buttons | Where-Object { $_.Current.Name -like '*看牌*' }).Count -ge 1) (Line '*你的下注*')

# ---------- 智能/存档隔离 ----------
ClickBtn '返回游戏大厅' | Out-Null
Start-Sleep -Milliseconds 400
ClickBtn '切换模式' | Out-Null
Start-Sleep -Milliseconds 600
$saveHeader = Line '模式：存档*'
$saveBal = [regex]::Match($saveHeader, '余额：\$([\d,]+)').Groups[1].Value
ClickBtn '切换模式' | Out-Null
Start-Sleep -Milliseconds 600
EnterCard '21 点' | Out-Null
ClickBtn '开始游戏' | Out-Null
SetStake '100'
ClickBtn '开始新局' | Out-Null
Start-Sleep -Milliseconds 400
ClickBtn '停牌' | Out-Null
Start-Sleep -Milliseconds 600
ClickBtn '返回游戏大厅' | Out-Null
Start-Sleep -Milliseconds 400
ClickBtn '切换模式' | Out-Null
Start-Sleep -Milliseconds 600
$saveHeader2 = Line '模式：存档*'
$saveBal2 = [regex]::Match($saveHeader2, '余额：\$([\d,]+)').Groups[1].Value
Check '智能模式不影响存档余额' ($saveBal -eq $saveBal2) "$saveBal -> $saveBal2"

# ---------- 游戏卡片 ToolTip ----------
ClickBtn '退出到模式选择' | Out-Null
Start-Sleep -Milliseconds 400
ClickBtn '选择智能模式' | Out-Null
Start-Sleep -Milliseconds 600
$card = @(Texts | Where-Object { $_.Current.Name -like '01  欧洲轮盘*' })[0]
$r = $card.Current.BoundingRectangle
[System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]($r.X + 20), [int]($r.Y + 10))
Start-Sleep -Milliseconds 2200
$tips = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ToolTip)))
$tipText = ($tips | ForEach-Object { $_.Current.Name }) -join ' || '
Check '悬停出现详细规则 ToolTip' ($tipText -like '*37 个号码*') $tipText
[System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(10, 10)

# ---------- 窗口按钮 ----------
ClickBtn '最小化' | Out-Null
Start-Sleep -Milliseconds 900
$h = $script:Proc.MainWindowHandle
$iconic = [WU]::IsIconic($h)
Check '最小化后进程仍在' ((-not $script:Proc.HasExited) -and $iconic) "iconic=$iconic"
[WU]::ShowWindow($h, 9) | Out-Null
Start-Sleep -Milliseconds 900
Activate
ClickBtn '关闭' | Out-Null
for ($i = 0; $i -lt 20; $i++) { Start-Sleep -Milliseconds 250; if ($script:Proc.HasExited) { break } }
Check '关闭按钮能退出程序' ($script:Proc.HasExited) "exited=$($script:Proc.HasExited)"
Report
