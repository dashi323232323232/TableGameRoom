param([string]$Path, [string]$Tag = '')
Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile($Path)
$W = $bmp.Width; $H = $bmp.Height
# 找标题栏深色区域起始行（中心列，颜色接近 #202A30）
$titleTop = -1
for ($y = 0; $y -lt [Math]::Min(60, $H); $y++) {
    $c = $bmp.GetPixel([int]($W / 2), $y)
    if ($c.R -lt 60 -and $c.G -lt 70 -and $c.B -lt 80) { $titleTop = $y; break }
}
"[$Tag] image=${W}x${H} titleTop=$titleTop"
if ($titleTop -lt 0) { $bmp.Dispose(); return }
# 按钮区：标题栏右侧最后 120px，垂直范围标题栏内
$x0 = $W - 120; $x1 = $W - 2
$y0 = $titleTop; $y1 = [Math]::Min($titleTop + 44, $H - 1)
$visited = New-Object 'bool[,]' ($x1 - $x0 + 1), ($y1 - $y0 + 1)
$comps = @()
for ($x = $x0; $x -le $x1; $x++) {
    for ($y = $y0; $y -le $y1; $y++) {
        if ($visited[$x - $x0, $y - $y0]) { continue }
        $c = $bmp.GetPixel($x, $y)
        if ($c.R -ge 190 -and $c.G -ge 190 -and $c.B -ge 190) {
            # BFS
            $q = New-Object System.Collections.Queue
            $q.Enqueue(@($x, $y)); $visited[$x - $x0, $y - $y0] = $true
            $minX = $x; $maxX = $x; $minY = $y; $maxY = $y; $n = 0; $sx = 0; $sy = 0
            while ($q.Count -gt 0) {
                $pt = $q.Dequeue(); $px = $pt[0]; $py = $pt[1]; $n++; $sx += $px; $sy += $py
                if ($px -lt $minX) { $minX = $px }; if ($px -gt $maxX) { $maxX = $px }
                if ($py -lt $minY) { $minY = $py }; if ($py -gt $maxY) { $maxY = $py }
                foreach ($d in @(@(1,0),@(-1,0),@(0,1),@(0,-1))) {
                    $nx = $px + $d[0]; $ny = $py + $d[1]
                    if ($nx -lt $x0 -or $nx -gt $x1 -or $ny -lt $y0 -or $ny -gt $y1) { continue }
                    if ($visited[$nx - $x0, $ny - $y0]) { continue }
                    $cc = $bmp.GetPixel($nx, $ny)
                    if ($cc.R -ge 190 -and $cc.G -ge 190 -and $cc.B -ge 190) {
                        $visited[$nx - $x0, $ny - $y0] = $true; $q.Enqueue(@($nx, $ny))
                    }
                }
            }
            if ($n -ge 3) {
                $comps += [pscustomobject]@{ n=$n; x=$minX; y=$minY; w=($maxX-$minX+1); h=($maxY-$minY+1); cx=[Math]::Round($sx/$n,1); cy=[Math]::Round($sy/$n,1) }
            }
        }
    }
}
$comps | Sort-Object x | ForEach-Object { "  glyph n={0} bbox=({1},{2}) {3}x{4} center=({5},{6})" -f $_.n, $_.x, $_.y, $_.w, $_.h, $_.cx, $_.cy }
"  glyphCount=$($comps.Count)"
$bmp.Dispose()
