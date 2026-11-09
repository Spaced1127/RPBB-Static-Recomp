@echo off
setlocal
copy /Y "H:\Projects\SB-RFD\build\moderngekko\SpongeBobRecompRunner.exe" "H:\Projects\SB-RFD\build\moderngekko\SpongeBobRecompRunner.orig.exe" >nul
call "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64 >nul
set "PATH=C:\Program Files\CMake\bin;%PATH%"
cmake -S "H:/Projects/SB-RFD/lib/ModernGekko-Template/lib/ModernGekko" -B "H:/Projects/SB-RFD/build/moderngekko" "-DCMAKE_EXE_LINKER_FLAGS=/machine:x64 /MAP:H:/Projects/RPBB-GCN-Recomp/work/moderngekko-run.map"
if errorlevel 1 exit /b 1
cmake --build "H:/Projects/SB-RFD/build/moderngekko" --target SpongeBobRecompRunner.exe
exit /b %ERRORLEVEL%
