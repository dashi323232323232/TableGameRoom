param([string]$Path = 'D:\dsh\xin\tools\shots\win-before.png', [int]$Rows = 60)
Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile($Path)
"size=$($bmp.Width)x$($bmp.Height)"
for ($y = 0; $y -lt $Rows; $y += 2) {
    $line = ''
    for ($x = 0; $x -lt $bmp.Width; $x += 8) {
        $any = $false
        for ($dy = 0; $dy -lt 2; $dy++) {
            for ($dx = 0; $dx -lt 8; $dx++) {
                $c = $bmp.GetPixel($x + $dx, $y + $dy)
                if ($c.R -gt 150 -and $c.G -gt 150 -and $c.B -gt 150) { $any = $true }
            }
        }
        $line += $(if ($any) { '#' } else { '.' })
    }
    "{0,3} {1}" -f $y, $line
}
$bmp.Dispose()
'DONE'
