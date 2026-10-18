@echo off
setlocal
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" goto fail
".venv\Scripts\python.exe" -m pip uninstall -y onnxruntime onnxruntime-gpu
if errorlevel 1 goto fail
".venv\Scripts\python.exe" -m pip install -r requirements-gpu.txt
if errorlevel 1 goto fail
".venv\Scripts\python.exe" -c "import onnxruntime as o; o.preload_dlls(); print(o.get_available_providers()); assert 'CUDAExecutionProvider' in o.get_available_providers()"
if errorlevel 1 goto fail
echo GPU packages installed. Actual model CUDA loading is checked by doctor-gpu.bat.
pause
exit /b 0
:fail
echo GPU installation failed. Run install.bat first and check NVIDIA driver / errors above.
pause
exit /b 1
