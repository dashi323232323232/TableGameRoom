. D:\dsh\xin\tools\uia.ps1
StartApp
ClickUntil '选择智能模式' '*选择玩法*' | Out-Null
EnterCard '欧洲轮盘' | Out-Null
ClickBtn '开始游戏' | Out-Null
Start-Sleep -Milliseconds 700
$b = @(Buttons | Where-Object { $_.Current.Name -like '*旋转轮盘*' })[0]
"spin offscreen=$($b.Current.IsOffscreen) rect=$($b.Current.BoundingRectangle)"
$sv = $Win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Pane)))
"pane=$($sv.Current.Name)"
$sc = @(All ([System.Windows.Automation.ControlType]::Pane))
foreach ($s in $sc) { "pane: '$($s.Current.Name)' class=$($s.Current.ClassName) off=$($s.Current.IsOffscreen)" }
"scrollto: $((ScrollTo $b))"
"after scroll: offscreen=$($b.Current.IsOffscreen) rect=$($b.Current.BoundingRectangle)"
ClickBtn '旋转轮盘' | Out-Null
Start-Sleep -Milliseconds 1500
"1.5s recent: $(Line '*最近结果*')"
Start-Sleep -Milliseconds 2500
"4s recent: $(Line '*最近结果*')"
"4s result: $(Line '*结果*')"
StopApp
Report
