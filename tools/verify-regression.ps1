# 回归验证：使用当前版本的界面文案，检查核心游戏功能
. "$PSScriptRoot\uia.ps1"

$results = New-Object System.Collections.Generic.List[string]
function Check($name, $ok, $detail = '') {
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    $results.Add("[$mark] $name  $detail")
    Write-Host "[$mark] $name  $detail"
}
function InvokeBtn([string]$part, [int]$index = 0) {
    $f = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($f.Count -le $index) { return $false }
    try { $f[$index].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 700; return $true }
    catch { return $false }
}
function EnterCard([string]$name) {
    $cards = @(Buttons | Where-Object { $_.Current.Name -like '*进入筛选*' })
    $idx = @('欧洲轮盘','21 点','德州扑克','炸金花','骗子扑克').IndexOf($name)
    if ($idx -ge 0 -and $cards.Count -gt $idx) {
        $cards[$idx].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 700
        return $true
    }
    return $false
}
# 检查按钮（而不只是文本）
function HasBtn([string]$part) { [bool](@(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') }).Count -gt 0) }
# 返回大厅：对局未结束时会有「确定放弃」确认框，需要点确定
function BackToLobby() {
    InvokeBtn '返回游戏大厅' | Out-Null
    Start-Sleep -Milliseconds 500
    if (HasBtn '确定放弃') { InvokeBtn '确定放弃' | Out-Null; Start-Sleep -Milliseconds 700 }
    Start-Sleep -Milliseconds 400
}

StartApp

# ---------- A 模式选择 ----------
Check '启动进入模式选择' ((Has '*选择普通模式*') -and (Has '*选择无限模式*'))
Check '显示当前存档信息' (Has '*当前存档*') (Line '*当前存档*')
Check '任务中心入口存在' (Has '*任务中心*')

# ---------- B 进入无限模式（余额无限，便于测试） ----------
InvokeBtn '选择无限模式' | Out-Null
Check '进入游戏大厅' (Has '*选择玩法*')
Check '大厅显示 5 张卡片' ((@(Texts | Where-Object { $_.Current.Name -like '0?  *' }).Count) -eq 5) "实际 $(@(Texts | Where-Object { $_.Current.Name -like '0?  *' }).Count)"

# ---------- C 分类筛选 ----------
InvokeBtn '轮盘' | Out-Null
Check '筛选轮盘剩 1 张' ((@(Texts | Where-Object { $_.Current.Name -like '0?  *' }).Count) -eq 1)
InvokeBtn '全部游戏' | Out-Null
Check '筛选全部恢复 5 张' ((@(Texts | Where-Object { $_.Current.Name -like '0?  *' }).Count) -eq 5)

# ---------- D 轮盘 ----------
EnterCard '欧洲轮盘' | Out-Null
Check '进入轮盘筛选页' (Has '*筛选 · 欧洲轮盘*')
Check '轮盘不支持人数筛选' (Has '*不支持人数*')
InvokeBtn '开始游戏' | Out-Null
Check '轮盘已开局' (Has '*旋转轮盘*') (Line '*投注*')
Check '轮盘有红黑选项' ((Has '*红色*') -and (Has '*黑色*'))

# ---------- E 21 点 ----------
BackToLobby
EnterCard '21 点' | Out-Null
InvokeBtn '开始游戏' | Out-Null
Check '21点进入牌桌' ((Has '*庄 家*') -or (Has '*庄家*'))
InvokeBtn '开始新局' | Out-Null
Check '21点已发牌（有要牌/停牌）' ((Has '*要牌*') -or (Has '*停牌*'))
Check '21点有双倍下注' (Has '*双倍下注*')

# ---------- F 炸金花 ----------
BackToLobby
EnterCard '炸金花' | Out-Null
InvokeBtn '开始游戏' | Out-Null
InvokeBtn '新开一局' | Out-Null
Check '炸金花已发牌' (HasBtn '跟注' -and HasBtn '看牌')

# ---------- G 骗子扑克 ----------
BackToLobby
EnterCard '骗子扑克' | Out-Null
InvokeBtn '开始游戏' | Out-Null
InvokeBtn '新开一局' | Out-Null
Check '骗子扑克已发牌' (HasBtn '盖牌' -or HasBtn '声明')

# ---------- H 稳定性 ----------
Check '全程无崩溃' (-not $Proc.HasExited)
$crashLog = Join-Path $env:LOCALAPPDATA 'TableGameRoom\crash.log'
if (Test-Path $crashLog) {
    $isRecent = ((Get-Date) - (Get-Item $crashLog).LastWriteTime).TotalMinutes -lt 5
    Check '无新增崩溃日志' (-not $isRecent)
} else { Check '无崩溃日志' $true }

StopApp

Write-Host ''
Write-Host '========== 结果汇总 =========='
$results | ForEach-Object { Write-Host $_ }
$pass = @($results | Where-Object { $_ -match '^\[PASS\]' }).Count
$fail = @($results | Where-Object { $_ -match '^\[FAIL\]' }).Count
Write-Host "通过 $pass 项，失败 $fail 项"
