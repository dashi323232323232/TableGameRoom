$ErrorActionPreference = 'Stop'
$exe = 'D:\dsh\xin\publish\TableGameRoom.exe'

Write-Host "=== 1. 二进制里搜索明文验证码 ==="
$bytes = [System.IO.File]::ReadAllBytes($exe)
$ascii = [System.Text.Encoding]::ASCII.GetString($bytes)
$unicode = [System.Text.Encoding]::Unicode.GetString($bytes)

$codeFile = Join-Path $PSScriptRoot '.testcode'
$Code = if (Test-Path $codeFile) { (Get-Content $codeFile -Raw).Trim() } else { '' }
$probes = if ($Code) { @($Code, $Code.ToLower(), $Code.ToUpper(), $Code.Substring(2), $Code.Substring(0, 6)) } else { @() }

foreach ($probe in $probes) {
    $hitA = $ascii.Contains($probe)
    $hitU = $unicode.Contains($probe)
    $mark = if ($hitA -or $hitU) { 'FOUND !!' } else { 'not found' }
    Write-Host ("  {0,-12} ASCII={1,-6} UTF16={2,-6} -> {3}" -f $probe, $hitA, $hitU, $mark)
}

Write-Host ""
Write-Host "=== 2. 搜索是否有明显的 'password'/'code' 明文常量 ==="
foreach ($probe in @('multiCode', 'verifyCode', 'unlockCode', 'SecretCode')) {
    Write-Host ("  {0,-14} -> {1}" -f $probe, $(if ($ascii.Contains($probe)) { 'FOUND' } else { 'not found' }))
}

Write-Host ""
Write-Host "=== 3. 反编译检查：多开相关字符串 ==="
$strings = [regex]::Matches($ascii, '[\x20-\x7E]{6,}') | ForEach-Object { $_.Value } |
    Where-Object { $_ -match 'multi|Multi|unlock|Unlock' } | Select-Object -Unique -First 20
foreach ($s in $strings) { Write-Host "  $s" }

Write-Host ""
Write-Host "=== 4. 解锁标记文件内容 ==="
$flag = Join-Path $env:LOCALAPPDATA 'TableGameRoom\.unlock'
if (Test-Path $flag) {
    $content = Get-Content $flag -Raw
    Write-Host "  内容: $content"
    Write-Host "  是否含明文验证码: $($Code -and $content.Contains($Code))"
} else {
    Write-Host "  （标记文件不存在）"
}
