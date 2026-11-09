param(
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][string]$Module,
    [int]$SettleSeconds = 4,
    [int]$SampleSeconds = 10,
    [switch]$KeepRunning,
    [int]$ProfileSeconds = 0,
    [string]$ProfileOut = "",
    [string]$Runner = "H:\Projects\SB-RFD\build\moderngekko\moderngekko-run.exe"
)
$ErrorActionPreference = "Stop"
$auto = "H:\Projects\RPBB-GCN-Recomp\work\autobench"
$user = "H:\Projects\RPBB-GCN-Recomp\work\autouser"
$state = "states/gameplay.state"
$runner = $Runner
$game = "H:\Projects\RPBB-GCN-Recomp\work\game"

Get-Process moderngekko-run -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
Remove-Item "$auto\commands\*" -Force -ErrorAction SilentlyContinue
Remove-Item "$auto\processed\*" -Force -ErrorAction SilentlyContinue
Remove-Item "$auto\failed\*" -Force -ErrorAction SilentlyContinue

function SendCmd([string]$name, [string]$body) {
    $path = "$auto\commands\$name"
    Set-Content $path $body -Encoding ASCII
    for ($i = 0; $i -lt 2400; $i++) {
        if (-not (Test-Path $path)) { return $true }
        Start-Sleep -Milliseconds 50
    }
    return $false
}

function ReadStatus {
    if (-not (Test-Path "$auto\status.txt")) { return $null }
    $text = Get-Content "$auto\status.txt" -Raw
    $map = @{}
    foreach ($line in ($text -split "`r?`n")) {
        if ($line -match '^([^=]+)=(.*)$') { $map[$matches[1]] = $matches[2] }
    }
    return $map
}

$log = "H:\Projects\RPBB-GCN-Recomp\work\bench-$Name.err"
Remove-Item $log -ErrorAction SilentlyContinue
$args = "--game `"$game`" --module `"$Module`" --audio cubeb --user-dir `"$user`" --automation-dir `"$auto`""
$p = Start-Process -FilePath $runner -ArgumentList $args -RedirectStandardError $log -PassThru
Start-Sleep -Milliseconds 1500
if (-not $p.HasExited) { $p.ProcessorAffinity = [IntPtr]0xFF; $p.PriorityClass = "High" }

for ($i = 0; $i -lt 120; $i++) {
    Start-Sleep -Milliseconds 500
    $st = ReadStatus
    if ($st -and $st.booted -eq '1' -and $st.state -eq 'running') { break }
}

$ok = SendCmd "001-load.txt" "command=load_state`npath=$state`n"
Start-Sleep -Seconds $SettleSeconds
$ok2 = SendCmd "002-shot.txt" "command=screenshot`npath=shots/bench-$Name.png`n"

if ($ProfileSeconds -gt 0) {
    if (-not $ProfileOut) { $ProfileOut = "H:\Projects\RPBB-GCN-Recomp\work\profile-$Name.csv" }
    & "H:\Projects\RPBB-GCN-Recomp\tools\sample_thread.exe" --pid $p.Id --seconds $ProfileSeconds --interval 1 --threads 1 --out $ProfileOut
}

$fps = @()
$speeds = @()
for ($i = 0; $i -lt ($SampleSeconds * 2); $i++) {
    Start-Sleep -Milliseconds 500
    $st = ReadStatus
    if ($st -and $st.fps) { $fps += [double]$st.fps }
    if ($st -and $st.speed) { $speeds += [double]$st.speed }
}
$st = ReadStatus
Write-Output "BENCH=$Name MODULE=$([System.IO.Path]::GetFileName($Module))"
Write-Output "TITLE=$($st.title) FPS=$($st.fps) SPEED=$($st.speed)"
if ($fps.Count -gt 0) {
    $avg = ($fps | Measure-Object -Average).Average
    $min = ($fps | Measure-Object -Minimum).Minimum
    $max = ($fps | Measure-Object -Maximum).Maximum
    Write-Output ("FPS_AVG={0:F2} MIN={1:F2} MAX={2:F2} N={3}" -f $avg, $min, $max, $fps.Count)
}
Write-Output "LAST_ERROR=$($st.last_error) PROCESSED=$($st.processed_commands)"
Write-Output ("ERR=" + ((Get-Content $log -Tail 4 -ErrorAction SilentlyContinue) -join ' | '))
Write-Output "PID=$($p.Id)"
if (-not $KeepRunning) {
    Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
}
