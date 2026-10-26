Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WQ {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  public delegate bool EnumProc(IntPtr h, IntPtr p);
}
"@
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400
$p = Start-Process 'D:\dsh\xin\bin\Release\net8.0-windows\TableGameRoom.exe' -PassThru
Start-Sleep -Seconds 4
$p.Refresh()
$script:targetPid = [uint32]$p.Id
$script:found = [IntPtr]::Zero
"pid=$($p.Id) MainWindowHandle=$($p.MainWindowHandle)"
$cb = [WQ+EnumProc]{
  param($h, $l)
  $wp = [uint32]0
  [WQ]::GetWindowThreadProcessId($h, [ref]$wp) | Out-Null
  if ($wp -eq $script:targetPid -and [WQ]::IsWindowVisible($h)) {
    $cn = New-Object System.Text.StringBuilder 256; [WQ]::GetClassName($h, $cn, 256) | Out-Null
    $tt = New-Object System.Text.StringBuilder 256; [WQ]::GetWindowText($h, $tt, 256) | Out-Null
    Write-Host "  hwnd=$h class=$($cn.ToString()) title=$($tt.ToString())"
    if ($cn.ToString() -like 'HwndWrapper*') { $script:found = $h }
  }
  return $true
}
[WQ]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
"enum found=$($script:found)"
foreach ($cand in @($p.MainWindowHandle, $script:found)) {
  if ($cand -eq [IntPtr]::Zero) { continue }
  for ($i = 0; $i -lt 3; $i++) {
    try {
      $el = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$cand)
      "FromHandle($cand) OK name='$($el.Current.Name)' class='$($el.Current.ClassName)'"
      break
    } catch {
      "FromHandle($cand) try$i FAIL: $($_.Exception.Message)"
      Start-Sleep -Milliseconds 500
    }
  }
}
try {
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
  $el2 = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
  "Root.FindFirst OK name='$($el2.Current.Name)'"
} catch { "Root.FindFirst FAIL: $($_.Exception.Message)" }
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
'DONE'
