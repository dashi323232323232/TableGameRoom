Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 64, 64
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

$table = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 226, 244, 236))
$g.FillEllipse($table, 1, 1, 62, 62)
$ring = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 59, 125, 114)), 4
$g.DrawEllipse($ring, 3, 3, 58, 58)
$dash = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 91, 159, 208)), 3
$g.DrawArc($dash, 10, 10, 44, 44, 0, 360)

$font = New-Object System.Drawing.Font 'Segoe UI', 26, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = [System.Drawing.StringAlignment]::Center
$fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
$ink = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 32, 42, 48))
$rect = New-Object System.Drawing.RectangleF 0, 0, 64, 64
$g.DrawString('T', $font, $ink, $rect, $fmt)
$g.Dispose()

$h = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($h)
$fs = [System.IO.File]::Create('D:\dsh\xin\app.ico')
$icon.Save($fs)
$fs.Close()
$fs.Dispose()
$icon.Dispose()
$bmp.Dispose()

$info = Get-Item 'D:\dsh\xin\app.ico'
"app.ico 生成: $($info.Length) 字节"
