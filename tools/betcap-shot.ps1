Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

$exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 4
$p.Refresh()
$win = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

function All($type) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type)
    $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c)
}
function Buttons { @(All ([System.Windows.Automation.ControlType]::Button) | Sort-Object { $_.Current.BoundingRectangle.Top }, { $_.Current.BoundingRectangle.Left }) }
function Texts { @(All ([System.Windows.Automation.ControlType]::Text)) }
function Invoke-El($el) {
    try { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
    catch { Start-Sleep -Milliseconds 250; $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
    Start-Sleep -Milliseconds 900
}
function ClickName([string]$part, [int]$index = 0) {
    $f = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($f.Count -le $index) { "  MISS: $part"; return }
    Invoke-El $f[$index]
}
function EnterCard([string]$title) {
    $t = Texts | Where-Object { $_.Current.Name -like ('*' + $title + '*') } | Select-Object -First 1
    $r = $t.Current.BoundingRectangle
    $btn = Buttons | Where-Object { $_.Current.Name -like '*进入筛选*' -and $_.Current.BoundingRectangle.Top -ge $r.Top } |
        Sort-Object { [math]::Abs($_.Current.BoundingRectangle.Left - $r.Left) } | Select-Object -First 1
    Invoke-El $btn
}
function Shot([string]$path) {
    $r = $win.Current.BoundingRectangle
    $bmp = New-Object System.Drawing.Bitmap([int]$r.Width, [int]$r.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}

ClickName '选择智能模式'
EnterCard '德州扑克'
ClickName '休闲：$10–$500'
ClickName '双人桌'
ClickName '开始游戏'
Start-Sleep -Milliseconds 500

$edit = All ([System.Windows.Automation.ControlType]::Edit) | Select-Object -First 1
$edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('500')
Start-Sleep -Milliseconds 200
"  设置后值: $($edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value)"
Shot 'D:\dsh\xin\tools\shots\poker-before.png'

ClickName '开始牌局'
Start-Sleep -Milliseconds 900
Shot 'D:\dsh\xin\tools\shots\poker-after.png'
"  弹窗: $(([System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)))).Count) 个顶层窗口"
Texts | Select-Object -First 6 | ForEach-Object { "  T: $($_.Current.Name)" }
Stop-Process -Id $p.Id -Force
'DONE'
