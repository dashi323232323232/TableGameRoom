. D:\dsh\xin\tools\uia.ps1
StartApp
ClickUntil '选择智能模式' '*选择玩法*' | Out-Null

function Dump([string]$tag) {
    "== $tag =="
    "TEXT: " + ((Texts | ForEach-Object { $_.Current.Name } | Where-Object { $_ -ne '' }) -join ' | ')
    "BTN:  " + ((Buttons | ForEach-Object { $_.Current.Name } | Where-Object { $_ -ne '' }) -join ' | ')
}

ClickBtn '轮盘' | Out-Null
Start-Sleep -Milliseconds 400
Dump '大厅-分类轮盘'
ClickBtn '21 点' | Out-Null
Start-Sleep -Milliseconds 400
Dump '大厅-分类21点'
ClickBtn '桌面牌类' | Out-Null
Start-Sleep -Milliseconds 400
Dump '大厅-桌面牌类'
ClickBtn '全部游戏' | Out-Null
Start-Sleep -Milliseconds 400

foreach ($g in @('欧洲轮盘', '21 点', '德州扑克', '炸金花', '骗子扑克')) {
    EnterCard $g | Out-Null
    Start-Sleep -Milliseconds 500
    Dump "筛选-$g"
    ClickBtn '开始游戏' | Out-Null
    Start-Sleep -Milliseconds 800
    Dump "游戏-$g"
    ClickBtn '返回游戏大厅' | Out-Null
    Start-Sleep -Milliseconds 600
}
StopApp
