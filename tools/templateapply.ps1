# Interactive smoke: pick a template in the combo, apply it, check dirty-flag + status
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

# switch to the Sandbox tab via UIA SelectionItemPattern (no mouse clicks — DPI-safe)
$tabs = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::TabItem)))
$sandbox = $tabs | Where-Object { $_.Current.Name -eq "Sandbox" } | Select-Object -First 1
if (-not $sandbox) {
    Write-Output "FAIL: Sandbox tab not found"
    taskkill /IM PZConfigurator.exe /F 2>$null | Out-Null; exit 1
}
$sandbox.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
Start-Sleep -Milliseconds 1000

# open the template combo and pick the first entry
# find the template combo by its stable AutomationId
$combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        "TemplateCombo")))
if (-not $combo) {
    Write-Output "FAIL: TemplateCombo not found"
    taskkill /IM PZConfigurator.exe /F 2>$null | Out-Null; exit 1
}

# expand the combo via UIA pattern
$expand = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
$expand.Expand()
Start-Sleep -Milliseconds 500

$items = $combo.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)))
Write-Output "combo items: $($items.Count): $(($items | ForEach-Object { $_.Current.Name }) -join ', ')"
if ($items.Count -eq 0) { Write-Output "FAIL: no combo items found"; taskkill /IM PZConfigurator.exe /F 2>$null | Out-Null; exit 1 }
($items | Select-Object -First 1).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
Start-Sleep -Milliseconds 800

# click Apply via InvokePattern (no mouse clicks)
$apply = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "Apply")))
if (-not $apply) {
    Write-Output "FAIL: Apply button not found"
    taskkill /IM PZConfigurator.exe /F 2>$null | Out-Null; exit 1
}
$apply.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 800

# read the status line
$texts = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Text)))
$status = $texts | Where-Object { $_.Current.Name -and $_.Current.Name -match "Applied|Loaded|template|Select" }

# screenshot
$r = New-Object 'S+RECT'
[S]::GetWindowRect($h, [ref]$r) | Out-Null
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap(([int]($r.R - $r.L)), ([int]($r.B - $r.T)))
$g = [System.Drawing.Graphics]::FromImage($bmp)
$dc = $g.GetHdc()
[S]::PrintWindow($h, $dc, 2) | Out-Null
$g.ReleaseHdc($dc)
$bmp.Save("tools\template-apply-smoke.png", [System.Drawing.Imaging.ImageFormat]::Png)
$status | ForEach-Object { Write-Output "STATUS-LIKE: $($_.Current.Name)" }
taskkill /IM PZConfigurator.exe /F 2>$null | Out-Null
Write-Output "done"
