Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Mouse {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, int e);
    public static void Move(int x, int y) { SetCursorPos(x, y); }
    public static void Down() { mouse_event(0x0002, 0, 0, 0, 0); }
    public static void Up() { mouse_event(0x0004, 0, 0, 0, 0); }
    public static void Click(int x, int y) {
        SetCursorPos(x, y); System.Threading.Thread.Sleep(150);
        mouse_event(0x0002, 0, 0, 0, 0); mouse_event(0x0004, 0, 0, 0, 0);
    }
}
'@

$exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
$outDir = 'D:\dsh\xin\tools\shots'
$crash = Join-Path $env:LOCALAPPDATA 'TableGameRoom\crash.log'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
if (Test-Path $crash) { Remove-Item $crash -Force }

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
function MouseClick([string]$part, [int]$index = 0) {
    $el = Find $part $index
    if (-not $el) { "  MISS button: $part"; return $false }
    $r = $el.Current.BoundingRectangle
    [Mouse]::Click([int]($r.Left + $r.Width / 2), [int]($r.Top + $r.Height / 2))
    Start-Sleep -Milliseconds 700
    return $true
}
function PressHold([string]$part, [int]$ms = 900) {
    $el = Find $part 0
    if (-not $el) { "  MISS button: $part"; return $false }
    $r = $el.Current.BoundingRectangle
    [Mouse]::Move([int]($r.Left + $r.Width / 2), [int]($r.Top + $r.Height / 2))
    Start-Sleep -Milliseconds 200
    [Mouse]::Down(); Start-Sleep -Milliseconds $ms; [Mouse]::Up()
    Start-Sleep -Milliseconds 400
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
function EnterCount { @(Buttons | Where-Object { $_.Current.Name -like '*进入游戏*' }).Count }
function Set-Combo([int]$index, [string]$item) {
    $list = @(All ([System.Windows.Automation.ControlType]::ComboBox) | Sort-Object { $_.Current.BoundingRectangle.Left })
    if ($list.Count -le $index) { "  MISS combo $index"; return }
    try { $list[$index].GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand() } catch { }
    Start-Sleep -Milliseconds 600
    $items = @(All ([System.Windows.Automation.ControlType]::ListItem) |
        Where-Object { $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.Name -like ('*' + $item + '*') })
    if ($items.Count -eq 0) { "  MISS item: $item"; [System.Windows.Forms.SendKeys]::SendWait('{ESC}'); return }
    $li = $items[0]
    try { $li.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
    catch {
        $r = $li.Current.BoundingRectangle
        [Mouse]::Click([int]($r.Left + $r.Width / 2), [int]($r.Top + $r.Height / 2))
    }
    Start-Sleep -Milliseconds 900
}
function KillModal { [System.Windows.Forms.SendKeys]::SendWait('{ENTER}'); Start-Sleep -Milliseconds 800 }
function Back { Click '返回游戏大厅' | Out-Null }

Write-Output '== 轮盘 多项下注控制 =='
Click '存档模式' | Out-Null
EnterGame '轮盘'
Click '红色 1:1' | Out-Null
Click '双数 1:1' | Out-Null
Click '7' 0 | Out-Null
"  三项已选: $(HasText '共 3 项')"
Snap 'G01-roulette-3bets'
Click '撤销最后一项' | Out-Null
"  撤销后两项: $(HasText '共 2 项')"
Snap 'G02-roulette-undo'
Click '清空投注' | Out-Null
"  清空后未选择: $(HasText '当前投注：未选择')"
MouseClick '旋转轮盘' | Out-Null
Start-Sleep -Milliseconds 1200
KillModal
Snap 'G03-roulette-empty-guard'
"  空投注被拦截(仍在选择页): $(HasText '当前投注：未选择')"
Click '红色 1:1' | Out-Null
Click '旋转轮盘' | Out-Null
Start-Sleep -Seconds 4
"  旋转后有最近结果: $(HasText '最近结果')"
Snap 'G04-roulette-spun'
Click '清空投注' | Out-Null
Click '重复上局投注' | Out-Null
"  重复上局恢复一项: $(HasText '共 1 项')"
Snap 'G05-roulette-repeat'
Back

Write-Output '== 21点 双倍下注 =='
EnterGame '21 点'
Click '开始新局' | Out-Null
"  开局下注 " + '$20' + ": " + (HasText '本局下注 $20')
Snap 'G06-blackjack-dealt'
Click '双倍下注' | Out-Null
"  双倍后下注 " + '$40' + ": " + (HasText '本局下注 $40')
Snap 'G07-blackjack-doubled'
"  双倍后有结果: $(HasText '点')"
Back

Write-Output '== 炸金花 加注 =='
EnterGame '炸金花'
Click '新开一局' | Out-Null
Click '看牌' | Out-Null
Click '加注' | Out-Null
"  加注后底池: $(HasText '底池：$')"
Snap 'G08-three-raise'
Click '弃牌' | Out-Null
Back

Write-Output '== 骗子扑克 表情与质疑 =='
EnterGame '骗子扑克'
Click '新开一局' | Out-Null
Click '手牌' 0 | Out-Null
Click '盖牌并声明' | Out-Null
Snap 'G09-liar-played'
Click '质疑上一手' | Out-Null
"  质疑后翻开: $(HasText '翻开')"
Snap 'G10-liar-challenge'
Click '新开一局' | Out-Null
PressHold '😄' 900 | Out-Null
Snap 'G11-liar-emoji'
"  表情按住后变化: $(HasText '😄')"
Back

Write-Output '== 智能模式 + 极强档位 =='
Click '切换模式' | Out-Null
Set-Combo 2 '极强'
EnterGame '21 点'
Click '开始新局' | Out-Null
"  智能极强可开局: $(HasText '本局下注')"
Snap 'G12-smart-toptier'
Back
Click '切换模式' | Out-Null

Write-Output '== 大厅 ToolTip 悬停 =='
$card = All ([System.Windows.Automation.ControlType]::Text) | Where-Object { $_.Current.Name -like '*欧洲单零轮盘共*' } | Select-Object -First 1
$title = All ([System.Windows.Automation.ControlType]::Text) | Where-Object { $_.Current.Name -like '*欧洲轮盘*' } | Select-Object -First 1
if ($title) {
    $r = $title.Current.BoundingRectangle
    [Mouse]::Move([int]($r.Left + 20), [int]($r.Top + 10))
    Start-Sleep -Seconds 3
    Snap 'G13-lobby-tooltip'
}
"  进程存活: $(-not $p.HasExited)"

Write-Output '== 崩溃日志 =='
"  crash.log 存在: $(Test-Path $crash)"
if (Test-Path $crash) { Get-Content $crash -Raw }

Stop-Process -Id $p.Id -Force
'DONE'
