@echo off
setlocal
cd /d "H:\Projects\RPBB-GCN-Recomp"
call "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64
if errorlevel 1 exit /b 1
set "PATH=C:\Program Files\LLVM\bin;%PATH%"
set "CFLAGS=-march=x86-64-v3 -mtune=generic"
set "DOLRECOMP_C_LOOP_CYCLE_BUDGET=4096"
set "DOLRECOMP_C_MAX_CALL_DEPTH=128"
"H:\Projects\SB-RFD\build\moderngekko\moderngekko-port.exe" pgo-run "H:\Projects\RPBB-GCN-Recomp\work\game" --backend c --toolchain clang --opt-level 3 --profile-dir C:\mg-pgo --output C:\mg-out --llvm-profdata "C:\Program Files\LLVM\bin\llvm-profdata.exe" -- --audio cubeb > "H:\Projects\RPBB-GCN-Recomp\work\pgo.log" 2>&1
exit /b %ERRORLEVEL%
