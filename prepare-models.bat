@echo off
setlocal
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" goto fail
echo Official InsightFace models: about 850 MB, non-commercial research license.
echo https://github.com/deepinsight/insightface/tree/master/examples/in_swapper
choice /c YN /m "Have you read and accepted the model terms?"
if errorlevel 2 exit /b 1
".venv\Scripts\python.exe" -m faceswap_app.models --accept-model-license
if errorlevel 1 goto fail
echo Model preparation complete.
pause
exit /b 0
:fail
echo Failed. Run install.bat first and check the error above.
pause
exit /b 1
