@echo off
setlocal
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" goto fail
".venv\Scripts\python.exe" -m pip install torch==2.7.1 --index-url https://download.pytorch.org/whl/cu126
if errorlevel 1 goto fail
".venv\Scripts\python.exe" -m pip install -r requirements-sd.txt
if errorlevel 1 goto fail
".venv\Scripts\python.exe" -c "import torch; from diffusers import StableDiffusionInpaintPipeline; print(torch.__version__); assert torch.cuda.is_available(), 'CUDA unavailable'"
if errorlevel 1 goto fail
echo SD packages installed. Run prepare-sd.bat next.
pause
exit /b 0
:fail
echo SD installation failed. Read the error above and check the NVIDIA driver.
pause
exit /b 1
