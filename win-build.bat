@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build_hamtool.ps1"
exit /b %ERRORLEVEL%
