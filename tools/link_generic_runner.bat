@echo off
setlocal
set "OBJ=H:\Projects\SB-RFD\build\moderngekko\CMakeFiles\moderngekko-run.dir\tools\moderngekko_run.cpp.obj"
copy /Y "%OBJ%" "%OBJ%.orig" >nul
copy /Y "H:\Projects\SB-RFD\build\moderngekko\CMakeFiles\moderngekko-run.dir\tools\moderngekko_run_generic.cpp.obj" "%OBJ%" >nul
call "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64 >nul
cmake --build "H:/Projects/SB-RFD/build/moderngekko" --target SpongeBobRecompRunner.exe
if errorlevel 1 exit /b 1
copy /Y "H:\Projects\SB-RFD\build\moderngekko\SpongeBobRecompRunner.exe" "H:\Projects\RPBB-GCN-Recomp\work\moderngekko-run-generic.exe" >nul
copy /Y "%OBJ%.orig" "%OBJ%" >nul
exit /b 0
