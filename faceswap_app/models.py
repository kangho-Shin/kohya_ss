"""Explicit model preparation. No download occurs during photo processing."""
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
import argparse
import hashlib
import shutil
import tempfile
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parent.parent
DEFAULT_MODELS = ROOT / 'models'
RELEASE = 'https://github.com/deepinsight/insightface/releases/download/model-zoo/'
# Published by GitHub on the official model-zoo release, checked 2026-10-18:
# https://github.com/deepinsight/insightface/releases/expanded_assets/model-zoo
ASSETS = {
    'buffalo_l.zip': '80ffe37d8a5940d59a7384c201a2a38d4741f2f3c51eef46ebb28218a7b0ca2f',
    'inswapper_128.onnx': 'e4a3f08c753cb72d04e10aa0f7dbe3deebbf39567d4ead6dce08e98aa49e16af',
}
BUFFALO_FILES = ('det_10g.onnx', 'w600k_r50.onnx')


@dataclass(frozen=True)
class ModelPaths:
    root: Path
    swapper: Path
    detector: Path
    recognizer: Path


def require_models(model_dir: Path) -> ModelPaths:
    root = Path(model_dir)
    paths = ModelPaths(root, root / 'inswapper_128.onnx',
                       root / 'buffalo_l' / BUFFALO_FILES[0],
                       root / 'buffalo_l' / BUFFALO_FILES[1])
    missing = [str(p) for p in (paths.swapper, paths.detector, paths.recognizer)
               if not p.is_file() or p.stat().st_size == 0]
    if missing:
        raise FileNotFoundError('모델이 없습니다. prepare-models.bat를 실행하세요.\n' + '\n'.join(missing))
    return paths


def download_file(url: str, destination: Path, sha256: str, *, opener=urllib.request.urlopen):
    if not url.startswith('https://'):
        raise ValueError('HTTPS 모델 주소만 허용합니다.')
    destination.parent.mkdir(parents=True, exist_ok=True)
    digest = hashlib.sha256()
    with tempfile.NamedTemporaryFile(dir=destination.parent, suffix='.part', delete=False) as file:
        temporary = Path(file.name)
        try:
            with opener(url, timeout=60) as response:
                while chunk := response.read(1024 * 1024):
                    file.write(chunk)
                    digest.update(chunk)
            file.close()
            if digest.hexdigest() != sha256:
                raise ValueError('모델 SHA-256 검증 실패. 다운로드 파일을 사용하지 않습니다.')
            temporary.replace(destination)
        finally:
            file.close()
            temporary.unlink(missing_ok=True)


def extract_buffalo(archive: Path, destination: Path):
    with zipfile.ZipFile(archive) as z:
        for info in z.infolist():
            path = PurePosixPath(info.filename)
            if path.is_absolute() or '..' in path.parts or '\\' in info.filename or ':' in info.filename:
                raise ValueError('안전하지 않은 모델 압축 파일 경로입니다.')
        for name in BUFFALO_FILES:
            if name not in z.namelist():
                raise ValueError('Buffalo 모델 구성 파일이 없습니다: ' + name)
        destination.mkdir(parents=True, exist_ok=True)
        for name in BUFFALO_FILES:
            with z.open(name) as source, tempfile.NamedTemporaryFile(
                dir=destination, suffix='.part', delete=False
            ) as target:
                temporary = Path(target.name)
                try:
                    shutil.copyfileobj(source, target)
                    target.close()
                    temporary.replace(destination / name)
                finally:
                    target.close()
                    temporary.unlink(missing_ok=True)


def prepare(model_dir: Path):
    model_dir.mkdir(parents=True, exist_ok=True)
    for name, expected in ASSETS.items():
        target = model_dir / name
        existing = None
        if target.is_file():
            with target.open('rb') as stream:
                existing = hashlib.file_digest(stream, 'sha256').hexdigest()
        if existing != expected:
            print(f'{name}: 공식 모델 다운로드 (총 약 850 MB)', flush=True)
            download_file(RELEASE + name, target, expected)
        if name.endswith('.zip'):
            extract_buffalo(target, model_dir / 'buffalo_l')
    require_models(model_dir)


def main():
    parser = argparse.ArgumentParser(description='공식 InsightFace 모델 준비 (비상업 연구용 모델)')
    parser.add_argument('--model-dir', type=Path, default=DEFAULT_MODELS)
    parser.add_argument('--accept-model-license', action='store_true')
    args = parser.parse_args()
    if not args.accept_model_license:
        parser.error('공식 모델은 비상업 연구용입니다. 이용 조건 확인 후 --accept-model-license를 지정하세요.')
    try:
        prepare(args.model_dir)
    except Exception as exc:
        parser.exit(1, f'모델 준비 실패: {exc}\n')
    print('모델 준비 완료')


if __name__ == '__main__':
    main()
