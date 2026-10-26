Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile('D:\dsh\xin\tools\shots\win-before.png')
"size=$($bmp.Width)x$($bmp.Height)"
# ASCII 缩略图：每 4x2 像素一个字符
for ($y = 0; $y -lt 60; $y += 2) {
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
# 右侧 200px 内亮像素范围
$minX = 9999; $maxX = -1; $minY = 9999; $maxY = -1
for ($y = 10; $y -lt 60; $y++) {
    for ($x = $bmp.Width - 200; $x -lt $bmp.Width - 9; $x++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.R -gt 150 -and $c.G -gt 150 -and $c.B -gt 150) {
            if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
"right bright bbox=($minX,$minY)-($maxX,$maxY)"
$bmp.Dispose()
'DONE'
