@echo off
setlocal
cd /d "%~dp0"
set "GAME=%~1"
if "%GAME%"=="" set "GAME=%~dp0game"
if not exist "%GAME%" (
  echo Put your extracted Rocket Power: Beach Bandits (USA) folder in "%~dp0game",
  echo or pass the folder or .iso/.rvz path:
  echo   run.bat "D:\Rocket Power Beach Bandits.iso"
  echo   run.bat "D:\Rocket Power Beach Bandits.rvz"
  pause
  exit /b 1
)
if /i "%GAME:~-4%"==".iso" goto prepare
if /i "%GAME:~-4%"==".rvz" goto prepare
goto launch

:prepare
if not exist "%LOCALAPPDATA%\RocketPowerRecomp\game\sys\main.dol" (
  "%~dp0rpbb-launcher.exe" --prepare --game "%GAME%" --user-dir "%LOCALAPPDATA%\RocketPowerRecomp"
  if errorlevel 1 (
    pause
    exit /b 1
  )
)

:launch
"%~dp0rpbb-launcher.exe" --game "%GAME%" --user-dir "%LOCALAPPDATA%\RocketPowerRecomp"
