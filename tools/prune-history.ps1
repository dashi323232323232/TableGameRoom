# 只保留「最新版本」和「上一个版本」两个提交
#
# 用法：
#   pwsh -NoProfile -ExecutionPolicy Bypass -File .\tools\prune-history.ps1
#
# 原理：把当前 HEAD 和它的父提交提取出来，重建为一个只有 2 个提交的新历史。
# 旧的提交对象会在 gc 后消失，仓库体积保持很小。
#
# 注意：这会重写历史。已经推送到远端的仓库不要用（本项目纯本地，无远端）。

param(
    [int]$Keep = 2
)

$ErrorActionPreference = 'Stop'

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $count = [int](git rev-list --count HEAD)
    Write-Host "当前提交数: $count（保留 $Keep 个）"

    if ($count -le $Keep) {
        Write-Host "已经是 $count 个版本，无需精简。"
        return
    }

    # 取需要保留的提交（从旧到新）
    $keepHashes = @(git rev-list --reverse HEAD | Select-Object -Last $Keep)
    $keepMessages = @()
    foreach ($h in $keepHashes) {
        $keepMessages += (git log -1 --format=%B $h)
    }
    $tags = @{}
    foreach ($t in (git tag -l)) {
        $th = (git rev-list -n 1 $t)
        if ($keepHashes -contains $th) { $tags[$th] = $t }
    }

    Write-Host "保留的提交:"
    for ($i = 0; $i -lt $keepHashes.Count; $i++) {
        Write-Host "  [$i] $($keepHashes[$i].Substring(0,8))  $($keepMessages[$i].Split("`n")[0])"
    }

    $branch = (git rev-parse --abbrev-ref HEAD)

    # 用第一个保留提交的树创建 orphan 分支，逐个重放
    git checkout --orphan __prune_tmp $keepHashes[0] --quiet 2>&1 | Out-Null
    git reset --mixed --quiet
    git add -A
    git commit --quiet -m $keepMessages[0]
    $newHashes = @((git rev-parse HEAD))

    for ($i = 1; $i -lt $keepHashes.Count; $i++) {
        git checkout $keepHashes[$i] -- . 2>&1 | Out-Null
        # 删除在新版本里已不存在的文件
        $current = @(git ls-files)
        $target = @(git ls-tree -r --name-only $keepHashes[$i])
        foreach ($f in $current) {
            if ($target -notcontains $f) { git rm --quiet --cached --ignore-unmatch $f | Out-Null; Remove-Item $f -Force -ErrorAction SilentlyContinue }
        }
        git add -A
        git commit --quiet -m $keepMessages[$i]
        $newHashes += (git rev-parse HEAD)
    }

    # 用新历史替换原分支
    git branch -D $branch --quiet
    git branch -m $branch

    # 重新打标签
    foreach ($t in @(git tag -l)) { git tag -d $t | Out-Null }
    for ($i = 0; $i -lt $newHashes.Count; $i++) {
        if ($tags.ContainsKey($keepHashes[$i])) { git tag $tags[$keepHashes[$i]] $newHashes[$i] }
    }

    # 清掉旧对象
    git reflog expire --expire=now --all
    git gc --prune=now --aggressive --quiet

    Write-Host ""
    Write-Host "完成。新历史:"
    git log --oneline
    Write-Host ""
    Write-Host "标签: $((git tag -l) -join ', ')"
    Write-Host "仓库体积: $([math]::Round((Get-ChildItem .git -Recurse -File | Measure-Object Length -Sum).Sum / 1KB, 0)) KB"
}
finally {
    Pop-Location
}
