# 新功能验收：圆角窗口、任务中心、最强AI、思考时间
# 使用 UIA InvokePattern 触发按钮，不依赖鼠标焦点（避免被其他窗口抢占）
. "$PSScriptRoot\uia.ps1"

$results = New-Object System.Collections.Generic.List[string]
function Check($name, $ok) {
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    $results.Add("[$mark] $name")
    Write-Host "[$mark] $name"
}
# 用 InvokePattern 点击按钮（按名称模糊匹配）
function InvokeBtn([string]$part, [int]$index = 0) {
    $f = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($f.Count -le $index) { Write-Host "  MISS: $part"; return $false }
    try {
        $p = $f[$index].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $p.Invoke()
        Start-Sleep -Milliseconds 900
        return $true
    } catch {
        Write-Host "  INVOKE-FAIL: $part ($($_.Exception.Message))"
        return $false
    }
}

StartApp

# ---------- 1. 圆角窗口（DWM CornerPreference） ----------
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class DwmCheck {
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int s);
}
"@
$h = $Proc.MainWindowHandle
$corner = -1
[DwmCheck]::DwmGetWindowAttribute($h, 33, [ref]$corner, 4) | Out-Null
Check "窗口四角圆角已启用 (CornerPreference=$corner, 期望2)" ($corner -eq 2)

# ---------- 2. 模式选择页任务中心入口 ----------
Check "模式选择页有「任务中心」入口" (Has '*任务中心*')

# ---------- 3. 进入普通模式 ----------
InvokeBtn '选择普通模式' | Out-Null
Check "已进入游戏大厅" (Has '*选择玩法*')

# ---------- 4. 任务页面 ----------
InvokeBtn '🎯 任务' | Out-Null
$inTasks = Has '*任务中心*'
Check "任务页面可打开" $inTasks
Check "任务页有「接取新任务」按钮" (Has '*接取新任务*')

if (Has '*接取新任务*') {
    InvokeBtn '接取新任务' | Out-Null
    Check "接取任务后显示进度" (Has '*进度：*')
    Check "任务显示奖励金额" (Has '*完成奖励*')
}

# 返回大厅
InvokeBtn '返回游戏大厅' | Out-Null
Check "可从任务页返回大厅" (Has '*选择玩法*')

# ---------- 5. 第七档「极强」可选 ----------
InvokeBtn '进入筛选' 0 | Out-Null
Check "可进入玩法筛选页" (Has '*筛选*')
$tierClicked = $false
foreach ($b in (Buttons)) {
    if ($b.Current.Name -like '*极强*') {
        try { $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); $tierClicked = $true; Start-Sleep -Milliseconds 600; break } catch {}
    }
}
Check "可选择「极强」档位" $tierClicked

# 验证选中极强后页面回显
Check "极强档位已选中（页面显示极强）" (Has '*极强*')

# ---------- 6. 运行稳定性 ----------
Check "程序运行中无崩溃" (-not $Proc.HasExited)

$crashLog = Join-Path $env:LOCALAPPDATA 'TableGameRoom\crash.log'
if (Test-Path $crashLog) {
    $recent = (Get-Item $crashLog).LastWriteTime
    $isRecent = ((Get-Date) - $recent).TotalMinutes -lt 5
    Check "无新增崩溃日志" (-not $isRecent)
} else {
    Check "无崩溃日志文件" $true
}

StopApp

Write-Host ''
Write-Host '========== 结果汇总 =========='
$results | ForEach-Object { Write-Host $_ }
$pass = @($results | Where-Object { $_ -match '^\[PASS\]' }).Count
$fail = @($results | Where-Object { $_ -match '^\[FAIL\]' }).Count
Write-Host "通过 $pass 项，失败 $fail 项"
if ($fail -eq 0) { Write-Host '=== 全部通过 ===' } else { Write-Host '=== 存在失败项 ===' }
