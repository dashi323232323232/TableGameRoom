# 共享 UIA 测试助手：dot-source 后调用 StartApp / ClickBtn / Expect 等
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class WU {
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, int e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr h, EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  public delegate bool EnumProc(IntPtr h, IntPtr p);
  public const uint LD = 0x0002, LU = 0x0004;
  public static void Click() { mouse_event(LD,0,0,0,0); System.Threading.Thread.Sleep(70); mouse_event(LU,0,0,0,0); }
  public static IntPtr Dialog(uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, p) => {
      uint wp; GetWindowThreadProcessId(h, out wp);
      if (wp == pid && IsWindowVisible(h)) {
        var cn = new StringBuilder(256); GetClassName(h, cn, 256);
        if (cn.ToString() == "#32770") { found = h; return false; }
      }
      return true;
    }, IntPtr.Zero);
    return found;
  }
  public static List<string> ChildTexts(IntPtr dlg) {
    var res = new List<string>();
    EnumChildWindows(dlg, (h, p) => {
      var cn = new StringBuilder(256); GetClassName(h, cn, 256);
      var tt = new StringBuilder(1024); GetWindowText(h, tt, 1024);
      if (tt.Length > 0 && cn.ToString() != "Button") res.Add(tt.ToString());
      return true;
    }, IntPtr.Zero);
    return res;
  }
}
"@

$script:Exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
$script:Proc = $null
$script:Win = $null
$script:Log = New-Object System.Collections.Generic.List[string]

function StartApp {
    Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
    $script:Proc = Start-Process $script:Exe -PassThru
    Start-Sleep -Seconds 4
    $script:Proc.Refresh()
    $script:Win = [System.Windows.Automation.AutomationElement]::FromHandle($script:Proc.MainWindowHandle)
    Activate
    Start-Sleep -Milliseconds 400
}
function StopApp {
    if ($script:Proc) { Stop-Process -Id $script:Proc.Id -Force -ErrorAction SilentlyContinue }
}
function All($type) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type)
    $script:Win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c)
}
function Buttons { @(All ([System.Windows.Automation.ControlType]::Button)) }
function Texts { @(All ([System.Windows.Automation.ControlType]::Text)) }
function Line([string]$pat) { (Texts | Where-Object { $_.Current.Name -like $pat } | Select-Object -First 1).Current.Name }
function Has([string]$pat) { [bool](Texts | Where-Object { $_.Current.Name -like $pat } | Select-Object -First 1) }
function Activate {
    if ($script:Proc) {
        $script:Proc.Refresh()
        [WU]::SetForegroundWindow($script:Proc.MainWindowHandle) | Out-Null
    }
    Start-Sleep -Milliseconds 200
}
function CloseModal {
    $d = [WU]::Dialog([uint32]$script:Proc.Id)
    if ($d -eq [IntPtr]::Zero) { return '' }
    $t = ([WU]::ChildTexts($d) -join ' / ')
    [WU]::SetForegroundWindow($d) | Out-Null
    Start-Sleep -Milliseconds 250
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
    Start-Sleep -Milliseconds 600
    return $t
}
function ScrollHost($el) {
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $node = $el
    while ($true) {
        $node = $walker.GetParent($node)
        if (-not $node) { return $null }
        if ($node.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$null)) { return $node }
    }
}
# 把元素滚动到滚动视口内（WPF 对被子元素裁剪的控件仍报 IsOffscreen=False，必须比矩形）
function EnsureVisible($el) {
    $sv = ScrollHost $el
    if (-not $sv) { return $false }
    $sp = $sv.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    for ($i = 0; $i -lt 25; $i++) {
        $r = $el.Current.BoundingRectangle
        $v = $sv.Current.BoundingRectangle
        if ($r.Height -le 0) { return $false }
        $inside = ($r.Top -ge ($v.Top + 2)) -and ($r.Bottom -le ($v.Bottom - 2))
        if ($inside) { return $true }
        try {
            if ($r.Bottom -gt $v.Bottom) {
                $sp.Scroll([System.Windows.Automation.ScrollAmount]::NoAmount, [System.Windows.Automation.ScrollAmount]::LargeIncrement)
            } else {
                $sp.Scroll([System.Windows.Automation.ScrollAmount]::NoAmount, [System.Windows.Automation.ScrollAmount]::LargeDecrement)
            }
        } catch { return $false }
        Start-Sleep -Milliseconds 220
    }
    return $false
}
function ScrollTo($el) { return (EnsureVisible $el) }
function ClickEl($el) {
    Activate
    EnsureVisible $el | Out-Null
    $r = $el.Current.BoundingRectangle
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
    Start-Sleep -Milliseconds 180
    [WU]::Click()
    Start-Sleep -Milliseconds 800
    return (CloseModal)
}
function ClickBtn([string]$part, [int]$index = 0) {
    $f = @(Buttons | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($f.Count -le $index) { return 'MISS:' + $part }
    return (ClickEl $f[$index])
}
# 点击直到页面上出现 $expect 文本（最多 3 次），返回 $true/$false
function ClickUntil([string]$part, [string]$expect) {
    for ($i = 0; $i -lt 3; $i++) {
        $r = ClickBtn $part
        if (Has $expect) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}
function EnterCard([string]$title) {
    Activate
    # 卡片标题形如 "02  21 点"；筛选 chip 里也有 "21 点"，必须只匹配带序号前缀的卡片标题
    $t = Texts | Where-Object { $_.Current.Name -like ('0*' + $title + '*') } | Select-Object -First 1
    if (-not $t) { return $false }
    $r = $t.Current.BoundingRectangle
    $btn = Buttons | Where-Object { $_.Current.Name -like '*进入筛选*' -and $_.Current.BoundingRectangle.Top -ge $r.Top } |
        Sort-Object { [math]::Abs($_.Current.BoundingRectangle.Left - $r.Left) } | Select-Object -First 1
    if (-not $btn) { return $false }
    ClickEl $btn | Out-Null
    return (Has '*筛选*')
}
function SetStake([string]$v) {
    $e = All ([System.Windows.Automation.ControlType]::Edit) | Select-Object -First 1
    if ($e) {
        $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($v)
        Start-Sleep -Milliseconds 250
    }
}
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    $script:Log.Add("[$mark] $name $detail")
    "$mark  $name $detail"
}
function Report {
    $fails = @($script:Log | Where-Object { $_ -like '`[FAIL`]*' }).Count
    "---- $($script:Log.Count) 项检查，失败 $fails ----"
}
