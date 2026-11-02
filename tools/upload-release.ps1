# 上传安装包到 GitHub Release
#
# 用法：
#   pwsh -NoProfile -ExecutionPolicy Bypass -File .\tools\upload-release.ps1 -Tag v2.4 -File "H:\软件安装包\TABLE游戏厅-Setup.exe"
#
# 为什么用 Release 而不是直接提交：
#   GitHub 仓库单文件上限 100 MB，安装包 136 MB 超限；
#   GitHub Release 单文件上限 2 GB，可以放。
#
# 上传后可以从 Release 页面直接下载，也能通过固定 URL 分发。

param(
    [Parameter(Mandatory = $true)][string]$Tag,
    [Parameter(Mandatory = $true)][string]$File,
    [string]$Repo = "",
    [string]$Name = "",
    [string]$Notes = "",
    [string]$Proxy = "http://127.0.0.1:7897"
)

$ErrorActionPreference = 'Stop'

# ---------- 定位仓库 ----------
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if ([string]::IsNullOrWhiteSpace($Repo)) {
        $url = (git remote get-url origin 2>&1)
        if ($url -match 'github\.com[:/]([^/]+)/([^/.]+)') {
            $Repo = "$($Matches[1])/$($Matches[2])"
        }
        else {
            throw "无法从 git remote 推断仓库，请用 -Repo owner/name 指定"
        }
    }
    Write-Host "仓库: $Repo"
}
finally { Pop-Location }

# ---------- Token ----------
$tokenFile = Join-Path $env:USERPROFILE ".dsh-gh-token"
if (-not (Test-Path $tokenFile)) { throw "缺少 token 文件：$tokenFile" }
$token = (Get-Content $tokenFile -Raw).Trim()

$headers = @{
    Authorization          = "token $token"
    Accept                 = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

$apiBase = "https://api.github.com/repos/$Repo"

function Invoke-GhApi {
    param([string]$Method, [string]$Uri, $Body, [string]$ContentType = "application/json")
    $params = @{
        Method      = $Method
        Uri         = $Uri
        Headers     = $headers
        Proxy       = $Proxy
        TimeoutSec  = 120
        ErrorAction = 'Stop'
    }
    if ($Body) {
        $params.Body = $Body
        $params.ContentType = $ContentType
    }
    return Invoke-RestMethod @params
}

# ---------- 1. 确保 tag 存在 ----------
Write-Host ""
Write-Host "=== 检查 tag: $Tag ==="
try {
    $ref = Invoke-GhApi -Method GET -Uri "$apiBase/git/ref/tags/$Tag"
    Write-Host "tag 已存在"
}
catch {
    throw "tag '$Tag' 在 GitHub 上不存在。请先推送 tag：git push origin --tags"
}

# ---------- 2. 创建或获取 Release ----------
Write-Host ""
Write-Host "=== 创建/获取 Release ==="
$release = $null
try {
    $release = Invoke-GhApi -Method GET -Uri "$apiBase/releases/tags/$Tag"
    Write-Host "Release 已存在: $($release.html_url)"
}
catch {
    if ([string]::IsNullOrWhiteSpace($Name)) { $Name = "TABLE 游戏厅 $Tag" }
    if ([string]::IsNullOrWhiteSpace($Notes)) { $Notes = "版本 $Tag 安装包" }

    $body = @{
        tag_name = $Tag
        name     = $Name
        body     = $Notes
        draft    = $false
        prerelease = $false
    } | ConvertTo-Json

    $release = Invoke-GhApi -Method POST -Uri "$apiBase/releases" -Body $body
    Write-Host "✓ Release 已创建: $($release.html_url)"
}

# ---------- 3. 上传文件 ----------
$fileInfo = Get-Item $File
$sizeMB = [math]::Round($fileInfo.Length / 1MB, 1)
$assetName = $fileInfo.Name

Write-Host ""
Write-Host "=== 上传文件 ==="
Write-Host "  文件: $($fileInfo.FullName)"
Write-Host "  大小: $sizeMB MB"

# 如果同名 asset 已存在，先删除
$existing = @($release.assets | Where-Object { $_.name -eq $assetName })
foreach ($a in $existing) {
    Write-Host "  删除已存在的同名文件..."
    Invoke-GhApi -Method DELETE -Uri "$apiBase/releases/assets/$($a.id)" | Out-Null
}

$uploadUrl = "https://uploads.github.com/repos/$Repo/releases/$($release.id)/assets?name=$([uri]::EscapeDataString($assetName))"

Write-Host "  上传中（$sizeMB MB，可能需要几分钟）..."

$bytes = [System.IO.File]::ReadAllBytes($fileInfo.FullName)
$uploadHeaders = @{
    Authorization          = "token $token"
    Accept                 = "application/vnd.github+json"
    "Content-Type"         = "application/octet-stream"
    "X-GitHub-Api-Version" = "2022-11-28"
}

$asset = Invoke-RestMethod -Method POST -Uri $uploadUrl -Headers $uploadHeaders `
    -Body $bytes -Proxy $Proxy -TimeoutSec 1800

Write-Host ""
Write-Host "✓ 上传成功"
Write-Host "  文件名: $($asset.name)"
Write-Host "  大小:   $([math]::Round($asset.size / 1MB, 1)) MB"
Write-Host "  下载:   $($asset.browser_download_url)"
Write-Host ""
Write-Host "=== Release 页面 ==="
Write-Host $release.html_url
