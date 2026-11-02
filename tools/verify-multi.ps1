# 验证：多开验证码流程（完整两阶段）
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$Exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
$results = New-Object System.Collections.Generic.List[string]
function Check($name, $ok, $detail = '') {
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    $results.Add("[$mark] $name  $detail")
    Write-Host "[$mark] $name  $detail"
}

# ---- 用 Win32 枚举该进程的顶层窗口，按标题找弹窗 ----
Add-Type @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class WEnum {
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    public static IntPtr Find(uint target, string title) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) => {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid == target && IsWindowVisible(h)) {
                var sb = new StringBuilder(256); GetWindowText(h, sb, 256);
                if (sb.ToString() == title) { found = h; return false; }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
'@

# 验证码从被 gitignore 的本地文件读取，绝不写进仓库
$codeFile = Join-Path $PSScriptRoot '.testcode'
if (-not (Test-Path $codeFile)) { Write-Host '缺少 tools/.testcode（本地测试密钥），跳过'; exit 2 }
$Code = (Get-Content $codeFile -Raw).Trim()

$flag = Join-Path $env:LOCALAPPDATA 'TableGameRoom\.unlock'
if (Test-Path $flag) { Remove-Item $flag -Force }
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 700

$proc = Start-Process $Exe -PassThru
Start-Sleep -Seconds 4
$proc.Refresh()
$win = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)

function BO($w) { $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button); @($w.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c)) }
function TO($w) { $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Text); @($w.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c)) }
function EB($w) { $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Edit); @($w.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c)) }

function FindDlg($p) {
    for ($i = 0; $i -lt 40; $i++) {
        $h = [WEnum]::Find([uint32]$p.Id, '多开')
        if ($h -ne [IntPtr]::Zero) { return [System.Windows.Automation.AutomationElement]::FromHandle($h) }
        Start-Sleep -Milliseconds 200
    }
    return $null
}
function ClickNamed($w, $name) {
    $b = @(BO $w | Where-Object { $_.Current.Name -eq $name })
    if ($b.Count -eq 0) { Write-Host "  MISS: $name"; return $false }
    $b[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 1300
    return $true
}
function Texts($w) { @(TO $w | ForEach-Object { $_.Current.Name }) -join ' / ' }

# ---------- 第一次：未解锁 ----------
ClickNamed $win '多开' | Out-Null
$dlg = FindDlg $proc
Check '首次点多开弹出验证码窗口' ($null -ne $dlg)

if ($dlg) {
    $t = Texts $dlg
    Write-Host "  弹窗文本: $t"
    Check '弹窗标题为解锁多开权限' ($t -like '*解锁多开权限*')
    $boxes = @(EB $dlg)
    Check '有验证码输入框' ($boxes.Count -ge 1) "$($boxes.Count) 个"

    # 错误验证码
    $boxes[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('WrongCode123')
    ClickNamed $dlg '确定' | Out-Null
    $t2 = Texts $dlg
    Check '错误验证码提示' ($t2 -like '*验证码错误，请输入正确验证码*') $t2
    Check '错误后仍在验证码阶段' ((@(EB $dlg)).Count -ge 1)
    Check '错误后未生成解锁标记' (-not (Test-Path $flag))

    # 大小写敏感
    $boxes = @(EB $dlg)
    $boxes[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Code.ToLower())
    ClickNamed $dlg '确定' | Out-Null
    Check '小写验证码被拒绝' (-not (Test-Path $flag))

    # 正确验证码
    $boxes = @(EB $dlg)
    $boxes[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Code)
    ClickNamed $dlg '确定' | Out-Null
    Start-Sleep -Milliseconds 1800

    $t3 = Texts $dlg
    Write-Host "  验证通过后: $t3"
    Check '正确验证码后切到新建实例确认' ($t3 -like '*是否新建一个实例*') $t3
    Check '验证码输入框已消失' ((@(EB $dlg)).Count -eq 0) "剩 $(@(EB $dlg).Count) 个"
    Check '已生成解锁标记' (Test-Path $flag)

    # 取消：不应新建
    $before = @(Get-Process TableGameRoom).Count
    ClickNamed $dlg '取消' | Out-Null
    Start-Sleep -Milliseconds 1800
    $after = @(Get-Process TableGameRoom).Count
    Check '点取消不新建实例' ($after -eq $before) "之前 $before → 之后 $after"

    # ---------- 第二次：已解锁 ----------
    ClickNamed $win '多开' | Out-Null
    $dlg2 = FindDlg $proc
    Check '已解锁后点多开直接弹确认' ($null -ne $dlg2)
    if ($dlg2) {
        $t4 = Texts $dlg2
        Check '已解锁后不再要求验证码' ($t4 -like '*是否新建一个实例*') $t4
        Check '已解锁后无验证码框' ((@(EB $dlg2)).Count -eq 0)

        $before2 = @(Get-Process TableGameRoom).Count
        ClickNamed $dlg2 '确认' | Out-Null
        Start-Sleep -Seconds 5
        $after2 = @(Get-Process TableGameRoom).Count
        Check '点确认新建了实例' ($after2 -eq $before2 + 1) "之前 $before2 → 之后 $after2"
    }
}

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host ''
Write-Host '========== 结果 =========='
$pass = @($results | Where-Object { $_ -match '^\[PASS\]' }).Count
$fail = @($results | Where-Object { $_ -match '^\[FAIL\]' }).Count
Write-Host "通过 $pass 项，失败 $fail 项"
