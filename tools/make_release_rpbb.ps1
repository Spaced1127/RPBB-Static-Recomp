param(
    [string]$Version = "0.1.2",
    [string]$Runner = "H:\Projects\RPBB-GCN-Recomp\vendor\rpbb.exe",
    [string]$Sys = "H:\Projects\SB-RFD\dist\SpongeBobRecomp-Release-v0.1.2\Sys",
    [string]$Licenses = "H:\Projects\SB-RFD\dist\SpongeBobRecomp-Release-v0.1.2\LICENSES"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$stageName = "RocketPower-Recomp-v$Version-Windows-x64"
$stage = Join-Path $root "dist\$stageName"
$zip = Join-Path $root "dist\$stageName.zip"

foreach ($p in @($Runner, $Sys, $Licenses, (Join-Path $root "recomplauncher.json"),
                 (Join-Path $root "README.md"), (Join-Path $root "PLAYERS.md"),
                 (Join-Path $root "run.bat"), (Join-Path $root "tools\rpbb_launcher.cs"),
                 (Join-Path $root "tools\set_icon.cs"),
                 (Join-Path $root "assets\rpbb.ico"),
                 (Join-Path $root "assets\rpbb-launcher.ico"),
                 (Join-Path $root "defaults\GCPadNew.ini"))) {
    if (-not (Test-Path $p)) { throw "Missing release input: $p" }
}

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage "defaults") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage "game") | Out-Null

# The setup/start helper is compiled from source so it always matches this tree.
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }
& $csc /nologo /optimize+ /target:exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /win32icon:"$root\assets\rpbb-launcher.ico" /out:"$stage\rpbb-launcher.exe" (Join-Path $root "tools\rpbb_launcher.cs")
if ($LASTEXITCODE -ne 0) { throw "rpbb-launcher.exe build failed" }

Copy-Item -LiteralPath $Runner -Destination (Join-Path $stage "rpbb.exe") -Force

$setIcon = Join-Path $root "work\set_icon.exe"
& $csc /nologo /target:exe /out:"$setIcon" (Join-Path $root "tools\set_icon.cs")
if ($LASTEXITCODE -ne 0) { throw "set_icon.exe build failed" }
& $setIcon (Join-Path $stage "rpbb.exe") (Join-Path $root "assets\rpbb.ico")
if ($LASTEXITCODE -ne 0) { throw "rpbb.exe icon update failed" }
Copy-Item -LiteralPath $Sys -Destination (Join-Path $stage "Sys") -Recurse -Force
Copy-Item -LiteralPath $Licenses -Destination (Join-Path $stage "LICENSES") -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root "recomplauncher.json") -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $root "README.md") -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $root "PLAYERS.md") -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $root "run.bat") -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $root "defaults\GCPadNew.ini") -Destination (Join-Path $stage "defaults") -Force
Set-Content -LiteralPath (Join-Path $stage "VERSION") -Value "v$Version" -Encoding ASCII

$gameReadme = @"
Put your extracted Rocket Power: Beach Bandits (USA) game folder HERE.

It must contain:
  sys\main.dol
  files\

Or run run.bat with the path to your ISO:
  run.bat "D:\Rocket Power Beach Bandits.iso"
"@
Set-Content -LiteralPath (Join-Path $stage "game\README.txt") -Value $gameReadme -Encoding ASCII

# Never ship game data. The stage is assembled from binaries and docs only; this is
# a hard guard in case a future edit points a copy at the wrong folder.
$bad = Get-ChildItem $stage -Recurse -File | Where-Object {
    $_.Extension -in ".iso", ".rvz", ".gcm", ".dol", ".thp", ".pak" -or
    $_.FullName -match '\\sys\\main\.dol$'
}
if ($bad) { throw ("Refusing to package game data: " + (($bad | ForEach-Object FullName) -join ", ")) }

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
$size = (Get-Item $zip).Length
Write-Output "Built $zip"
Write-Output ("Size: {0:N0} bytes" -f $size)
Write-Output "SHA-256: $hash"
Write-Output "recomplauncher.json: $(Test-Path (Join-Path $stage 'recomplauncher.json'))"
