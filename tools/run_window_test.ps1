$ErrorActionPreference = "Stop"
$bat = "C:\Users\Gamer\AppData\Local\Temp\opencode\run_rpbb_window.bat"
$out = "H:\Projects\RPBB-GCN-Recomp\work\boot-window.out.log"
$err = "H:\Projects\RPBB-GCN-Recomp\work\boot-window.err.log"
$shot = "H:\Projects\RPBB-GCN-Recomp\work\boot-window.png"
Remove-Item -LiteralPath $out, $err, $shot -Force -ErrorAction SilentlyContinue
$p = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", "`"$bat`"" -RedirectStandardOutput $out -RedirectStandardError $err -PassThru -NoNewWindow
Start-Sleep -Seconds 20

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$bmp.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()

$exited = $p.HasExited
$code = $null
if ($exited) { $code = $p.ExitCode } else { taskkill /PID $p.Id /T /F | Out-Null }
Write-Output "ExitedEarly=$exited ExitCode=$code"
Write-Output "Screenshot=$shot"
Write-Output "=== stdout (tail) ==="
Get-Content -LiteralPath $out -ErrorAction SilentlyContinue | Select-Object -Last 40
Write-Output "=== stderr (tail) ==="
Get-Content -LiteralPath $err -ErrorAction SilentlyContinue | Select-Object -Last 80
