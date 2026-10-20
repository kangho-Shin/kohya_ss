@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0PowerMaster-Tools.ps1"
if errorlevel 1 (
  echo Failed. Read the error above.
  pause
  exit /b 1
)
