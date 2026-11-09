$ErrorActionPreference = "Stop"
$bat = "H:\Projects\RPBB-GCN-Recomp\tools\run_clang.bat"
$out = "H:\Projects\RPBB-GCN-Recomp\work\clang-run.out.log"
$err = "H:\Projects\RPBB-GCN-Recomp\work\clang-run.err.log"
Remove-Item -LiteralPath $out, $err -Force -ErrorAction SilentlyContinue
$p = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", "`"$bat`"" -RedirectStandardOutput $out -RedirectStandardError $err -PassThru -NoNewWindow

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32c {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@
function Find-GameWindow {
    Get-Process | Where-Object { $_.MainWindowTitle -like "ModernGekko*Rocket Power*" } | Select-Object -First 1
}
function Grab($path) {
    $proc = Find-GameWindow
    if (-not $proc) { Write-Output "no game window"; return }
    [Win32c]::ShowWindow($proc.MainWindowHandle, 9) | Out-Null
    [Win32c]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 300
    $r = New-Object Win32c+RECT
    [Win32c]::GetWindowRect($proc.MainWindowHandle, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    if ($w -le 0 -or $h -le 0) { return }
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output "$(Get-Date -Format HH:mm:ss) $($proc.MainWindowTitle) -> $path"
}

Start-Sleep -Seconds 30
Grab "H:\Projects\RPBB-GCN-Recomp\work\clang-30s.png"
Start-Sleep -Seconds 30
Grab "H:\Projects\RPBB-GCN-Recomp\work\clang-60s.png"
Start-Sleep -Seconds 30
Grab "H:\Projects\RPBB-GCN-Recomp\work\clang-90s.png"
Start-Sleep -Seconds 30
Grab "H:\Projects\RPBB-GCN-Recomp\work\clang-120s.png"

$exited = $p.HasExited
$code = $null
if ($exited) { $code = $p.ExitCode } else { taskkill /PID $p.Id /T /F | Out-Null }
Write-Output "ExitedEarly=$exited ExitCode=$code"
Write-Output "=== stderr (tail) ==="
Get-Content -LiteralPath $err -ErrorAction SilentlyContinue | Select-Object -Last 40
