$ErrorActionPreference = "Stop"
$runner = "H:\Projects\SB-RFD\build\moderngekko\moderngekko-run.exe"
$game = "H:\Projects\RPBB-GCN-Recomp\work\game"
$module = "C:\mg-pgo\gen-flags\gGBQE78_recomp.dll"
New-Item -ItemType Directory -Force -Path "C:\mg-pgo\play" | Out-Null
$env:LLVM_PROFILE_FILE = "C:\mg-pgo\play\GBQE78-%p.profraw"
$args = "--game `"$game`" --module `"$module`" --audio cubeb"
$p = Start-Process -FilePath $runner -ArgumentList $args -PassThru
Start-Sleep -Milliseconds 800
if (-not $p.HasExited) {
    $p.ProcessorAffinity = [IntPtr]0xFF
    $p.PriorityClass = "High"
}
