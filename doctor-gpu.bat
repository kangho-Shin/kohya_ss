@echo off
setlocal
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" goto fail
".venv\Scripts\python.exe" -m scripts.doctor --device cuda --load
if errorlevel 1 goto fail
pause
exit /b 0
:fail
echo GPU check failed. Read the errors above.
pause
exit /b 1
