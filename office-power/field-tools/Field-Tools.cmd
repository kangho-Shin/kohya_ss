@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Field-Tools.ps1"
if errorlevel 1 (
  echo Failed or administrator approval was cancelled. Read the error above.
  pause
  exit /b 1
)
