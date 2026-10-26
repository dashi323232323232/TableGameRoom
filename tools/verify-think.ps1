# 验证电脑思考时间真实生效：打一局德州扑克，测量玩家点击后到电脑行动完成的耗时
. "$PSScriptRoot\uia.ps1"

function InvokeBtn([string]$part, [int]$index = 0) {
    $f = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($f.Count -le $index) { Write-Host "  MISS: $part"; return $false }
    try {
        $f[$index].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        return $true
    } catch { Write-Host "  FAIL: $part"; return $false }
}

StartApp
InvokeBtn '选择普通模式' | Out-Null; Start-Sleep -Milliseconds 900

# 进入德州扑克（第3张卡片）
$cards = @(Buttons | Where-Object { $_.Current.Name -like '*进入筛选*' })
Write-Host "进入筛选按钮数量: $($cards.Count)"
$cards[2].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 900
Write-Host "当前页文本: $(Line '*筛选*')"

# 选择「休闲」档（低档 → 思考应较短）
foreach ($b in (Buttons)) {
    if ($b.Current.Name -like '*休闲*') { $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); break }
}
Start-Sleep -Milliseconds 700

# 开始游戏（设置页）→ 进入牌桌
InvokeBtn '开始游戏' | Out-Null; Start-Sleep -Milliseconds 1400
Write-Host "进入牌桌: $(Line '*德州扑克*')"

# 牌桌上点「开始牌局」发牌
InvokeBtn '开始牌局' | Out-Null; Start-Sleep -Milliseconds 1800

# 找跟注按钮，测量点击到页面稳定的耗时
$callBtn = @(Buttons | Where-Object { $_.Current.Name -like '*跟注*' })[0]
if ($callBtn) {
    Write-Host "找到跟注按钮: $($callBtn.Current.Name)"
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $callBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    # 轮询等待页面文本变化（电脑行动完成会刷新日志）
    $before = (Line '*电脑*')
    $changed = $false
    for ($i = 0; $i -lt 200; $i++) {
        Start-Sleep -Milliseconds 100
        $now = (Line '*电脑*')
        if ($now -ne $before) { $changed = $true; break }
    }
    $sw.Stop()
    Write-Host "点击跟注 → 电脑行动完成耗时: $([math]::Round($sw.Elapsed.TotalSeconds,2)) 秒"
    if ($sw.Elapsed.TotalSeconds -ge 0.5) {
        Write-Host "[PASS] 电脑思考时间生效（>=0.5s）"
    } else {
        Write-Host "[FAIL] 电脑行动过快，思考时间可能未生效"
    }
} else {
    Write-Host "[FAIL] 未找到跟注按钮"
}

# 截图看看牌桌
Activate
Start-Sleep -Milliseconds 500
Add-Type -AssemblyName System.Drawing
$r = $Win.Current.BoundingRectangle
$w = [int]$r.Width; $hh = [int]$r.Height
$bmp = New-Object System.Drawing.Bitmap($w, $hh)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, (New-Object System.Drawing.Size($w, $hh)))
$bmp.Save('D:\dsh\_poker_table.png', [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "牌桌截图: D:\dsh\_poker_table.png"

StopApp
