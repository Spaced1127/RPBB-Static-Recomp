@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64 >nul
"C:\Program Files\CMake\bin\cmake.exe" -S "H:/Projects/SB-RFD/lib/ModernGekko-Template/lib/ModernGekko" -B "H:/Projects/SB-RFD/build/moderngekko" -DCMAKE_EXE_LINKER_FLAGS=/machine:x64
exit /b %ERRORLEVEL%
