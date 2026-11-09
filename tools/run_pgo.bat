@echo off
setlocal
"H:\Projects\SB-RFD\build\moderngekko\moderngekko-run.exe" --game "%~dp0..\work\game" --module "%~dp0..\build\pgo\gGBQE78_recomp.dll" --audio cubeb %*
exit /b %ERRORLEVEL%
