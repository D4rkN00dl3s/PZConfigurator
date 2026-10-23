# Screenshot smoke: modify a value, badge + filter + search counter, via UIA patterns
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class S {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    public struct RECT { public int L, T, R, B; }
}
"@
[S]::SetProcessDPIAware() | Out-Null
taskkill /IM PZConfigurator.exe /F 2>$null | Out-Null

Start-Process -FilePath "PZConfigurator\bin\Debug\net10.0\PZConfigurator.exe"
Start-Sleep 6
$proc = Get-Process PZConfigurator | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$h = $proc.MainWindowHandle
$root = [System.Windows.Automation.AutomationElement]::FromHandle($h)

Add-Type -AssemblyName System.Drawing
function Snap($name) {
    $r = New-Object 'S+RECT'
    [S]::GetWindowRect($h, [ref]$r) | Out-Null
    $bmp = New-Object System.Drawing.Bitmap(([int]($r.R - $r.L)), ([int]($r.B - $r.T)))
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $dc = $g.GetHdc()
    [S]::PrintWindow($h, $dc, 2) | Out-Null
    $g.ReleaseHdc($dc)
    $bmp.Save("tools\$name.png", [System.Drawing.Imaging.ImageFormat]::Png)
}

function ByName($n) {
    $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, $n)))
}

# 1) toggle the first checkbox in the detail pane
$boxes = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::CheckBox)))
$boxes[0].GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
Start-Sleep -Milliseconds 600
Snap "badge-smoke"

# 2) turn on the "Only modified" filter
$btns = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "Only modified")))
$om = $btns[0]
if ($om) { $om.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
Start-Sleep -Milliseconds 800
Snap "modified-smoke"

# 3) type a search term via the TextBox ValuePattern
$search = ByName ""
$edit = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)))
$edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("Port")
Start-Sleep -Milliseconds 800
Snap "modified-search-smoke"

# 4) clear via the × button
$xcands = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "Clear search")))
if ($xcands.Count -gt 0) { $xcands[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
Start-Sleep -Milliseconds 500

taskkill /IM PZConfigurator.exe /F 2>$null | Out-Null

# close via UIA so OnClosing runs and settings are persisted
Start-Process -FilePath "PZConfigurator\bin\Debug\net10.0\PZConfigurator.exe"
Start-Sleep 6
$proc2 = Get-Process PZConfigurator | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$root2 = [System.Windows.Automation.AutomationElement]::FromHandle($proc2.MainWindowHandle)
$root2.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
Start-Sleep 3
$settings = "$env:USERPROFILE\PZConfigurator\settings.json"
if (Test-Path $settings) { Write-Output "SETTINGS:"; Get-Content $settings } else { Write-Output "SETTINGS: MISSING" }
taskkill /IM PZConfigurator.exe /F 2>$null | Out-Null
Write-Output "done"
