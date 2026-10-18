import numpy as np
import pytest

from faceswap_app.refine import make_region, merge_region


def test_edge_bbox_clips_and_keeps_face_center_unmasked():
    box, mask = make_region((100, 100, 3), (-10, -5, 30, 35))
    x1, y1, x2, y2 = box
    assert x1 == y1 == 0
    assert mask.size == (x2 - x1, y2 - y1)
    assert mask.getpixel((10, 10)) == 0
    assert np.asarray(mask).max() > 0


@pytest.mark.parametrize('bbox', [(0, 0, 0, 0), (200, 200, 220, 220), (0, 0, 1, 1)])
def test_invalid_or_tiny_face(bbox):
    with pytest.raises(ValueError):
        make_region((100, 100, 3), bbox)


def test_merge_preserves_every_pixel_outside_mask():
    base = np.zeros((100, 100, 3), dtype=np.uint8)
    region, mask = make_region(base.shape, (25, 25, 75, 75))
    x1, y1, x2, y2 = region
    changed = np.full((y2-y1, x2-x1, 3), 255, dtype=np.uint8)
    result = merge_region(base, changed, region, mask)
    assert np.all(result[:y1] == base[:y1])
    assert np.all(result[y2:] == base[y2:])
    crop = result[y1:y2, x1:x2]
    assert np.all(crop[np.asarray(mask) == 0] == 0)
    assert result.max() > 0
