$ErrorActionPreference = "Stop"
$runner = "H:\Projects\SB-RFD\build\moderngekko\moderngekko-run.exe"
$game = "H:\Projects\RPBB-GCN-Recomp\work\game"
$module = "H:\Projects\RPBB-GCN-Recomp\build\pgo-gameplay\gGBQE78_recomp.dll"
$args = "--game `"$game`" --module `"$module`" --audio cubeb"
$p = Start-Process -FilePath $runner -ArgumentList $args -PassThru
Start-Sleep -Milliseconds 800
if (-not $p.HasExited) {
    # 0xFF = logical cores 0-7, the eight physical cores on the Ryzen 7 7800X3D.
    # Keeping the main thread and DSP LLE thread off SMT siblings recovers ~20%.
    $p.ProcessorAffinity = [IntPtr]0xFF
    $p.PriorityClass = "High"
}


