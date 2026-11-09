$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Idle2 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
}
"@
$runner = "H:\Projects\SB-RFD\build\moderngekko\moderngekko-run.exe"
$game = "H:\Projects\RPBB-GCN-Recomp\work\game"
$module = "C:\mg-pgo\gen-flags\gGBQE78_recomp.dll"
$env:LLVM_PROFILE_FILE = "C:\mg-pgo\play\GBQE78-%p.profraw"
$args = "--game `"$game`" --module `"$module`" --audio cubeb"
$p = Start-Process -FilePath $runner -ArgumentList $args -PassThru
Start-Sleep -Milliseconds 800
if (-not $p.HasExited) { $p.ProcessorAffinity = [IntPtr]0xFF; $p.PriorityClass = "High" }
Start-Sleep -Seconds 3
$p.Refresh()
Write-Output "PID=$($p.Id) start=$($p.MainWindowTitle)"

function Grab($path) {
    $p.Refresh()
    if ($p.HasExited) { return }
    $r = New-Object Win32Idle2+RECT
    [Win32Idle2]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    if ($w -le 0 -or $h -le 0) { return }
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [Win32Idle2]::PrintWindow($p.MainWindowHandle, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc); $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "$(Get-Date -Format HH:mm:ss) $($p.MainWindowTitle) -> $path"
}

Start-Sleep -Seconds 180
Grab "H:\Projects\RPBB-GCN-Recomp\work\profile-long-180s.png"
Start-Sleep -Seconds 120
Grab "H:\Projects\RPBB-GCN-Recomp\work\profile-long-300s.png"
Start-Sleep -Seconds 90
Grab "H:\Projects\RPBB-GCN-Recomp\work\profile-long-390s.png"

$p.Refresh()
$closed = $p.CloseMainWindow()
Write-Output "CloseMainWindow=$closed"
Start-Sleep -Seconds 12
$p.Refresh()
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force; Write-Output "forced stop" } else { Write-Output "exited normally" }
Start-Sleep -Seconds 2
Write-Output "=== profiles ==="
Get-ChildItem "C:\mg-pgo\play" -File -ErrorAction SilentlyContinue | Select-Object Name,Length,LastWriteTime
