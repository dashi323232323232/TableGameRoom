# 联机「勾选真人 + 选总人数 + AI 自动补位」端到端测试
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$Exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
$results = New-Object System.Collections.Generic.List[string]
function Check($name, $ok, $detail = '') {
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    $results.Add("[$mark] $name  $detail")
    Write-Host "[$mark] $name  $detail"
}

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

$procA = Start-Process $Exe -ArgumentList '--multi' -PassThru
Start-Sleep -Seconds 4
$procA.Refresh()
$winA = [System.Windows.Automation.AutomationElement]::FromHandle($procA.MainWindowHandle)

$procB = Start-Process $Exe -ArgumentList '--multi' -PassThru
Start-Sleep -Seconds 4
$procB.Refresh()
$winB = [System.Windows.Automation.AutomationElement]::FromHandle($procB.MainWindowHandle)

$procC = Start-Process $Exe -ArgumentList '--multi' -PassThru
Start-Sleep -Seconds 4
$procC.Refresh()
$winC = [System.Windows.Automation.AutomationElement]::FromHandle($procC.MainWindowHandle)

function ButtonsOf($win) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    @($win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c))
}
function TextsOf($win) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Text)
    @($win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c))
}
function ChecksOf($win) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::CheckBox)
    @($win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c))
}
function ClickBtn($win, [string]$part, [int]$index = 0) {
    $f = @(ButtonsOf $win | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($f.Count -le $index) { Write-Host "  MISS: $part"; return $false }
    try { $f[$index].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 800; return $true }
    catch { Write-Host "  INVOKE FAIL: $part"; return $false }
}
function ClickExact($win, [string]$name) {
    $f = @(ButtonsOf $win | Where-Object { $_.Current.Name -eq $name })
    if ($f.Count -eq 0) { Write-Host "  MISS exact: $name"; return $false }
    try { $f[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 800; return $true }
    catch { Write-Host "  INVOKE FAIL: $name"; return $false }
}
function HasText($win, [string]$pat) {
    [bool](@(TextsOf $win | Where-Object { $_.Current.Name -like $pat }).Count -gt 0)
}
function LineOf($win, [string]$pat) {
    (@(TextsOf $win | Where-Object { $_.Current.Name -like $pat } | Select-Object -First 1)).Current.Name
}

# ---------- A 创建会话，B/C 加入 ----------
ClickBtn $winA '联机大厅' | Out-Null
Start-Sleep -Milliseconds 700
ClickBtn $winA '创建房间' | Out-Null
Start-Sleep -Milliseconds 1500
$infoA = LineOf $winA '*端口*'
$port = if ($infoA -match '端口：(\d+)') { $Matches[1] } else { '' }
Write-Host "  ==> 端口 $port"

foreach ($w in @($winB, $winC)) {
    ClickBtn $w '联机大厅' | Out-Null
    Start-Sleep -Milliseconds 700
    $editCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)
    $edits = @($w.FindAll([System.Windows.Automation.TreeScope]::Descendants, $editCond))
    if ($edits.Count -gt 0) {
        $edits[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($port)
    }
    ClickBtn $w '加入房间' | Out-Null
    Start-Sleep -Seconds 2
}
Start-Sleep -Milliseconds 1500
Check 'B/C 都加入会话' ((LineOf $winA '*在线*') -match '在线：(3|\d{2,})') (LineOf $winA '*在线*')
# ---------- A 建房，B/C 加入 ----------
ClickBtn $winA '创建房间' | Out-Null
Start-Sleep -Milliseconds 1200
Start-Sleep -Milliseconds 800
foreach ($w in @($winB, $winC)) {
    $jb = @(ButtonsOf $w | Where-Object { $_.Current.Name -eq '加入' })
    if ($jb.Count -gt 0) { $jb[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 1200 }
}
Check 'A 房间 3 人' ($null -ne (LineOf $winA '*· 3/10 人*')) (LineOf $winA '*· 3/10 人*')

# ---------- A 进房间页 ----------
ClickBtn $winA '进入房间' | Out-Null
Start-Sleep -Milliseconds 900
Check '房间页有参战勾选框' ((@(ChecksOf $winA)).Count -ge 3) "勾选框 $(@(ChecksOf $winA).Count) 个"
Check '房间页显示参战真人数' (HasText $winA '*已勾选参战真人*') (LineOf $winA '*已勾选参战真人*')

# 默认 3 人全是真人参战 → 应显示「全部真人」
Check '默认全部真人参战' (HasText $winA '*（全部真人）*') (LineOf $winA '*本局人数*')

# ---------- 选 5 人 → AI 补 2 ----------
ClickExact $winA '5（+2AI）' | Out-Null
Start-Sleep -Milliseconds 1000
$seatLine = LineOf $winA '*本局人数*'
Check '选 5 人后显示 AI 补位 2 名' ($seatLine -match '5 人.*AI 补位 2 名') $seatLine

# ---------- 取消一个真人参战 → 2 真人 + 3 AI ----------
$checks = @(ChecksOf $winA)
$toggled = $false
foreach ($c in $checks) {
    $tp = $c.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if ($tp.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) {
        $tp.Toggle(); Start-Sleep -Milliseconds 1200; $toggled = $true; break
    }
}
Check '可以取消某个真人参战' $toggled
$seatLine2 = LineOf $winA '*本局人数*'
Check '取消 1 人后变成 2 真人 + 3 AI' ($seatLine2 -match '5 人.*AI 补位 3 名') $seatLine2
$countLine = LineOf $winA '*已勾选参战真人*'
Check '参战真人数变成 2' ($countLine -match '已勾选参战真人：2 人') $countLine

# ---------- 换游戏：骗子扑克上限 2 ----------
ClickExact $winA '骗子扑克' | Out-Null
Start-Sleep -Milliseconds 1200
$seatLine3 = LineOf $winA '*本局人数*'
Check '切到骗子扑克后桌位收到 <=2' ($seatLine3 -match '本局人数：[12] 人') $seatLine3

# ---------- 切回 21 点 ----------
ClickExact $winA '21 点' | Out-Null
Start-Sleep -Milliseconds 1200
Check '切回 21 点正常' (HasText $winA '*游戏设置*')

Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host ''
Write-Host '========== 结果 =========='
$pass = @($results | Where-Object { $_ -match '^\[PASS\]' }).Count
$fail = @($results | Where-Object { $_ -match '^\[FAIL\]' }).Count
Write-Host "通过 $pass 项，失败 $fail 项"
