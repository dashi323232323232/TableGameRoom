# 构建 Windows 安装包
#
# 用法：
#   pwsh -NoProfile -ExecutionPolicy Bypass -File .\tools\build-installer.ps1
#
# 它做的事：
#   1. 单文件发布主程序到 publish/
#   2. 复制到 installer/payload/
#   3. 发布安装器（把主程序作为嵌入资源）
#   4. 输出到 installer/bin/Release/net8.0-windows/win-x64/publish/TABLE游戏厅-Setup.exe

param(
    [string]$OutputDir = "",
    [string]$Proxy = ""
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root

try {
    # ---------- 1. 主程序单文件发布 ----------
    Write-Host "=== 发布主程序 ==="

    Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 800

    if (Test-Path publish) { Remove-Item -Recurse -Force publish -ErrorAction SilentlyContinue }

    dotnet publish TableGameRoom.csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o publish 2>&1 | Select-Object -Last 3

    $exe = Join-Path $root 'publish\TableGameRoom.exe'
    if (-not (Test-Path $exe)) { throw "主程序发布失败：找不到 $exe" }
    $exeMB = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "✓ 主程序：$exeMB MB"

    # ---------- 2. 复制载荷 ----------
    Write-Host ""
    Write-Host "=== 准备安装器载荷 ==="

    $payloadDir = Join-Path $root 'installer\payload'
    New-Item -ItemType Directory -Force -Path $payloadDir | Out-Null
    Copy-Item $exe (Join-Path $payloadDir 'TableGameRoom.exe') -Force
    Write-Host "✓ 已复制到 installer/payload/"

    # ---------- 3. 发布安装器 ----------
    Write-Host ""
    Write-Host "=== 发布安装器 ==="

    Push-Location (Join-Path $root 'installer')
    try {
        if (Test-Path bin) { Remove-Item -Recurse -Force bin -ErrorAction SilentlyContinue }
        if (Test-Path obj) { Remove-Item -Recurse -Force obj -ErrorAction SilentlyContinue }

        dotnet publish Setup.csproj -c Release 2>&1 | Select-Object -Last 3

        $setup = Join-Path $root 'installer\bin\Release\net8.0-windows\win-x64\publish\TABLE游戏厅-Setup.exe'
        if (-not (Test-Path $setup)) { throw "安装器发布失败：找不到 $setup" }

        $setupMB = [math]::Round((Get-Item $setup).Length / 1MB, 1)
        Write-Host "✓ 安装包：$setupMB MB"

        # ---------- 4. 可选：复制到指定目录 ----------
        if (-not [string]::IsNullOrWhiteSpace($OutputDir)) {
            New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
            $dest = Join-Path $OutputDir 'TABLE游戏厅-Setup.exe'
            Copy-Item $setup $dest -Force
            Write-Host "✓ 已复制到 $dest"
        }

        Write-Host ""
        Write-Host "=== 完成 ==="
        Write-Host "安装包：$setup"
    }
    finally { Pop-Location }
}
finally { Pop-Location }
