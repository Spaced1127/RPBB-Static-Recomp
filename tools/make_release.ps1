param(
    [string]$Module = 'H:\Projects\SB-RFD\build\modules-call\GSQE78\dca4f72b57e8bf3f540e694f01cc23ef687d2b25028d974b67356e15f1c2e009-8f78e41483de3f25\module-pgo\gGSQE78_recomp.dll',
    [string]$Out = 'H:\Projects\SB-RFD\dist\SpongeBobRecomp-Release'
)

$ErrorActionPreference = 'Stop'
$root = 'H:\Projects\SB-RFD'

if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
New-Item -ItemType Directory $Out | Out-Null

Copy-Item "$root\build\moderngekko\SpongeBobRecomp.exe" $Out
Copy-Item "$root\build\moderngekko\SpongeBobRecompRunner.exe" $Out
Copy-Item $Module $Out
if (Test-Path "$root\build\moderngekko\Sys") {
    Copy-Item "$root\build\moderngekko\Sys" $Out -Recurse
}

New-Item -ItemType Directory "$Out\LICENSES" | Out-Null
Copy-Item "$root\lib\ModernGekko-Template\lib\ModernGekko\vendor\dolphin\COPYING" "$Out\LICENSES\Dolphin-GPL-2.0.txt"
Copy-Item "$root\lib\DolRecomp\LICENSE" "$Out\LICENSES\DolRecomp.txt"
if (Test-Path "$root\docs\release\README.txt") { Copy-Item "$root\docs\release\README.txt" $Out }
if (Test-Path "$root\docs\release\PLAYERS.md") { Copy-Item "$root\docs\release\PLAYERS.md" $Out }

# Refuse to publish if any game data slipped in.
$forbidden = Get-ChildItem $Out -Recurse -File | Where-Object {
    $_.Name -match '^(main\.dol|boot\.bin|bi2\.bin|fst\.bin|apploader\.img)$' -or
    $_.Extension -match '^\.(iso|rvz|wbfs|gcm|dol|gci|raw)$' -or
    $_.FullName -match '\\Game\\'
}
if ($forbidden) {
    Write-Output 'REFUSED: game data present in release folder:'
    $forbidden | ForEach-Object { Write-Output "  $($_.FullName)" }
    exit 1
}

$files = Get-ChildItem $Out -Recurse -File
$total = ($files | Measure-Object Length -Sum).Sum
Write-Output "release: $Out"
Write-Output ("files: {0}  bytes: {1:N0}" -f $files.Count, $total)
$files | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($Out.Length + 1)
    Write-Output ("  {0,-40} {1,14:N0}" -f $rel, $_.Length)
}
