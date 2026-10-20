@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0Install-Agent.ps1" -AgentSource "%~dp0Agent"
if errorlevel 1 (
  echo Installation failed. Read the specific error above. Administrator privileges are required.
  pause
  exit /b 1
)
pause
