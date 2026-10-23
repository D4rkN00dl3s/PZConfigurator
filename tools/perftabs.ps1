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
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class C {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
    const uint LEFTDOWN = 0x02, LEFTUP = 0x04;
    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        mouse_event(LEFTDOWN, x, y, 0, UIntPtr.Zero);
        mouse_event(LEFTUP, x, y, 0, UIntPtr.Zero);
    }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
"@

[C]::SetProcessDPIAware() | Out-Null

if (Test-Path tools\switchlog.txt) { Remove-Item tools\switchlog.txt }

Start-Process -FilePath "PZConfigurator\bin\Debug\net10.0\PZConfigurator.exe"
Start-Sleep 6
$proc = Get-Process PZConfigurator | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { Write-Output "FAIL: no window"; exit 1 }
$h = $proc.MainWindowHandle
$root = [System.Windows.Automation.AutomationElement]::FromHandle($h)

$tabs = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::TabItem)))
if ($tabs.Count -lt 2) { Write-Output "FAIL: tabs not found"; taskkill /IM PZConfigurator.exe /F 2>$null | Out-Null; exit 1 }

$ini = $tabs | Where-Object { $_.Current.Name -like "*ini*" } | Select-Object -First 1
$sbx = $tabs | Where-Object { $_.Current.Name -like "*Sandbox*" } | Select-Object -First 1

[C]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 500

# click center of tab header
$rect = $sbx.Current.BoundingRectangle
[C]::Click([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2))

Start-Sleep -Milliseconds 1500
$rect = $ini.Current.BoundingRectangle
[C]::Click([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2))
Start-Sleep -Milliseconds 1500

for ($i = 0; $i -lt 4; $i++) {
    $rect = $sbx.Current.BoundingRectangle
    [C]::Click([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2))
    Start-Sleep -Milliseconds 700
    $rect = $ini.Current.BoundingRectangle
    [C]::Click([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2))
    Start-Sleep -Milliseconds 700
}

taskkill /IM PZConfigurator.exe /F 2>$null | Out-Null
Get-Content tools\switchlog.txt
