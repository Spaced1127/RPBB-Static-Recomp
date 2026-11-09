$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Idle {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
}
"@
$p = Get-Process -Id 166400 -ErrorAction SilentlyContinue
if (-not $p) { Write-Output "instrumented game not running"; exit 1 }

function Grab($path) {
    $r = New-Object Win32Idle+RECT
    [Win32Idle]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    if ($w -le 0 -or $h -le 0) { return }
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [Win32Idle]::PrintWindow($p.MainWindowHandle, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc); $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $p.Refresh()
    Write-Output "$(Get-Date -Format HH:mm:ss) $($p.MainWindowTitle) -> $path"
}

Start-Sleep -Seconds 60
Grab "H:\Projects\RPBB-GCN-Recomp\work\profile-idle-60s.png"
Start-Sleep -Seconds 60
Grab "H:\Projects\RPBB-GCN-Recomp\work\profile-idle-120s.png"
Start-Sleep -Seconds 60
Grab "H:\Projects\RPBB-GCN-Recomp\work\profile-idle-180s.png"

# Normal close so the profile flushes.
$p.Refresh()
$closed = $p.CloseMainWindow()
Write-Output "CloseMainWindow=$closed"
Start-Sleep -Seconds 12
$p.Refresh()
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force; Write-Output "forced stop" } else { Write-Output "exited normally" }
Start-Sleep -Seconds 2
Write-Output "=== profiles ==="
Get-ChildItem "C:\mg-pgo\play" -File -ErrorAction SilentlyContinue | Select-Object Name,Length,LastWriteTime
