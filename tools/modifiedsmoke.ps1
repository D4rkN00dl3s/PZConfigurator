# Screenshot smoke: change a value on the Sandbox tab, verify yellow highlight on the key
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

# checkbox inside the sandbox detail pane is found under the window; use the
# one whose label sits left of it: just take the first CheckBox whose X > tab-header X
$boxes = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::CheckBox)))
foreach ($b in $boxes) {
    $br = $b.Current.BoundingRectangle
    if ($br.X -gt 330) { [C]::Click([int]($br.X + 8), [int]($br.Y + $br.Height / 2)); break }
}
Start-Sleep -Milliseconds 800

$r = New-Object 'S+RECT'
[S]::GetWindowRect($h, [ref]$r) | Out-Null
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap(([int]($r.R - $r.L)), ([int]($r.B - $r.T)))
$g = [System.Drawing.Graphics]::FromImage($bmp)
$dc = $g.GetHdc()
[S]::PrintWindow($h, $dc, 2) | Out-Null
$g.ReleaseHdc($dc)
$bmp.Save("tools\modified-smoke.png", [System.Drawing.Imaging.ImageFormat]::Png)
taskkill /IM PZConfigurator.exe /F 2>$null | Out-Null
Write-Output "done"
