param(
    [string]$Name = "ir1",
    [int]$Ir = 1,
    [string]$Backend = "",
    [string]$Efb = "true",
    [int]$ShaderMode = 2,
    [bool]$WaitShaders = $true,
    [int]$WaitSeconds = 70,
    [string]$Module = "H:\Projects\RPBB-GCN-Recomp\build\pgo-gameplay\gGBQE78_recomp.dll"
)
$ErrorActionPreference = "Stop"
$base = "$env:LOCALAPPDATA\moderngekko"
$dir = "H:\Projects\RPBB-GCN-Recomp\work\cfgtest-$Name"
Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path "$dir\Config" | Out-Null
Copy-Item "$base\Config\Dolphin.ini" "$dir\Config\Dolphin.ini" -Force
Copy-Item "$base\Config\GFX.ini" "$dir\Config\GFX.ini" -Force
if (Test-Path "$base\config.ini") { Copy-Item "$base\config.ini" "$dir\config.ini" -Force }

$gfx = Get-Content "$dir\Config\GFX.ini" -Raw
$gfx = $gfx -replace '(?m)^InternalResolution\s*=.*$', "InternalResolution = $Ir"
$gfx = $gfx -replace '(?m)^ShaderCompilationMode\s*=.*$', "ShaderCompilationMode = $ShaderMode"
$gfx = $gfx -replace '(?m)^WaitForShadersBeforeStarting\s*=.*$', "WaitForShadersBeforeStarting = $($WaitShaders.ToString())"
Set-Content "$dir\Config\GFX.ini" $gfx -Encoding ASCII

$cfg = Get-Content "$dir\config.ini" -Raw
$cfg = $cfg -replace '(?m)^efb_access\s*=.*$', "efb_access=$Efb"
Set-Content "$dir\config.ini" $cfg -Encoding ASCII

$runner = "H:\Projects\SB-RFD\build\moderngekko\moderngekko-run.exe"
$game = "H:\Projects\RPBB-GCN-Recomp\work\game"
$module = $Module
$log = "H:\Projects\RPBB-GCN-Recomp\work\cfgtest-$Name.err"
Remove-Item $log -ErrorAction SilentlyContinue
$args = "--game `"$game`" --module `"$module`" --audio cubeb --user-dir `"$dir`""
if ($Backend) { $args += " --graphics $Backend" }
Get-Process moderngekko-run -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
$p = Start-Process -FilePath $runner -ArgumentList $args -RedirectStandardError $log -PassThru
Start-Sleep -Milliseconds 1500
if (-not $p.HasExited) { $p.ProcessorAffinity = [IntPtr]0xFF; $p.PriorityClass = "High" }
$samples = @()
for ($i = 1; $i -le $WaitSeconds; $i += 5) {
    Start-Sleep -Seconds 5
    $p.Refresh()
    if ($p.HasExited) { break }
    $t = $p.MainWindowTitle
    if ($t -match '\| ([0-9.]+) FPS') { $samples += [double]$matches[1] }
}
$p.Refresh()
Write-Output "CONFIG=$Name IR=$Ir Backend=$Backend EFB=$Efb Shader=$ShaderMode Wait=$WaitShaders"
Write-Output "TITLE=$($p.MainWindowTitle)"
Write-Output "SAMPLES=$($samples -join ',')"
if ($samples.Count -gt 0) { Write-Output ("LAST3=" + (($samples | Select-Object -Last 3) -join ',')) }
Get-Content $log -Tail 8 -ErrorAction SilentlyContinue
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
