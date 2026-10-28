. D:\dsh\xin\tools\uia.ps1
StartApp
ClickUntil '选择智能模式' '*选择玩法*' | Out-Null
EnterCard '炸金花' | Out-Null
ClickBtn '休闲：$10–$500' | Out-Null
Start-Sleep -Milliseconds 400
"tier line: " + (Line '*档位*')
ClickBtn '开始游戏' | Out-Null
Start-Sleep -Milliseconds 600
"edits: " + (@(All ([System.Windows.Automation.ControlType]::Edit)).Count)
SetStake '5'
"stake now: " + ((All ([System.Windows.Automation.ControlType]::Edit))[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value)
"buttons: " + ((Buttons | ForEach-Object { $_.Current.Name }) -join ' | ')
$r = ClickBtn '新开一局'
"modal: [$r]"
"page has 看牌 text: " + (Has '*看牌*')
"texts: " + ((Texts | ForEach-Object { $_.Current.Name }) -join ' | ')
StopApp
