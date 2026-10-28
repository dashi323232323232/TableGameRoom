. D:\dsh\xin\tools\uia.ps1
StartApp
ClickUntil '选择智能模式' '*选择玩法*' | Out-Null
function Dump([string]$tag) {
    "== $tag =="
    "TEXT: " + ((Texts | ForEach-Object { $_.Current.Name } | Where-Object { $_ -ne '' -and $_ -notlike '*历史记录*' -and $_ -notlike '第 * 局*' }) -join ' | ')
    "BTN:  " + ((Buttons | ForEach-Object { $_.Current.Name } | Where-Object { $_ -ne '' }) -join ' | ')
}
foreach ($g in @('21 点', '德州扑克', '炸金花')) {
    EnterCard $g | Out-Null
    ClickBtn '开始游戏' | Out-Null
    Start-Sleep -Milliseconds 800
    Dump "游戏-$g-初始"
    ClickBtn '新开一局' | Out-Null
    Start-Sleep -Milliseconds 400
    ClickBtn '开始新局' | Out-Null
    Start-Sleep -Milliseconds 400
    ClickBtn '开始牌局' | Out-Null
    Start-Sleep -Milliseconds 600
    Dump "游戏-$g-开局后"
    ClickBtn '返回游戏大厅' | Out-Null
    Start-Sleep -Milliseconds 600
}
StopApp
