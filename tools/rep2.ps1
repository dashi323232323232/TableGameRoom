1..2 | ForEach-Object {
    $out = "D:\dsh\xin\tools\out2_$_.txt"
    pwsh -NoProfile -ExecutionPolicy Bypass -File D:\dsh\xin\tools\verify2.ps1 *> $out
    $line = (Get-Content $out | Select-String -Pattern 'FAIL|项检查' | ForEach-Object { $_.Line.Trim() }) -join ' ; '
    Write-Host ("run {0}: {1}" -f $_, $line)
}
