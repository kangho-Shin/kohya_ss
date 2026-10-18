@echo off
setlocal
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" goto fail
echo Optional Stable Diffusion inpainting model: several GB.
echo https://huggingface.co/stable-diffusion-v1-5/stable-diffusion-inpainting
choice /c YN /m "Have you read and accepted the model card and license?"
if errorlevel 2 exit /b 1
".venv\Scripts\python.exe" -m scripts.prepare_sd --accept-model-license
if errorlevel 1 goto fail
pause
exit /b 0
:fail
echo SD model download failed. Run install-sd.bat first and check network access.
pause
exit /b 1
