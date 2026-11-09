$ErrorActionPreference = "Stop"
# 60 FPS path: Dolphin's JIT core on the same ModernGekko runner. The static
# C module is still the default in run_rpbb.ps1; this launcher selects the
# dynamic core instead, which has enough headroom to hold the VI rate.
$env:MODERNGEKKO_STATICRECOMP = "0"
$runner = "H:\Projects\SB-RFD\build\moderngekko\moderngekko-run.exe"
$game = "H:\Projects\RPBB-GCN-Recomp\work\game"
$args = "--game `"$game`" --audio cubeb --allow-interpreter"
$p = Start-Process -FilePath $runner -ArgumentList $args -PassThru
Start-Sleep -Milliseconds 800
if (-not $p.HasExited) {
    # 0xFF = logical cores 0-7, the eight physical cores on the Ryzen 7 7800X3D.
    $p.ProcessorAffinity = [IntPtr]0xFF
    $p.PriorityClass = "High"
}
