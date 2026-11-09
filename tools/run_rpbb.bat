@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run_rpbb.ps1"
exit /b %ERRORLEVEL%
