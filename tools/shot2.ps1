Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Mouse {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, int e);
    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(150);
        mouse_event(0x0002, 0, 0, 0, 0);
        mouse_event(0x0004, 0, 0, 0, 0);
    }
}
'@

$exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
$outDir = 'D:\dsh\xin\tools\shots'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

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
function EnterCount { @(Buttons | Where-Object { $_.Current.Name -like '*进入游戏*' }).Count }
function HasText([string]$needle) {
    $hit = All ([System.Windows.Automation.ControlType]::Text) | Where-Object { $_.Current.Name -like ('*' + $needle + '*') } | Select-Object -First 1
    return [bool]$hit
}
function CountText([string]$prefix) {
    return @(All ([System.Windows.Automation.ControlType]::Text) | Where-Object { $_.Current.Name -like ($prefix + '*') }).Count
}
function Invoke-El($el) {
    $el.SetFocus(); Start-Sleep -Milliseconds 120
    $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 600
}
function Click([string]$part, [int]$index = 0) {
    $found = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($found.Count -le $index) { "  MISS button: $part"; return $false }
    Invoke-El $found[$index]
    return $true
}
function MouseClick([string]$part, [int]$index = 0) {
    $found = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($found.Count -le $index) { "  MISS button: $part"; return $false }
    $r = $found[$index].Current.BoundingRectangle
    [Mouse]::Click([int]($r.Left + $r.Width / 2), [int]($r.Top + $r.Height / 2))
    Start-Sleep -Milliseconds 700
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
function Combos { @(All ([System.Windows.Automation.ControlType]::ComboBox) | Sort-Object { $_.Current.BoundingRectangle.Left }) }
function Set-Combo([int]$index, [string]$item) {
    $list = Combos
    if ($list.Count -le $index) { "  MISS combo $index (have $($list.Count))"; return }
    try { $list[$index].GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand() } catch { }
    Start-Sleep -Milliseconds 600
    $items = @(All ([System.Windows.Automation.ControlType]::ListItem) |
        Where-Object { $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.Name -like ('*' + $item + '*') })
    if ($items.Count -eq 0) { "  MISS item: $item"; [System.Windows.Forms.SendKeys]::SendWait('{ESC}'); return }
    $li = $items[0]
    try {
        $li.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    }
    catch {
        $r = $li.Current.BoundingRectangle
        [Mouse]::Click([int]($r.Left + $r.Width / 2), [int]($r.Top + $r.Height / 2))
    }
    Start-Sleep -Milliseconds 900
    "  combo[$index] -> $item"
}

Write-Output '== 筛选组合 =='
Click '存档模式' | Out-Null
Set-Combo 2 '休闲'
"  初始卡片数: $(EnterCount)"
Set-Combo 0 '轮盘'
"  分类=轮盘 -> 卡片数 $(EnterCount)"
Set-Combo 0 '21 点'
"  分类=21点 -> 卡片数 $(EnterCount)"
Set-Combo 0 '桌面牌类'
"  分类=桌面牌类 -> 卡片数 $(EnterCount)"
Set-Combo 1 '双人桌'
"  桌面牌类+双人桌 -> 卡片数 $(EnterCount)"
Snap 'F01-filter-duo'
Set-Combo 1 '3–7 人'
"  桌面牌类+3-7人 -> 卡片数 $(EnterCount)"
Set-Combo 0 '全部游戏'
Set-Combo 1 '全部人数'
"  全部+全部 -> 卡片数 $(EnterCount)"

Write-Output '== 双人桌德州 =='
Set-Combo 1 '双人桌'
EnterGame '德州扑克'
"  德州双人桌文本: $(HasText '牌桌人数 2 人')"
"  开局前电脑座位数: $(CountText '电脑 1') / $(CountText '电脑 2')"
Click '开始牌局' | Out-Null
Snap 'F02-poker-duo'
"  开局后电脑座位数: $(CountText '电脑 1') / $(CountText '电脑 2')"
Click '弃牌' | Out-Null
Click '返回游戏大厅' | Out-Null
Set-Combo 1 '全部人数'

Write-Output '== 5 人炸金花 =='
Click '5' 0 | Out-Null
EnterGame '炸金花'
"  炸金花5人: $(HasText '牌桌人数 5 人')"
"  开局前座位数: $(CountText '电脑 1')/$(CountText '电脑 2')/$(CountText '电脑 3')/$(CountText '电脑 4')"
Click '新开一局' | Out-Null
Snap 'F03-three-5p'
"  开局后座位数: $(CountText '电脑 1')/$(CountText '电脑 2')/$(CountText '电脑 3')/$(CountText '电脑 4')"
Click '看牌' | Out-Null
Snap 'F03b-three-5p-seen'
Click '弃牌' | Out-Null
Click '返回游戏大厅' | Out-Null

Write-Output '== 智能模式 =='
Click '切换模式' | Out-Null
"  智能模式余额: $(HasText '余额：∞（智能模式）')"
Snap 'F05-smart-mode'
Click '退出到模式选择' | Out-Null
"  模式选择页: $(HasText '欢迎来到 TABLE 游戏厅')"

Write-Output '== 档位极强 + 余额校验 =='
Click '存档模式' | Out-Null
Set-Combo 2 '极强'
"  大厅档位: $(HasText '档位：极强')"
EnterGame '21 点'
"  21点档位文本: $(HasText '极强：$10,000 起，无上限')"
MouseClick '开始新局' | Out-Null
Start-Sleep -Milliseconds 1500
[System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
Start-Sleep -Milliseconds 900
Snap 'F04-tier-guard'
"  仍显示下注页(未开局): $(HasText '本局下注')"
Click '返回游戏大厅' | Out-Null
Set-Combo 2 '休闲'
"  档位已复位: $(HasText '休闲（$10')"

Write-Output '== 存档文件 =='
$save = Join-Path $env:LOCALAPPDATA 'TableGameRoom\progress.json'
"  progress.json 存在: $(Test-Path $save)"
if (Test-Path $save) { "  内容: " + (Get-Content $save -Raw) }

Stop-Process -Id $p.Id -Force
'DONE'
