. D:\dsh\xin\tools\uia.ps1
$suites = @('verify1', 'verify2', 'verify3', 'liar-full')
foreach ($s in $suites) {
    $out = "D:\dsh\xin\tools\final_$s.txt"
    pwsh -NoProfile -ExecutionPolicy Bypass -File "D:\dsh\xin\tools\$s.ps1" *> $out
    $lines = Get-Content $out | Select-String -Pattern 'FAIL|项检查'
    Write-Host ("{0}: {1}" -f $s, (($lines | ForEach-Object { $_.Line.Trim() }) -join ' ; '))
}
