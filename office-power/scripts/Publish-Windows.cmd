@echo off
pushd "%~dp0.."
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish-Windows.ps1" %*
set "publish_result=%errorlevel%"
popd
if not "%publish_result%"=="0" (
  echo Publish failed. Close running Office Power programs and read the error above.
  pause
  exit /b 1
)
pause
