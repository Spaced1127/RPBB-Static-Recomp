$ErrorActionPreference = "Stop"
$bat = "C:\Users\Gamer\AppData\Local\Temp\opencode\run_rpbb_window.bat"
$out = "H:\Projects\RPBB-GCN-Recomp\work\boot-long.out.log"
$err = "H:\Projects\RPBB-GCN-Recomp\work\boot-long.err.log"
Remove-Item -LiteralPath $out, $err -Force -ErrorAction SilentlyContinue
$p = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", "`"$bat`"" -RedirectStandardOutput $out -RedirectStandardError $err -PassThru -NoNewWindow

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
function Grab($path) {
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}

Start-Sleep -Seconds 20
Grab "H:\Projects\RPBB-GCN-Recomp\work\boot-20s.png"
Start-Sleep -Seconds 25
Grab "H:\Projects\RPBB-GCN-Recomp\work\boot-45s.png"
Start-Sleep -Seconds 30
Grab "H:\Projects\RPBB-GCN-Recomp\work\boot-75s.png"

$exited = $p.HasExited
$code = $null
if ($exited) { $code = $p.ExitCode } else { taskkill /PID $p.Id /T /F | Out-Null }
Write-Output "ExitedEarly=$exited ExitCode=$code"
Write-Output "=== stderr (tail) ==="
Get-Content -LiteralPath $err -ErrorAction SilentlyContinue | Select-Object -Last 40
