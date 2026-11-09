$ErrorActionPreference = "Stop"
$bat = "C:\Users\Gamer\AppData\Local\Temp\opencode\run_rpbb_headless.bat"
$out = "H:\Projects\RPBB-GCN-Recomp\work\boot-headless.out.log"
$err = "H:\Projects\RPBB-GCN-Recomp\work\boot-headless.err.log"
Remove-Item -LiteralPath $out, $err -Force -ErrorAction SilentlyContinue
$p = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", "`"$bat`"" -RedirectStandardOutput $out -RedirectStandardError $err -PassThru -NoNewWindow
Start-Sleep -Seconds 25
$exited = $p.HasExited
$code = $null
if ($exited) { $code = $p.ExitCode } else { taskkill /PID $p.Id /T /F | Out-Null }
Write-Output "ExitedEarly=$exited ExitCode=$code"
Write-Output "=== stdout (tail) ==="
Get-Content -LiteralPath $out -ErrorAction SilentlyContinue | Select-Object -Last 80
Write-Output "=== stderr (tail) ==="
Get-Content -LiteralPath $err -ErrorAction SilentlyContinue | Select-Object -Last 80
