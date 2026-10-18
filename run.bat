@echo off
setlocal
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" (
  echo Run install.bat first.
  pause
  exit /b 1
)
".venv\Scripts\python.exe" -m faceswap_app
if errorlevel 1 (
  echo Program failed. Read the error above.
  pause
  exit /b 1
)
exit /b 0
