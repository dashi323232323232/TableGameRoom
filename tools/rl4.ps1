. D:\dsh\xin\tools\uia.ps1
StartApp
ClickUntil '选择智能模式' '*选择玩法*' | Out-Null
EnterCard '欧洲轮盘' | Out-Null
ClickBtn '开始游戏' | Out-Null
Start-Sleep -Milliseconds 700
$b = @(Buttons | Where-Object { $_.Current.Name -like '*旋转轮盘*' })[0]
"invoke..."
try { $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); "invoked" } catch { "invoke err: $_" }
Start-Sleep -Milliseconds 1500
"1.5s modal: $(CloseModal)"
"1.5s recent: $(Line '*最近结果*')"
Start-Sleep -Milliseconds 2500
"4s recent: $(Line '*最近结果*')"
"4s result: $(Line '*结果*')"
StopApp
Report
