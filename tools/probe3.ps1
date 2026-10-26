. D:\dsh\xin\tools\uia.ps1
StartApp
ClickUntil '选择智能模式' '*选择玩法*' | Out-Null
function Dump([string]$tag) {
    "== $tag =="
    "TEXT: " + ((Texts | ForEach-Object { $_.Current.Name } | Where-Object { $_ -ne '' -and $_ -notlike '第 * 局*' }) -join ' | ')
    "BTN:  " + ((Buttons | ForEach-Object { $_.Current.Name } | Where-Object { $_ -ne '' }) -join ' | ')
}
function ToLobby { ClickBtn '返回游戏大厅' | Out-Null; Start-Sleep -Milliseconds 500 }

# 德州：连续跟注到摊牌
EnterCard '德州扑克' | Out-Null
ClickBtn '开始游戏' | Out-Null
ClickBtn '开始牌局' | Out-Null
Start-Sleep -Milliseconds 400
for ($i = 0; $i -lt 5; $i++) {
    $m = ClickBtn '跟注'
    if ($m) { "  跟注弹窗: $m" }
    Start-Sleep -Milliseconds 400
    if (Has '*净收益*') { break }
}
Dump '德州-多次跟注后'
ToLobby

# 炸金花：看牌 + 比牌
EnterCard '炸金花' | Out-Null
ClickBtn '开始游戏' | Out-Null
ClickBtn '新开一局' | Out-Null
Start-Sleep -Milliseconds 400
ClickBtn '看牌' | Out-Null
Start-Sleep -Milliseconds 300
ClickBtn '比牌' | Out-Null
Start-Sleep -Milliseconds 600
Dump '炸金花-比牌后'
ToLobby

# 骗子：新开一局
EnterCard '骗子扑克' | Out-Null
ClickBtn '开始游戏' | Out-Null
ClickBtn '新开一局' | Out-Null
Start-Sleep -Milliseconds 600
Dump '骗子-新开一局'
StopApp
