Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowW(string cls, string title);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int hh, uint f);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
function Snap([string]$out, [int]$x, [int]$y, [int]$w, [int]$hh) {
    [Win32]::SetProcessDPIAware() | Out-Null
    $proc = Start-Process -FilePath "PZConfigurator\bin\Debug\net10.0\PZConfigurator.exe" -PassThru
    Start-Sleep -Seconds 5
    $proc.WaitForInputIdle(10000) | Out-Null
    $h = $proc.MainWindowHandle
    if ($h -eq [IntPtr]::Zero) { throw "window not found" }
    [Win32]::SetWindowPos($h, [IntPtr]::Zero, $x, $y, $w, $hh, 0x0040) | Out-Null
    [Win32]::SetForegroundWindow($h) | Out-Null
    Start-Sleep -Seconds 2
    $r = New-Object Win32+RECT
    [Win32]::GetWindowRect($h, [ref]$r) | Out-Null
    $bw = $r.Right - $r.Left; $bh = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap($bw, $bh)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [Win32]::PrintWindow($h, $hdc, 2) | Out-Null # 2 = PW_RENDERFULLCONTENT
    $g.ReleaseHdc($hdc)
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Stop-Process -Id $proc.Id -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Output "saved $out ($bw x $bh)"
}
Snap "resp_wide.png" 50 50 1400 800
Snap "resp_narrow.png" 50 50 782 424
Snap "resp_mid.png" 50 50 1000 650
