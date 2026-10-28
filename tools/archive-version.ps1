# 归档并精简：GitHub 保留全部版本，本地只留最新 2 个
#
# 用法：
#   pwsh -NoProfile -ExecutionPolicy Bypass -File .\tools\archive-version.ps1
#
# 工作流程：
#   1. 检查工作区是否干净（有改动会先提示）
#   2. 给当前 HEAD 打一个 tag（如果还没有）
#   3. 推送到 GitHub —— 所有历史 + 所有 tag 都会永久保存在远端
#   4. 本地精简到最近 2 个提交（旧提交从本地删除，但 GitHub 上还在）
#
# 关键原理：
#   git tag 是「永久锚点」。一旦 tag 被 push 到 GitHub，那个提交就被永久引用，
#   即使本地 gc 掉、force push 覆盖分支，GitHub 上的 tag 依然指向完整快照。
#   想恢复任何旧版本：git fetch --tags 然后 git checkout <tag>

param(
    [string]$Remote = "origin",
    [int]$KeepLocal = 2,
    [string]$Proxy = "http://127.0.0.1:7897"
)

$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)

try {
    # ---------- 0. 环境检查 ----------
    if (-not (Test-Path .git)) { throw "当前目录不是 git 仓库" }

    $branch = (git rev-parse --abbrev-ref HEAD)
    Write-Host "=== 仓库：$((Get-Location).Path) ==="
    Write-Host "分支：$branch"

    $dirty = @(git status --porcelain)
    if ($dirty.Count -gt 0) {
        Write-Host ""
        Write-Host "⚠ 工作区有 $($dirty.Count) 项未提交改动："
        $dirty | Select-Object -First 10 | ForEach-Object { Write-Host "    $_" }
        if ($dirty.Count -gt 10) { Write-Host "    ...还有 $($dirty.Count - 10) 项" }
        Write-Host ""
        $ans = Read-Host "先提交这些改动？(y = 提交后继续 / n = 取消)"
        if ($ans -ne 'y') { Write-Host "已取消"; return }

        git add -A
        $msg = Read-Host "提交信息（留空用默认）"
        if ([string]::IsNullOrWhiteSpace($msg)) { $msg = "更新 $(Get-Date -Format 'yyyy-MM-dd HH:mm')" }
        git commit --quiet -m $msg
        Write-Host "已提交：$msg"
    }

    # ---------- 1. 确保 HEAD 有 tag ----------
    $head = (git rev-parse HEAD)
    $headTags = @(git tag --points-at $head)

    if ($headTags.Count -eq 0) {
        # 自动生成版本号：已有 vX.Y 就 +0.1
        $existing = @(git tag -l 'v*' | Where-Object { $_ -match '^v\d+\.\d+$' })
        $next = "v0.1"
        if ($existing.Count -gt 0) {
            $versions = $existing | ForEach-Object {
                $p = $_ -replace '^v', '' -split '\.'
                [pscustomobject]@{ Major = [int]$p[0]; Minor = [int]$p[1] }
            } | Sort-Object Major, Minor
            $last = $versions[-1]
            $next = "v$($last.Major).$($last.Minor + 1)"
        }

        Write-Host ""
        Write-Host "当前 HEAD 没有 tag。建议版本号：$next"
        $input = Read-Host "输入版本号（回车用 $next）"
        if (-not [string]::IsNullOrWhiteSpace($input)) { $next = $input.Trim() }

        git tag $next $head
        Write-Host "已打 tag：$next"
        $headTags = @($next)
    }
    else {
        Write-Host "当前 HEAD 已有 tag：$($headTags -join ', ')"
    }

    # ---------- 2. 推送到 GitHub（完整历史 + 全部 tag）----------
    Write-Host ""
    Write-Host "=== 推送到 $Remote（完整历史 + 全部 tag）==="

    $allTags = @(git tag -l)
    Write-Host "本地 tag：$($allTags -join ', ')"

    # 用 --tags 推全部标签，main 分支用 --force（允许历史重写后覆盖）
    $gitArgs = @('-c', "http.proxy=$Proxy", 'push', $Remote, $branch, '--force')
    & git @gitArgs 2>&1 | ForEach-Object { Write-Host "  $_" }

    $tagArgs = @('-c', "http.proxy=$Proxy", 'push', $Remote, '--tags', '--force')
    & git @tagArgs 2>&1 | ForEach-Object { Write-Host "  $_" }

    Write-Host ""
    Write-Host "✓ 已推送。GitHub 上现在有 $($allTags.Count) 个版本（含全部历史）"

    # ---------- 3. 本地精简 ----------
    $count = [int](git rev-list --count HEAD)
    Write-Host ""
    Write-Host "=== 本地精简：当前 $count 个提交，目标 $KeepLocal 个 ==="

    if ($count -le $KeepLocal) {
        Write-Host "已经是 $count 个，无需精简。"
    }
    else {
        $keepHashes = @(git rev-list --reverse HEAD | Select-Object -Last $KeepLocal)

        $subjects = @()
        foreach ($h in $keepHashes) {
            $s = (git log -1 --format=%s $h)
            if ([string]::IsNullOrWhiteSpace($s)) { $s = "版本 $($h.Substring(0,8))" }
            $subjects += $s
        }

        # 记录 tag → commit 映射
        $tagMap = @{}
        foreach ($t in @(git tag -l)) {
            $th = (git rev-list -n 1 $t)
            if ($keepHashes -contains $th) { $tagMap[$th] = $t }
        }

        Write-Host "保留："
        for ($i = 0; $i -lt $keepHashes.Count; $i++) {
            Write-Host "  $($keepHashes[$i].Substring(0,8))  $($subjects[$i])"
        }

        $stashed = $false
        if (@(git status --porcelain).Count -gt 0) {
            git stash push --include-untracked --quiet -m "archive-tmp"
            $stashed = $true
        }

        try {
            git checkout --orphan __archive_tmp $keepHashes[0] --quiet 2>&1 | Out-Null
            git reset --mixed --quiet
            git add -A
            git commit --quiet -m $subjects[0]
            $newHashes = @((git rev-parse HEAD))

            for ($i = 1; $i -lt $keepHashes.Count; $i++) {
                git rm -r --cached --quiet . 2>&1 | Out-Null
                Get-ChildItem -Force |
                    Where-Object { $_.Name -ne '.git' } |
                    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
                git checkout $keepHashes[$i] -- . 2>&1 | Out-Null
                git add -A
                git commit --quiet -m $subjects[$i]
                $newHashes += (git rev-parse HEAD)
            }

            git checkout -B $branch --quiet
            git branch -D __archive_tmp --quiet 2>&1 | Out-Null

            # 重打 tag（指向新提交）
            foreach ($t in @(git tag -l)) { git tag -d $t | Out-Null }
            for ($i = 0; $i -lt $newHashes.Count; $i++) {
                if ($tagMap.ContainsKey($keepHashes[$i])) {
                    git tag $tagMap[$keepHashes[$i]] $newHashes[$i]
                }
            }
        }
        finally {
            if ($stashed) { git stash pop --quiet 2>&1 | Out-Null }
        }

        git reflog expire --expire=now --all 2>&1 | Out-Null
        git gc --prune=now --quiet 2>&1 | Out-Null
    }

    # ---------- 4. 汇总 ----------
    Write-Host ""
    Write-Host "=== 完成 ==="
    git log --oneline --decorate
    Write-Host ""
    Write-Host "本地 tag：$((git tag -l) -join ', ')"
    $size = [math]::Round((Get-ChildItem .git -Recurse -File -ErrorAction SilentlyContinue |
        Measure-Object Length -Sum).Sum / 1KB, 0)
    Write-Host "本地仓库体积：$size KB"
    Write-Host ""
    Write-Host "提示：GitHub 上保留了全部版本。要恢复旧版本："
    Write-Host "  git fetch --tags"
    Write-Host "  git checkout <tag名>"
}
finally {
    Pop-Location
}
