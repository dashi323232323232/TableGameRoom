. D:\dsh\xin\tools\uia.ps1
StartApp
ClickUntil '选择智能模式' '*选择玩法*' | Out-Null
EnterCard '骗子扑克' | Out-Null
ClickBtn '开始游戏' | Out-Null
ClickBtn '新开一局' | Out-Null
Start-Sleep -Milliseconds 600

$turn = 0
for ($i = 0; $i -lt 60; $i++) {
    if ((Has '*你获胜，赢得底池*') -or (Has '*电脑获胜，你损失*')) { break }
    if (Has '*你可以质疑或继续*') {
        ClickBtn '继续' | Out-Null
        Start-Sleep -Milliseconds 400
        continue
    }
    $cards = @(Buttons | Where-Object { $_.Current.Name -like '手牌 *' })
    if ($cards.Count -eq 0) { break }
    ClickEl $cards[0] | Out-Null
    Start-Sleep -Milliseconds 250
    $m = ClickBtn '盖牌并声明'
    $turn++
    Start-Sleep -Milliseconds 350
    if ($m -like '*请先点击手牌*') { ClickEl (@(Buttons | Where-Object { $_.Current.Name -like '手牌 *' })[0]) | Out-Null; Start-Sleep -Milliseconds 200; ClickBtn '盖牌并声明' | Out-Null }
}
Check '骗子能打到分出胜负' ((Has '*你获胜，赢得底池*') -or (Has '*电脑获胜，你损失*')) ("回合 $turn | " + (Line '*获胜*'))
Check '结束后出现新开一局' (@(Buttons | Where-Object { $_.Current.Name -like '*新开一局*' }).Count -ge 1)
Check '结束后不能再出牌' (@(Buttons | Where-Object { $_.Current.Name -like '*盖牌并声明*' }).Count -eq 0)
Check '结算有净收益记录' (Has '*净收益*') (Line '*净收益*')
StopApp
Report
