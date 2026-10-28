. D:\dsh\xin\tools\uia.ps1
StartApp
ClickUntil '选择智能模式' '*选择玩法*' | Out-Null
EnterCard '欧洲轮盘' | Out-Null
ClickUntil '休闲：' '*休闲*' | Out-Null
ClickBtn '开始游戏' | Out-Null
Start-Sleep -Milliseconds 700
Check '轮盘页' (Has '*旋转轮盘*')

ClickBtn '红色' | Out-Null
Check '选中红色' (Has '*当前投注*红色*')
ClickBtn '双数' | Out-Null
Check '多项投注' (Has '*共 2 项*')

$spin = ClickBtn '旋转轮盘'
"spin modal: $spin"
for ($i = 0; $i -lt 12; $i++) {
    Start-Sleep -Milliseconds 500
    if (Has '*最近结果*') { break }
}
$recent = Line '*最近结果*'
Check '旋转后出现最近结果' ([bool]$recent) $recent
$res = Line '*返还*'
Check '结果区含返还金额' ([bool]$res) $res
StopApp
Report
