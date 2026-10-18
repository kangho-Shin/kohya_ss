from pathlib import Path
import os
import tempfile

import numpy as np
from PIL import Image, ImageOps, UnidentifiedImageError


def load_image(path: Path) -> np.ndarray:
    try:
        with Image.open(path) as image:
            image = ImageOps.exif_transpose(image).convert('RGB')
            return np.asarray(image)[:, :, ::-1].copy()
    except (OSError, UnidentifiedImageError, Image.DecompressionBombError) as exc:
        raise ValueError('이미지를 읽을 수 없습니다. PNG/JPEG 파일을 확인하세요.') from exc


def save_png(image: np.ndarray, destination: Path, originals: tuple[Path, ...]) -> Path:
    destination = Path(destination).with_suffix('.png')
    for original in originals:
        if destination.resolve() == original.resolve() or (
            destination.exists() and original.exists() and destination.samefile(original)
        ):
            raise ValueError('원본 사진은 덮어쓸 수 없습니다. 다른 이름을 선택하세요.')
    fd, name = tempfile.mkstemp(dir=destination.parent, suffix='.png')
    os.close(fd)
    temporary = Path(name)
    try:
        Image.fromarray(image[:, :, ::-1]).save(temporary, format='PNG')
        temporary.replace(destination)
    finally:
        temporary.unlink(missing_ok=True)
    return destination
