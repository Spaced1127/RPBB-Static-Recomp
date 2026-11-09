@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64 >nul
cd /d H:\Projects\SB-RFD\build\moderngekko
ninja -t commands SpongeBobRecompRunner.exe > H:\Projects\RPBB-GCN-Recomp\work\runner-link-cmds.txt 2>&1
exit /b %ERRORLEVEL%
