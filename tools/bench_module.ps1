param(
    [Parameter(Mandatory = $true)][string]$Module,
    [string]$Label = 'bench',
    [int]$Seconds = 12,
    [string]$User = 'UserDefault'
)

$ErrorActionPreference = 'Stop'
$app = 'H:\Projects\SB-RFD\dist\SpongeBobRecomp'
$runner = Join-Path $app 'SpongeBobRecompRunner.exe'
$game = Join-Path $app 'Game'
$user = Join-Path $app $User
$state = 'H:\Projects\SB-RFD\work\auto9\gameplay.state'
$auto = Join-Path $app "Bench-$Label"
if (Test-Path $auto) { Remove-Item $auto -Recurse -Force }
New-Item -ItemType Directory "$auto\commands" -Force | Out-Null

$err = Join-Path $auto 'stderr.txt'
$p = Start-Process -FilePath $runner -ArgumentList @(
    '--game', $game,
    '--module', $Module,
    '--user-dir', $user,
    '--graphics', 'Vulkan',
    '--audio', 'Null',
    '--allow-interpreter',
    '--automation-dir', $auto,
    '--load-state', $state
) -RedirectStandardError $err -PassThru

Start-Sleep -Seconds $Seconds
Set-Content (Join-Path $auto 'commands\stop.txt') "command=stop`n"
Start-Sleep -Seconds 3
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }

$statusPath = Join-Path $auto 'status.txt'
$fps = '?'
$speed = '?'
$frames = '?'
if (Test-Path $statusPath) {
    $s = Get-Content $statusPath -Raw
    if ($s -match 'fps=([0-9.]+)') { $fps = $Matches[1] }
    if ($s -match 'speed=([0-9.]+)') { $speed = $Matches[1] }
    if ($s -match 'frame_count=([0-9]+)') { $frames = $Matches[1] }
}
$size = if (Test-Path $Module) { (Get-Item $Module).Length } else { 0 }
Write-Output ("{0,-22} fps={1,-8} speed={2,-8} frames={3,-8} bytes={4}" -f $Label, $fps, $speed, $frames, $size)
