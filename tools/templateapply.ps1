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
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    public struct RECT { public int L, T, R, B; }
}
"@
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class C {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    public static void Click(int x, int y) {
        SetCursorPos(x, y); mouse_event(0x02,0,0,0,UIntPtr.Zero); mouse_event(0x04,0,0,0,UIntPtr.Zero);
    }
}
"@
[S]::SetProcessDPIAware() | Out-Null
taskkill /IM PZConfigurator.exe /F 2>$null | Out-Null

Start-Process -FilePath "PZConfigurator\bin\Debug\net10.0\PZConfigurator.exe"
Start-Sleep 6
$proc = Get-Process PZConfigurator | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$h = $proc.MainWindowHandle
$root = [System.Windows.Automation.AutomationElement]::FromHandle($h)

$tabs = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::TabItem)))
[C]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 400
$sbx = $tabs | Where-Object { $_.Current.Name -like "*Sandbox*" } | Select-Object -First 1
$rect = $sbx.Current.BoundingRectangle
[C]::Click([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2))
Start-Sleep -Milliseconds 1000

# open the template combo and pick the first entry
# find the template combo by its stable AutomationId
$combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        "TemplateCombo")))
if (-not $combo) { Write-Output "FAIL: TemplateCombo not found"; taskkill /IM PZConfigurator.exe /F 2>$null | Out-Null; exit 1 }
$crect = $combo.Current.BoundingRectangle
[C]::Click([int]($crect.X + $crect.Width - 20), [int]($crect.Y + $crect.Height / 2))
Start-Sleep -Milliseconds 1000
$items = $combo.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)))
Write-Output "combo items: $($items.Count): $(($items | ForEach-Object { $_.Current.Name }) -join ', ')"
if ($items.Count -eq 0) { Write-Output "FAIL: no combo items found"; taskkill /IM PZConfigurator.exe /F 2>$null | Out-Null; exit 1 }
($items | Select-Object -First 1).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
Start-Sleep -Milliseconds 800

# dismiss the popup with a harmless click on the tab header, then click Apply
[C]::Click([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2))
Start-Sleep -Milliseconds 500
$apply = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "Apply")))
$arect = $apply.Current.BoundingRectangle
[C]::Click([int]($arect.X + $arect.Width / 2), [int]($arect.Y + $arect.Height / 2))
Start-Sleep -Milliseconds 400
[C]::Click([int]($arect.X + $arect.Width / 2), [int]($arect.Y + $arect.Height / 2))
Start-Sleep -Milliseconds 800

# read the status line (TextBlock matching load/apply messages)
$texts = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Text)))
$status = $texts | Where-Object { $_.Current.Name -and $_.Current.Name -match "Applied|Loaded|template|Select" }

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
