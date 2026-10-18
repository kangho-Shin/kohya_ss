from pathlib import Path
import subprocess
import sys


def test_doctor_reports_missing_models_with_failure(tmp_path):
    result = subprocess.run([sys.executable, '-m', 'scripts.doctor', '--model-dir', str(tmp_path)],
                            capture_output=True, text=True)
    assert result.returncode == 1
    assert 'inswapper_128.onnx' in result.stdout


def test_smoke_rejects_missing_inputs(tmp_path):
    result = subprocess.run([sys.executable, '-m', 'scripts.smoke', '--source', str(tmp_path/'absent.png'),
                             '--target', str(tmp_path/'absent.png'), '--output', str(tmp_path/'result.png')],
                            capture_output=True, text=True)
    assert result.returncode == 1
    assert not (tmp_path/'result.png').exists()
    assert '이미지' in result.stderr
