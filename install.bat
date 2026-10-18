@echo off
setlocal
cd /d "%~dp0"
py -3.11 --version >nul 2>&1
if errorlevel 1 (
  echo Python 3.11 is required. Install it from https://www.python.org/downloads/windows/
  pause
  exit /b 1
)
if not exist ".venv\Scripts\python.exe" py -3.11 -m venv .venv
if errorlevel 1 goto fail
".venv\Scripts\python.exe" -m pip install --upgrade pip
if errorlevel 1 goto fail
".venv\Scripts\python.exe" -m pip install -r requirements.txt
if errorlevel 1 goto fail
echo Base installation complete. Next: install-gpu.bat for RTX 3080.
echo Then prepare-models.bat and run.bat.
pause
exit /b 0
:fail
echo Installation failed. Read the error above. Do not run the next step yet.
pause
exit /b 1
