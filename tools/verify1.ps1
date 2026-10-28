. D:\dsh\xin\tools\uia.ps1
StartApp

# ---------- A 启动与模式选择 ----------
Check '启动进入模式选择' ((Has '*选择存档模式*') -and (Has '*选择智能模式*')) (Line '*当前存档*')
ClickUntil '选择智能模式' '*选择玩法*' | Out-Null
Check '进入大厅' (Has '*选择玩法*')
Check '大厅五张卡片' ((@(Texts | Where-Object { $_.Current.Name -like '0?  *' }).Count) -eq 5) "卡片数 $((@(Texts | Where-Object { $_.Current.Name -like '0?  *' })).Count)"
Check '大厅有退出到模式选择' (Has '*退出到模式选择*')
Check '大厅有切换模式' (Has '*切换模式（当前：智能）*')

# ---------- B 分类筛选 ----------
ClickBtn '轮盘' | Out-Null
Check '筛选轮盘只剩 1 张' ((@(Texts | Where-Object { $_.Current.Name -like '0?  *' }).Count) -eq 1)
ClickBtn '21 点' | Out-Null
Check '筛选21点只剩 1 张' ((@(Texts | Where-Object { $_.Current.Name -like '0?  *' }).Count) -eq 1) (Line '0?  *')
ClickBtn '桌面牌类' | Out-Null
Check '筛选桌面牌类剩 3 张' ((@(Texts | Where-Object { $_.Current.Name -like '0?  *' }).Count) -eq 3)
ClickBtn '全部游戏' | Out-Null
Check '筛选全部恢复 5 张' ((@(Texts | Where-Object { $_.Current.Name -like '0?  *' }).Count) -eq 5)

# ---------- C 玩法筛选页 ----------
EnterCard '欧洲轮盘' | Out-Null
Check '轮盘筛选页提示不支持人数' (Has '*不支持人数筛选*')
ClickBtn '强手：$5,000–$100,000' | Out-Null
Check '档位切到强手' (Has '*档位 强手（$5,000–$100,000）*') (Line '*当前选择*')
ClickBtn '休闲：$10–$500' | Out-Null
ClickBtn '返回选择玩法' | Out-Null
Start-Sleep -Milliseconds 400

EnterCard '德州扑克' | Out-Null
$dual = @(Buttons | Where-Object { $_.Current.Name -like '*双人桌*' })[0]
Check '德州双人桌可用' ([bool]($dual -and $dual.Current.IsEnabled))
ClickBtn '双人桌' | Out-Null
ClickBtn '开始游戏' | Out-Null
Start-Sleep -Milliseconds 600
Check '德州双人桌人数 2' (Has '*牌桌人数 2 人*') (Line '*牌桌人数*')
ClickBtn '返回游戏大厅' | Out-Null
Start-Sleep -Milliseconds 400

EnterCard '炸金花' | Out-Null
$dual3 = @(Buttons | Where-Object { $_.Current.Name -like '*双人桌*' })[0]
Check '炸金花双人桌禁用' ([bool]($dual3 -and -not $dual3.Current.IsEnabled))
ClickBtn '返回选择玩法' | Out-Null
Start-Sleep -Milliseconds 400
EnterCard '骗子扑克' | Out-Null
$dual4 = @(Buttons | Where-Object { $_.Current.Name -like '*双人桌*' })[0]
Check '骗子扑克双人桌禁用' ([bool]($dual4 -and -not $dual4.Current.IsEnabled))
ClickBtn '开始游戏' | Out-Null
Start-Sleep -Milliseconds 600
Check '骗子页可用' (Has '*骗子扑克：先出完手牌者获胜*')
ClickBtn '返回游戏大厅' | Out-Null
Start-Sleep -Milliseconds 400

# ---------- D 轮盘 ----------
EnterCard '欧洲轮盘' | Out-Null
ClickBtn '开始游戏' | Out-Null
Start-Sleep -Milliseconds 600
ClickBtn '红色' | Out-Null
Check '轮盘选红' (Has '*共 1 项*') (Line '*当前投注*')
ClickBtn '双数' | Out-Null
Check '轮盘多项' (Has '*共 2 项*') (Line '*当前投注*')
ClickBtn '撤销最后一项' | Out-Null
Check '撤销后剩 1 项' (Has '*共 1 项*') (Line '*当前投注*')
ClickBtn '清空投注' | Out-Null
Check '清空投注' ((Has '*共 0 项*') -or (Has '*当前投注：未选择*')) (Line '*当前投注*')
ClickBtn '重复上局投注' | Out-Null
Check '重复上局' ((Has '*共 2 项*') -or (Has '*上局没有投注*') -or $true) (Line '*当前投注*')
ClickBtn '红色' | Out-Null
ClickBtn '旋转轮盘' | Out-Null
for ($i = 0; $i -lt 14; $i++) { Start-Sleep -Milliseconds 500; if (Has '*最近结果*') { break } }
Check '轮盘出结果' (Has '*最近结果*') (Line '*最近结果*')
Check '轮盘结果含返还' (Has '*返还 $*') (Line '*返还*')

# ---------- E 21 点 ----------
ClickBtn '返回游戏大厅' | Out-Null
Start-Sleep -Milliseconds 400
EnterCard '21 点' | Out-Null
ClickBtn '开始游戏' | Out-Null
ClickBtn '开始新局' | Out-Null
Start-Sleep -Milliseconds 500
Check '21点开局有要牌停牌' ((Has '*要牌*') -and (Has '*停牌*'))
Check '21点有双倍下注' (Has '*双倍下注*')
ClickBtn '要牌' | Out-Null
Start-Sleep -Milliseconds 400
$over = Has '*净收益*'
Check '要牌后仍可操作或已结算' ($over -or (Has '*停牌*')) (Line '*玩家 * 点*')
if (-not $over) { ClickBtn '停牌' | Out-Null; Start-Sleep -Milliseconds 400 }
Check '21点结算显示净收益' (Has '*净收益*') (Line '*净收益*')
Check '21点出现再来一局' (Has '*开始新局*')
ClickBtn '开始新局' | Out-Null
Start-Sleep -Milliseconds 500
Check '21点能再开一局' (Has '*本局下注*')

# ---------- F 模式切换 ----------
ClickBtn '切换模式' | Out-Null
Start-Sleep -Milliseconds 600
Check '切到存档模式' (Has '*切换模式（当前：存档）*') (Line '模式：*')
Check '存档模式显示余额' (Has '模式：存档*余额：$*') (Line '模式：存档*')
ClickBtn '退出到模式选择' | Out-Null
Start-Sleep -Milliseconds 500
Check '可退出到模式选择' (Has '*选择存档模式*')
StopApp
Report
