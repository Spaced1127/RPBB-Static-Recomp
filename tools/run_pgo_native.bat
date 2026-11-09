@echo off
setlocal
start "" /high "H:\Projects\SB-RFD\build\moderngekko\moderngekko-run.exe" --game "%~dp0..\work\game" --module "%~dp0..\build\pgo-native\gGBQE78_recomp.dll" --audio cubeb
exit /b 0
