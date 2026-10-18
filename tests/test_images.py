from pathlib import Path

import numpy as np
import pytest
from PIL import Image

from faceswap_app.images import load_image, save_png


def test_unicode_path_roundtrip_and_bgr(tmp_path):
    original = tmp_path / '내 사진.png'
    Image.new('RGB', (12, 9), (255, 0, 0)).save(original)
    data = load_image(original)
    assert data.shape == (9, 12, 3)
    assert data[0, 0].tolist() == [0, 0, 255]
    output = save_png(data, tmp_path / '새 결과.png', (original,))
    assert np.array_equal(load_image(output), data)


def test_exif_rotation(tmp_path):
    path = tmp_path / 'rotate.jpg'
    exif = Image.Exif()
    exif[274] = 6
    Image.new('RGB', (12, 9)).save(path, exif=exif)
    assert load_image(path).shape == (12, 9, 3)


def test_original_cannot_be_overwritten(tmp_path):
    path = tmp_path / 'original.png'
    Image.new('RGB', (10, 10), 'red').save(path)
    before = path.read_bytes()
    with pytest.raises(ValueError, match='원본'):
        save_png(load_image(path), path, (path,))
    assert path.read_bytes() == before


def test_hardlink_cannot_overwrite_original(tmp_path):
    path = tmp_path / 'original.png'
    Image.new('RGB', (10, 10)).save(path)
    alias = tmp_path / 'alias.png'
    alias.hardlink_to(path)
    with pytest.raises(ValueError, match='원본'):
        save_png(load_image(path), alias, (path,))


def test_corrupt_image(tmp_path):
    path = tmp_path / 'bad.jpg'
    path.write_text('bad')
    with pytest.raises(ValueError, match='이미지'):
        load_image(path)
