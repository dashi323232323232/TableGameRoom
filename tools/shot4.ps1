Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Mouse {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, int e);
    public static void Click(int x, int y) {
        SetCursorPos(x, y); System.Threading.Thread.Sleep(150);
        mouse_event(0x0002, 0, 0, 0, 0); mouse_event(0x0004, 0, 0, 0, 0);
    }
}
'@

$exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
$outDir = 'D:\dsh\xin\tools\shots'
$crash = Join-Path $env:LOCALAPPDATA 'TableGameRoom\crash.log'
$save = Join-Path $env:LOCALAPPDATA 'TableGameRoom\progress.json'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
if (Test-Path $crash) { Remove-Item $crash -Force }

Write-Output '== 损坏存档恢复 =='
New-Item -ItemType Directory -Force -Path (Split-Path $save) | Out-Null
Set-Content -Path $save -Value '{ 这不是合法 JSON ,,,' -Encoding UTF8
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 4
$p.Refresh()
$win = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

function Snap([string]$name) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
    $bmp.Save((Join-Path $outDir ($name + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}
function All($type) {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type)
    $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
}
function Buttons { @(All ([System.Windows.Automation.ControlType]::Button) | Sort-Object { $_.Current.BoundingRectangle.Top }, { $_.Current.BoundingRectangle.Left }) }
function HasText([string]$needle) {
    return [bool](All ([System.Windows.Automation.ControlType]::Text) | Where-Object { $_.Current.Name -like ('*' + $needle + '*') } | Select-Object -First 1)
}
function Invoke-El($el) {
    $el.SetFocus(); Start-Sleep -Milliseconds 120
    $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 700
}
function Find([string]$part, [int]$index = 0) {
    $found = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($found.Count -le $index) { return $null }
    return $found[$index]
}
function Click([string]$part, [int]$index = 0) {
    $el = Find $part $index
    if (-not $el) { "  MISS button: $part"; return $false }
    Invoke-El $el
    return $true
}
function EnterGame([string]$title) {
    $el = All ([System.Windows.Automation.ControlType]::Text) | Where-Object { $_.Current.Name -like ('*' + $title + '*') } | Select-Object -First 1
    if (-not $el) { "  MISS card: $title"; return }
    $t = $el.Current.BoundingRectangle
    $btn = Buttons | Where-Object { $_.Current.Name -like '*进入游戏*' -and $_.Current.BoundingRectangle.Top -ge $t.Top } |
        Sort-Object { [math]::Abs($_.Current.BoundingRectangle.Left - $t.Left) } | Select-Object -First 1
    if (-not $btn) { "  MISS enter: $title"; return }
    Invoke-El $btn
}
function Back { Click '返回游戏大厅' | Out-Null }
function CardText([string]$part) {
    $el = All ([System.Windows.Automation.ControlType]::Text) | Where-Object { $_.Current.Name -like ('*' + $part + '*') } | Select-Object -First 1
    if ($el) { return $el.Current.Name } else { return '(无)' }
}

Click '存档模式' | Out-Null
"  坏档后余额恢复默认: $(HasText '余额：$1,000')"
"  坏档后局数恢复默认: $(HasText '总局数：1')"
Snap 'H01-corrupt-save'
"  进程存活: $(-not $p.HasExited)"

Write-Output '== 21点 爆牌路径 =='
EnterGame '21 点'
Click '开始新局' | Out-Null
$bust = $false
for ($i = 0; $i -lt 8; $i++) {
    if (-not (Click '要牌' )) { break }
    if (HasText '爆牌') { $bust = $true; break }
    if (HasText '结果：') { break }
}
"  出现爆牌或结算: $(HasText '结果：') 爆牌字样: $(HasText '爆牌')"
Snap 'H02-blackjack-bust'
Back

Write-Output '== 炸金花 比牌亮牌 =='
EnterGame '炸金花'
Click '新开一局' | Out-Null
Click '看牌' | Out-Null
"  局中电脑牌面隐藏: $(HasText '✦')"
Snap 'H03-three-hidden'
Click '比牌' | Out-Null
Snap 'H04-three-compare'
"  比牌日志: $(CardText '比牌')"
Back

Write-Output '== 骗子扑克 继续与电脑质疑 =='
EnterGame '骗子扑克'
Click '新开一局' | Out-Null
Click '手牌' 0 | Out-Null
Click '盖牌并声明' | Out-Null
Click '继续' | Out-Null
Snap 'H05-liar-continue'
"  电脑已出牌或质疑: $(HasText '电脑盖下') / $(HasText '电脑质疑')"
$turns = 0
while ($turns -lt 8 -and (HasText '请选择出牌') -and -not (HasText '获胜')) {
    Click '手牌' 0 | Out-Null
    Click '盖牌并声明' | Out-Null
    if (HasText '你可质疑') { Click '继续' | Out-Null }
    $turns++
}
Snap 'H06-liar-loop'
"  回合推进: 状态 = $(CardText '当前回合')"
Back

Write-Output '== 崩溃日志 =='
"  crash.log 存在: $(Test-Path $crash)"
if (Test-Path $crash) { Get-Content $crash -Raw }

Stop-Process -Id $p.Id -Force
'DONE'
