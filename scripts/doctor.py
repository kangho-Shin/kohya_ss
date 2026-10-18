import argparse
import importlib.metadata
from pathlib import Path
import platform
import sys

from faceswap_app.models import DEFAULT_MODELS, require_models


def main():
    parser = argparse.ArgumentParser(description='설치 및 모델 준비 상태 확인')
    parser.add_argument('--model-dir', type=Path, default=DEFAULT_MODELS)
    parser.add_argument('--device', choices=['cpu', 'cuda'], default='cpu')
    parser.add_argument('--load', action='store_true', help='실제 ONNX 세션 초기화도 확인')
    args = parser.parse_args()
    failed = False
    print(f'Python: {platform.python_version()} / {platform.system()}')
    if sys.version_info < (3, 11):
        print('ERROR: Python 3.11 이상 필요 (Windows 권장: 3.11)')
        failed = True
    for name in ('numpy', 'Pillow', 'opencv-python', 'insightface'):
        try:
            print(f'{name}: {importlib.metadata.version(name)}')
        except importlib.metadata.PackageNotFoundError:
            print(f'ERROR: {name} 미설치')
            failed = True
    try:
        import onnxruntime as ort
        if hasattr(ort, 'preload_dlls') and args.device == 'cuda':
            ort.preload_dlls()
        print('ONNX Runtime:', ort.__version__, 'providers:', ', '.join(ort.get_available_providers()))
        if args.device == 'cuda' and 'CUDAExecutionProvider' not in ort.get_available_providers():
            print('ERROR: GPU 런타임 없음. install-gpu.bat를 실행하세요.')
            failed = True
        try:
            importlib.metadata.version('onnxruntime')
            importlib.metadata.version('onnxruntime-gpu')
            print('ERROR: CPU/GPU 런타임 중복 설치. install-gpu.bat로 복구하세요.')
            failed = True
        except importlib.metadata.PackageNotFoundError:
            pass
    except ImportError as exc:
        print('ERROR: ONNX Runtime:', exc)
        failed = True
    try:
        require_models(args.model_dir)
        print('얼굴 교체 모델: 준비됨')
        if args.load:
            from faceswap_app.engine import FaceEngine
            engine = FaceEngine(args.model_dir, args.device)
            print('실제 모델 실행 장치:', engine.provider)
            engine.close()
    except Exception as exc:
        print('ERROR:', exc)
        failed = True
    print('선택적 SD 모델:', '있음' if (args.model_dir/'sd-inpainting'/'model_index.json').exists() else '미설치 (얼굴 교체에는 불필요)')
    return 1 if failed else 0


if __name__ == '__main__':
    raise SystemExit(main())
