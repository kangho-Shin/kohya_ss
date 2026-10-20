@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0Install-Agent.ps1"
if errorlevel 1 (
  echo Installation failed. Run this file as administrator and read the error above.
  pause
  exit /b 1
)
pause
