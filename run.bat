@echo off
setlocal
cd /d "%~dp0"
set "GAME=%~1"
if "%GAME%"=="" set "GAME=%~dp0game"
if not exist "%GAME%" (
  echo Put your extracted Rocket Power: Beach Bandits (USA) folder in "%~dp0game",
  echo or pass the folder or .iso path:
  echo   run.bat "D:\Rocket Power Beach Bandits.iso"
  pause
  exit /b 1
)
if /i "%GAME:~-4%"==".iso" (
  if not exist "%LOCALAPPDATA%\RocketPowerRecomp\game\sys\main.dol" (
    "%~dp0rpbb_launch.exe" --prepare --game "%GAME%" --user-dir "%LOCALAPPDATA%\RocketPowerRecomp"
    if errorlevel 1 (
      pause
      exit /b 1
    )
  )
)
"%~dp0rpbb_launch.exe" --game "%GAME%" --user-dir "%LOCALAPPDATA%\RocketPowerRecomp"
