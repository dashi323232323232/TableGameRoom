# 版本维护：检查当前版本状态
#
# 用法：pwsh -NoProfile -ExecutionPolicy Bypass -File .\tools\version-status.ps1

$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $count = [int](git rev-list --count HEAD)
    Write-Host "=== 版本状态 ==="
    Write-Host "提交数: $count （策略：只保留最新 + 上一版，共 2 个）"
    Write-Host ""

    Write-Host "=== 历史 ==="
    git log --oneline --decorate
    Write-Host ""

    Write-Host "=== 标签 ==="
    if (git tag -l) { git tag -l } else { Write-Host "（无）" }
    Write-Host ""

    Write-Host "=== 工作区 ==="
    $st = @(git status --short)
    if ($st.Count -eq 0) { Write-Host "干净，无未提交改动" }
    else {
        Write-Host "有 $($st.Count) 项未提交："
        $st | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" }
        if ($st.Count -gt 20) { Write-Host "  ...还有 $($st.Count - 20) 项" }
    }
    Write-Host ""

    $size = [math]::Round((Get-ChildItem .git -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum / 1KB, 0)
    Write-Host "仓库体积: $size KB"

    if ($count -gt 2) {
        Write-Host ""
        Write-Host "⚠ 当前有 $count 个提交，超过策略上限 2 个。"
        Write-Host "  运行 tools\prune-history.ps1 精简到最新 + 上一版。"
    }
}
finally {
    Pop-Location
}
