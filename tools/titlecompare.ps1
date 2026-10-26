Add-Type -AssemblyName System.Drawing
$dir = 'D:\dsh\xin\tools\shots'
function Analyze([string]$path, [string]$label) {
    if (-not (Test-Path $path)) { "$label : MISSING $path"; return }
    $bmp = [System.Drawing.Bitmap]::FromFile($path)
    $w = $bmp.Width; $ht = $bmp.Height
    "== $label ($w x $ht) =="
    # 顶部近白行数（沿中线）
    $cx = [int]($w / 2); $white = 0
    for ($y = 0; $y -lt 14; $y++) {
        $c = $bmp.GetPixel($cx, $y)
        if ($c.R -gt 225 -and $c.G -gt 225 -and $c.B -gt 225) { $white++ } else { break }
    }
    "  topNearWhiteRows=$white"
    # 顶部各行颜色
    $rows = @()
    for ($y = 0; $y -lt 12; $y++) { $c = $bmp.GetPixel($cx, $y); $rows += ("#{0:X2}{1:X2}{2:X2}" -f $c.R, $c.G, $c.B) }
    "  topColors=" + ($rows -join ' ')

    # 右上区域亮像素（字形）连通列分组
    $x0 = [Math]::Max(0, $w - 170); $x1 = $w - 9; $y0 = 9; $y1 = 40
    $colHit = @{}
    for ($y = $y0; $y -le $y1; $y++) {
        for ($x = $x0; $x -le $x1; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.R -gt 130 -and $c.G -gt 130 -and $c.B -gt 130) { $colHit[$x] = $true }
        }
    }
    $groups = @(); $cur = $null
    for ($x = $x0; $x -le $x1; $x++) {
        if ($colHit.ContainsKey($x)) {
            if ($null -eq $cur) { $cur = @{ MinX = $x; MaxX = $x } }
            else { $cur.MaxX = $x }
        } elseif ($null -ne $cur) {
            if ($cur.MaxX - $cur.MinX -ge 2) { $groups += , $cur }
            $cur = $null
        }
    }
    if ($null -ne $cur -and $cur.MaxX - $cur.MinX -ge 2) { $groups += , $cur }
    $gi = 0
    foreach ($grp in $groups) {
        $gi++
        $minX = $grp.MinX; $maxX = $grp.MaxX
        $minY = 9999; $maxY = -1; $mass = 0; $cnt = 0
        for ($y = $y0; $y -le $y1; $y++) {
            for ($x = $minX; $x -le $maxX; $x++) {
                $c = $bmp.GetPixel($x, $y)
                if ($c.R -gt 130 -and $c.G -gt 130 -and $c.B -gt 130) {
                    $cnt++; $mass += ($c.R + $c.G + $c.B) / 3.0
                    if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
                }
            }
        }
        $gw = $maxX - $minX + 1; $gh = $maxY - $minY + 1
        $gcx = $minX + ($gw - 1) / 2.0; $gcy = $minY + ($gh - 1) / 2.0
        "  glyph{0}: box=({1},{2}) {3}x{4} pixels={5} avgBrightness={6:N0} center=({7:N1},{8:N1}) rightGap={9}" -f `
            $gi, $minX, $minY, $gw, $gh, $cnt, ($mass / $cnt), $gcx, $gcy, ($w - 1 - $maxX)
    }
    $bmp.Dispose()
}
Analyze (Join-Path $dir 'win-before.png') 'BEFORE'
Analyze (Join-Path $dir 'win-after.png') 'AFTER'
'DONE'
