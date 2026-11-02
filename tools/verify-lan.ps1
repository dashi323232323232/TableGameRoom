# 局域网联机双实例测试
# 实例 A 创建局域网会话（主机），实例 B 通过端口号加入
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$Exe = 'D:\dsh\xin\publish\TableGameRoom.exe'
$results = New-Object System.Collections.Generic.List[string]
function Check($name, $ok, $detail = '') {
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    $results.Add("[$mark] $name  $detail")
    Write-Host "[$mark] $name  $detail"
}

# 启动两个实例
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
function ClickBtn($win, [string]$part, [int]$index = 0) {
    $f = @(ButtonsOf $win | Where-Object { $_.Current.Name -like ('*' + $part + '*') })
    if ($f.Count -le $index) { Write-Host "  MISS in window: $part"; return $false }
    try { $f[$index].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 800; return $true }
    catch { Write-Host "  INVOKE FAIL: $part"; return $false }
}
function HasText($win, [string]$pat) {
    [bool](@(TextsOf $win | Where-Object { $_.Current.Name -like $pat }).Count -gt 0)
}
function LineOf($win, [string]$pat) {
    (@(TextsOf $win | Where-Object { $_.Current.Name -like $pat } | Select-Object -First 1)).Current.Name
}

# ---------- A: 进入联机大厅 ----------
Check 'A 有联机入口' (HasText $winA '*联机大厅*')
ClickBtn $winA '联机大厅' | Out-Null
Start-Sleep -Milliseconds 700
Check 'A 进入联机大厅（有创建房间）' ((HasText $winA '*创建房间*') -and (HasText $winA '*加入房间*'))

# ---------- A: 创建局域网会话 ----------
ClickBtn $winA '创建房间' | Out-Null
Start-Sleep -Milliseconds 1500
$infoA = LineOf $winA '*端口*'
Check 'A 创建会话成功并显示端口' ($infoA -ne $null -and $infoA -match '端口：(\d+)') $infoA

$port = if ($infoA -match '端口：(\d+)') { $Matches[1] } else { '' }
Write-Host "  ==> 主机端口: $port"

# ---------- B: 加入 ----------
ClickBtn $winB '联机大厅' | Out-Null
Start-Sleep -Milliseconds 700

# B 需要先进入「加入房间」填端口
$joinBoxes = @()
$editCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Edit)
$edits = @($winB.FindAll([System.Windows.Automation.TreeScope]::Descendants, $editCond))
if ($edits.Count -gt 0) {
    $vp = $edits[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $vp.SetValue($port)
    Write-Host "  B 已填入端口 $port"
}
ClickBtn $winB '加入房间' | Out-Null
Start-Sleep -Seconds 2

$infoB = LineOf $winB '*端口*'
Check 'B 加入会话成功' ($infoB -ne $null) $infoB
Check 'B 看到在线人数 >= 2' ((LineOf $winB '*在线*') -match '在线：([2-9]|\d{2,})') (LineOf $winB '*在线*')

# ---------- A: 创建逻辑房间 ----------
ClickBtn $winA '创建房间' | Out-Null
Start-Sleep -Milliseconds 1200
$roomLineA = LineOf $winA '*· 1/10 人*'
Check 'A 创建房间成功' ($roomLineA -ne $null) $roomLineA

# ---------- B: 看到房间并加入 ----------
Start-Sleep -Milliseconds 800
$roomLineB = LineOf $winB '*· 1/10 人*'
Check 'B 能看到 A 的房间' ($roomLineB -ne $null) $roomLineB

# 精确点击房间卡片里的「加入」（不是「加入房间」）
$joinBtns = @(ButtonsOf $winB | Where-Object { $_.Current.Name -eq '加入' })
if ($joinBtns.Count -gt 0) {
    $joinBtns[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 1400
    Check 'B 点击加入房间' $true
} else {
    Check 'B 点击加入房间' $false "未找到精确的「加入」按钮"
}
Check 'B 显示已在此房间' (HasText $winB '*已在此房间*')

# ---------- A: 看到 B 进房 ----------
Start-Sleep -Milliseconds 800
$roomInfoA = LineOf $winA '*· 2/10 人*'
Check 'A 房间人数变成 2' ($roomInfoA -ne $null) $roomInfoA

# ---------- 进入房间页 ----------
ClickBtn $winA '进入房间' | Out-Null
Start-Sleep -Milliseconds 900
Check 'A 进入房间页' (HasText $winA '*玩家列表*')
Check 'A 房间页有 10 个位置' ((@(TextsOf $winA | Where-Object { $_.Current.Name -match '^\d+\.\s' }).Count) -eq 10)
Check 'A 房间页有游戏设置' (HasText $winA '*游戏设置*')
Check 'A 房间页有取消房间' (HasText $winA '*取消房间*')

# ---------- 关闭 ----------
Get-Process TableGameRoom -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host ''
Write-Host '========== 结果 =========='
$results | ForEach-Object { Write-Host $_ }
$pass = @($results | Where-Object { $_ -match '^\[PASS\]' }).Count
$fail = @($results | Where-Object { $_ -match '^\[FAIL\]' }).Count
Write-Host "通过 $pass 项，失败 $fail 项"
