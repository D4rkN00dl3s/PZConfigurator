Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class W {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
}
"@

$proc = Start-Process -FilePath "PZConfigurator\bin\Debug\net10.0\PZConfigurator.exe" -PassThru
Start-Sleep 6
$proc.WaitForInputIdle(10000) | Out-Null
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)

# toggle a checkbox -> dirty
$cb = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::CheckBox)))
$cb.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
Start-Sleep 2
Write-Output "after toggle, title: $($root.Current.Name)"

# try to close -> expect "Unsaved changes" dialog
$proc.CloseMainWindow() | Out-Null
Start-Sleep 4

# find the dialog window of this process by title
$myPid = $proc.Id
$dlgH = [IntPtr]::Zero
[W]::EnumWindows({ param($h, $l)
    $pid2 = 0
    [W]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
    if ($pid2 -eq $myPid) {
        $sb = New-Object System.Text.StringBuilder 256
        [W]::GetWindowTextW($h, $sb, 256) | Out-Null
        if ($sb.ToString() -eq "Unsaved changes") { $script:dlgH = $h }
    }
    return $true
}, [IntPtr]::Zero) | Out-Null

if ($dlgH -eq [IntPtr]::Zero) { Write-Output "RESULT: no dialog"; Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue; exit }

$dlg = [System.Windows.Automation.AutomationElement]::FromHandle($dlgH)
$btn = $dlg.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "Discard changes")))
if (-not $btn) { Write-Output "RESULT: dialog but no button"; Stop-Process -Id $proc.Id -Force; exit }
$btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep 4
$proc.Refresh()
if ($proc.HasExited) { Write-Output "RESULT: window closed after Discard - FIXED" }
else {
    Write-Output "RESULT: window STILL OPEN - bug persists"
    Stop-Process -Id $proc.Id -Force
}
