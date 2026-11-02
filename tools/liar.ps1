. D:\dsh\xin\tools\uia.ps1
StartApp
ClickUntil '选择智能模式' '*选择玩法*' | Out-Null
EnterCard '骗子扑克' | Out-Null
ClickBtn '开始游戏' | Out-Null
ClickBtn '新开一局' | Out-Null
Start-Sleep -Milliseconds 600
$cards = @(Buttons | Where-Object { $_.Current.Name -like '手牌 *' })
ClickEl $cards[0] | Out-Null
Start-Sleep -Milliseconds 300
ClickBtn '盖牌并声明' | Out-Null
Start-Sleep -Milliseconds 500
"after play: " + (Line '*盖下*')
"continue modal: " + (ClickBtn '继续')
Start-Sleep -Milliseconds 800
"after continue: " + (Line '*声明信息*')
"challenge modal: " + (ClickBtn '质疑上一手')
Start-Sleep -Milliseconds 800
"after challenge text: " + ((Texts | ForEach-Object { $_.Current.Name } | Where-Object { $_ -like '*翻开*' -or $_ -like '*收走*' -or $_ -like '*声明属实*' -or $_ -like '*声明不实*' }) -join ' || ')
"pile line: " + (Line '*桌面牌堆：*')
"hand line: " + (Line '*你的手牌：*')
StopApp
Report
