$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$out = 'D:\dsh\xin\tools\shots\calverify'
$log = Join-Path $out 'pix.txt'
"== PIX $(Get-Date -Format o)" | Out-File -Encoding utf8 $log

# 1. window corner rounding check on u3_08_final.png (window at 20,20 size 1200x780)
$b = [System.Drawing.Bitmap]::FromFile((Join-Path $out 'u3_08_final.png'))
"u3_08 size $($b.Width)x$($b.Height)" | Out-File -Append -Encoding utf8 $log
foreach ($pt in @(@(0,0),@(1,1),@(3,3),@(6,6),@(12,12),@(30,30),@(599,5),@(1194,5),@(5,774))) {
  $c = $b.GetPixel($pt[0], $pt[1]); "corner($($pt[0]),$($pt[1]))=#$($c.R.ToString('X2'))$($c.G.ToString('X2'))$($c.B.ToString('X2'))" | Out-File -Append -Encoding utf8 $log
}
# right rail columns: rightmost non-background pixel per row band 100..470
$bg = $b.GetPixel(599, 700)
"bgProbe(599,700)=#$($bg.R.ToString('X2'))$($bg.G.ToString('X2'))$($bg.B.ToString('X2'))" | Out-File -Append -Encoding utf8 $log
$maxX = 0
for ($y = 80; $y -lt 500; $y += 2) {
  for ($x = $b.Width - 1; $x -gt $b.Width - 60; $x--) {
    $c = $b.GetPixel($x, $y)
    if ([Math]::Abs($c.R - $bg.R) + [Math]::Abs($c.G - $bg.G) + [Math]::Abs($c.B - $bg.B) -gt 18) { if ($x -gt $maxX) { $maxX = $x }; break }
  }
}
"rightmost content pixel in rows 80-500: x=$maxX (window width $($b.Width)); overflow = $($maxX -ge ($b.Width - 1))" | Out-File -Append -Encoding utf8 $log
$b.Dispose()

# 2. hover tooltip crop from full screen
$f = [System.Drawing.Bitmap]::FromFile((Join-Path $out 'u3_07_hover20.png'))
$rect = New-Object System.Drawing.Rectangle 860, 300, 400, 260
$crop = $f.Clone($rect, $f.PixelFormat)
$crop.Save((Join-Path $out 'u3_07_hover20_crop.png'), [System.Drawing.Imaging.ImageFormat]::Png)
# enlarge 2x for legibility
$big = New-Object System.Drawing.Bitmap ($crop.Width*2), ($crop.Height*2)
$g = [System.Drawing.Graphics]::FromImage($big)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.DrawImage($crop, 0, 0, $big.Width, $big.Height); $g.Dispose()
$big.Save((Join-Path $out 'u3_07_hover20_crop2x.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$big.Dispose(); $crop.Dispose(); $f.Dispose()
"cropped tooltip region 860,300,400,260" | Out-File -Append -Encoding utf8 $log
Write-Output done
