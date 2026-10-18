import argparse
from pathlib import Path
import sys

from faceswap_app.engine import FaceEngine
from faceswap_app.images import load_image, save_png
from faceswap_app.models import DEFAULT_MODELS


def main():
    parser = argparse.ArgumentParser(description='GUI 없이 실제 얼굴 교체 실행')
    for flag in ('source', 'target', 'output'):
        parser.add_argument('--'+flag, type=Path, required=True)
    parser.add_argument('--model-dir', type=Path, default=DEFAULT_MODELS)
    parser.add_argument('--device', choices=['cpu', 'cuda'], default='cpu')
    parser.add_argument('--source-index', type=int, default=0, help='왼쪽부터 0 기반 번호')
    parser.add_argument('--target-index', type=int, default=0)
    args = parser.parse_args()
    engine = None
    try:
        source, target = load_image(args.source), load_image(args.target)
        engine = FaceEngine(args.model_dir, args.device)
        result = engine.swap(source, target, args.source_index, args.target_index)
        destination = save_png(result.image, args.output, (args.source, args.target))
        print(f'교체 완료: {destination} / {result.provider}')
        return 0
    except Exception as exc:
        print(f'교체 실패: {exc}', file=sys.stderr)
        return 1
    finally:
        if engine:
            engine.close()


if __name__ == '__main__':
    raise SystemExit(main())
