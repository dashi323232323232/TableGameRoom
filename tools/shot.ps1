Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

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
    "  saved $name"
}
function All($type) {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type)
    $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
}
function Invoke-El($el) {
    $el.SetFocus(); Start-Sleep -Milliseconds 150
    $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 600
}
function Click([string]$part, [int]$index = 0, [switch]$Exact) {
    $found = All ([System.Windows.Automation.ControlType]::Button) | Where-Object {
        if ($Exact) { $_.Current.Name -eq $part } else { $_.Current.Name -like ('*' + $part + '*') }
    }
    $sorted = @($found | Sort-Object { $_.Current.BoundingRectangle.Top }, { $_.Current.BoundingRectangle.Left })
    if ($sorted.Count -le $index) { "  MISS button: $part"; return $false }
    Invoke-El $sorted[$index]
    "  clicked: " + ($sorted[$index].Current.Name -replace "`n", ' ')
    return $true
}
function Back { Click '返回游戏大厅' | Out-Null }
function EnterGame([string]$title) {
    $el = All ([System.Windows.Automation.ControlType]::Text) | Where-Object { $_.Current.Name -like ('*' + $title + '*') } | Select-Object -First 1
    if (-not $el) { "  MISS card: $title"; return }
    $t = $el.Current.BoundingRectangle
    $btn = All ([System.Windows.Automation.ControlType]::Button) |
        Where-Object { $_.Current.Name -like '*进入游戏*' -and $_.Current.BoundingRectangle.Top -ge $t.Top } |
        Sort-Object { [math]::Abs($_.Current.BoundingRectangle.Left - $t.Left) } |
        Select-Object -First 1
    if (-not $btn) { "  MISS enter: $title"; return }
    Invoke-El $btn
    "  entered: $title"
}
function Names { (All ([System.Windows.Automation.ControlType]::Button) | Sort-Object { $_.Current.BoundingRectangle.Top }, { $_.Current.BoundingRectangle.Left } | ForEach-Object { $_.Current.Name -replace "`n", ' ' }) -join ' | ' }

Write-Output '== 模式选择 =='
Snap 'V00-mode'
Click '存档模式' | Out-Null
Snap 'V01-lobby'
Write-Output ("lobby: " + (Names))

Write-Output '== 轮盘 =='
EnterGame '01  欧洲轮盘'
Snap 'V02-roulette'
Click '红色 1:1' | Out-Null
Click '5' 0 -Exact | Out-Null
Snap 'V03-roulette-bets'
Click '旋转轮盘' | Out-Null
Start-Sleep -Seconds 4
Snap 'V04-roulette-spun'
Back

Write-Output '== 21点 =='
EnterGame '02  21 点'
Click '开始新局' | Out-Null
Snap 'V05-blackjack-dealt'
Click '要牌' | Out-Null
Snap 'V06-blackjack-hit'
Click '停牌' | Out-Null
Snap 'V07-blackjack-result'
Back

Write-Output '== 德州扑克 =='
EnterGame '03  德州扑克'
Click '开始牌局' | Out-Null
Snap 'V08-poker-preflop'
Click '跟注' | Out-Null
Snap 'V09-poker-flop'
Click '跟注' | Out-Null
Snap 'V10-poker-turn'
Click '跟注' | Out-Null
Snap 'V11-poker-river'
Click '跟注' | Out-Null
Snap 'V12-poker-showdown'
Write-Output ("poker end: " + (Names))
Back

Write-Output '== 炸金花 =='
EnterGame '04  炸金花'
Click '新开一局' | Out-Null
Snap 'V13-three-start'
Click '看牌' | Out-Null
Snap 'V14-three-seen'
Click '跟注' | Out-Null
Snap 'V15-three-call'
Click '比牌' | Out-Null
Snap 'V16-three-compare'
Back

Write-Output '== 骗子扑克 =='
EnterGame '05  骗子扑克'
Click '新开一局' | Out-Null
Snap 'V17-liar-start'
Click '手牌' 0 | Out-Null
Click '手牌' 1 | Out-Null
Snap 'V18-liar-selected'
Click '盖牌并声明' | Out-Null
Snap 'V19-liar-played'
Click '质疑上一手' | Out-Null
Snap 'V20-liar-challenge'
Write-Output ("liar: " + (Names))

Stop-Process -Id $p.Id -Force
'DONE'
