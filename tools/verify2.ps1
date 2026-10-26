. D:\dsh\xin\tools\uia.ps1
StartApp
ClickUntil '选择智能模式' '*选择玩法*' | Out-Null

# ---------- 德州：加注 + 连续跟注到摊牌 ----------
EnterCard '德州扑克' | Out-Null
ClickBtn '开始游戏' | Out-Null
ClickBtn '开始牌局' | Out-Null
Start-Sleep -Milliseconds 500
Check '德州开局有跟注加注弃牌' ((Has '*跟注*') -and (Has '*加注*') -and (Has '*弃牌*'))
$pot0 = Line '*底池：*'
ClickBtn '加注' | Out-Null
Start-Sleep -Milliseconds 400
$pot1 = Line '*底池：*'
Check '德州加注后底池增加' ($pot0 -ne $pot1) "$pot0 -> $pot1"
for ($i = 0; $i -lt 20; $i++) {
    if ((Has '*净收益*') -or (Has '*再来一手*')) { break }
    ClickBtn '跟注' | Out-Null
    Start-Sleep -Milliseconds 450
}
Check '德州能推进到摊牌' ((Has '*河牌摊牌*') -or (Has '*净收益*')) (Line '*阶段*')
Check '德州摊牌有结果' (Has '*净收益*') (Line '*净收益*')
Check '德州摊牌显示牌型' (Has '*最佳五张*') (Line '*最佳五张*')
Check '德州出现再来一手' (Has '*再来一手*')
ClickBtn '再来一手' | Out-Null
Start-Sleep -Milliseconds 600
Check '德州能再来一手' (Has '*开始牌局*') (Line '*底池：*')

# ---------- 德州弃牌 ----------
ClickBtn '开始牌局' | Out-Null
Start-Sleep -Milliseconds 400
ClickBtn '弃牌' | Out-Null
Start-Sleep -Milliseconds 400
Check '德州弃牌有负收益' (Has '*净收益 -$*') (Line '*净收益*')

# ---------- 炸金花 ----------
ClickBtn '返回游戏大厅' | Out-Null
Start-Sleep -Milliseconds 400
EnterCard '炸金花' | Out-Null
ClickBtn '开始游戏' | Out-Null
ClickBtn '新开一局' | Out-Null
Start-Sleep -Milliseconds 500
Check '炸金花开局有五个操作' (((Has '*看牌*') -and (Has '*跟注*') -and (Has '*加注*') -and (Has '*比牌*') -and (Has '*弃牌*')))
ClickBtn '看牌' | Out-Null
Start-Sleep -Milliseconds 400
Check '炸金花看牌显示牌型' (Has '*已看牌*') (Line '*已看牌*')
$potA = Line '*底池：*'
ClickBtn '跟注' | Out-Null
Start-Sleep -Milliseconds 400
$potB = Line '*底池：*'
Check '炸金花跟注后底池增加' ($potA -ne $potB) "$potA -> $potB"
$mCompare = ClickBtn '比牌'
if (-not (Has '*净收益*')) { Start-Sleep -Milliseconds 400; $mCompare = ClickBtn '比牌' }
Start-Sleep -Milliseconds 600
Check '炸金花比牌有结果' ((Has '*净收益*') -or (Has '*电脑弃牌*') -or (Has '*你赢得*')) ("modal=$mCompare | " + (Line '*净收益*'))
$row = Line '*桌面筹码堆*'
$pot = [int]([regex]::Match($row, '底池：\$([\d,]+)').Groups[1].Value -replace ',', '')
$stack = [int]([regex]::Match($row, '🟡 (\d+) 叠').Groups[1].Value)
Check '炸金花筹码堆与底池一致' ($pot -eq 0 -or $stack -ge 1) "$row"

# ---------- 骗子扑克 ----------
ClickBtn '返回游戏大厅' | Out-Null
Start-Sleep -Milliseconds 400
EnterCard '骗子扑克' | Out-Null
ClickBtn '开始游戏' | Out-Null
ClickBtn '新开一局' | Out-Null
Start-Sleep -Milliseconds 600
Check '骗子发 5 张手牌' (Has '*你的手牌：5 张*') (Line '*桌面牌堆：*')
$cards = @(Buttons | Where-Object { $_.Current.Name -like '手牌 *' })
Check '骗子手牌可点击' ($cards.Count -eq 5) "可点手牌 $($cards.Count)"
$played = $false
$lastModal = ''
for ($t = 0; $t -lt 4 -and -not $played; $t++) {
    $cards = @(Buttons | Where-Object { $_.Current.Name -like '手牌 *' })
    if ($cards.Count -lt 2) { break }
    ClickEl $cards[0] | Out-Null
    Start-Sleep -Milliseconds 250
    $cards = @(Buttons | Where-Object { $_.Current.Name -like '手牌 *' })
    ClickEl $cards[1] | Out-Null
    Start-Sleep -Milliseconds 250
    $lastModal = ClickBtn '盖牌并声明'
    if ($lastModal -notlike '*请先点击手牌*') { $played = $true }
}
Start-Sleep -Milliseconds 600
Check '骗子出牌后手牌减少' ((Has '*你的手牌：3 张*') -or (Has '*你的手牌：2 张*')) ("modal=$lastModal | " + (Line '*桌面牌堆：*'))
Check '骗子出牌有日志' ((Has '*你盖下*') -or (Has '*电脑盖下*') -or (Has '*电脑质疑你*') -or (Has '*你出完手牌*')) (Line '*盖下*')
if (Has '*你可以质疑或继续*') {
    Check '电脑出牌并声明' $true (Line '*电脑盖下*')
    ClickBtn '质疑上一手' | Out-Null
    Start-Sleep -Milliseconds 700
    Check '质疑后翻开判定' (Has '*翻开：*') (Line '*翻开：*')
    Check '质疑后桌面牌堆清空' (Has '*桌面牌堆：0 张 ·*') (Line '*桌面牌堆：0 张*')
}
else {
    Check '电脑主动质疑玩家' (Has '*电脑质疑你*') (Line '*电脑质疑你*')
    Check '质疑后翻开判定' (Has '*翻开：*') (Line '*翻开：*')
}

# ---------- 存档模式持久化 ----------
ClickBtn '返回游戏大厅' | Out-Null
Start-Sleep -Milliseconds 400
ClickBtn '切换模式' | Out-Null
Start-Sleep -Milliseconds 600
EnterCard '21 点' | Out-Null
ClickBtn '开始游戏' | Out-Null
ClickBtn '开始新局' | Out-Null
Start-Sleep -Milliseconds 500
ClickBtn '停牌' | Out-Null
Start-Sleep -Milliseconds 600
$header = Line '模式：存档*'
$bal = [int]([regex]::Match($header, '余额：\$([\d,]+)').Groups[1].Value -replace ',', '')
$round = [int]([regex]::Match($header, '总局数：(\d+)').Groups[1].Value)
Start-Sleep -Milliseconds 400
$json = Get-Content "$env:LOCALAPPDATA\TableGameRoom\progress.json" -Raw | ConvertFrom-Json
Check '存档余额已写入' ($json.Balance -eq $bal) "界面 $bal / 文件 $($json.Balance)"
Check '存档局数已写入' ($json.Round -eq ($round + 1)) "界面总局数 $round / 文件 Round $($json.Round)"
Check '存档历史不超过 40 条' (@($json.History).Count -le 40) "历史 $(@($json.History).Count) 条"
StopApp
Report
