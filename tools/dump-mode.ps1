Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 4
$p.Refresh()
$win = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

$types = @('Button','Text','Group','Pane','Custom','List','ListItem','Edit','Document','Image')
foreach ($t in $types) {
    $ct = [System.Windows.Automation.ControlType]::$t
    if (-not $ct) { continue }
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $ct)
    $els = @($win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c))
    if ($els.Count -gt 0) {
        "== $t ($($els.Count)) =="
        $els | Select-Object -First 14 | ForEach-Object {
            $r = $_.Current.BoundingRectangle
            "   '$($_.Current.Name)' @ $([int]$r.X),$([int]$r.Y) $([int]$r.Width)x$([int]$r.Height)"
        }
    }
}
'DONE'
