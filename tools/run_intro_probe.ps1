$ErrorActionPreference = "Stop"
$bat = "C:\Users\Gamer\AppData\Local\Temp\opencode\run_rpbb_window.bat"
$out = "H:\Projects\RPBB-GCN-Recomp\work\boot-intro.out.log"
$err = "H:\Projects\RPBB-GCN-Recomp\work\boot-intro.err.log"
Remove-Item -LiteralPath $out, $err -Force -ErrorAction SilentlyContinue
$p = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", "`"$bat`"" -RedirectStandardOutput $out -RedirectStandardError $err -PassThru -NoNewWindow

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32b {
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
    [Win32b]::ShowWindow($proc.MainWindowHandle, 9) | Out-Null
    [Win32b]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 400
    $r = New-Object Win32b+RECT
    [Win32b]::GetWindowRect($proc.MainWindowHandle, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    if ($w -le 0 -or $h -le 0) { return }
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output "captured $path title=$($proc.MainWindowTitle)"
}

Start-Sleep -Seconds 60
Grab "H:\Projects\RPBB-GCN-Recomp\work\intro-60s.png"
Start-Sleep -Seconds 60
Grab "H:\Projects\RPBB-GCN-Recomp\work\intro-120s.png"
Start-Sleep -Seconds 60
Grab "H:\Projects\RPBB-GCN-Recomp\work\intro-180s.png"

$exited = $p.HasExited
$code = $null
if ($exited) { $code = $p.ExitCode } else { taskkill /PID $p.Id /T /F | Out-Null }
Write-Output "ExitedEarly=$exited ExitCode=$code"
Write-Output "=== stderr (tail) ==="
Get-Content -LiteralPath $err -ErrorAction SilentlyContinue | Select-Object -Last 40
