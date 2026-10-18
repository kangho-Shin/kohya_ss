from types import SimpleNamespace

import numpy as np
import pytest

from faceswap_app.engine import FaceEngine, select_face, sorted_faces, verify_providers


def face(x):
    return SimpleNamespace(bbox=np.array([x, 0, x + 20, 20]))


def test_sorted_faces_left_to_right_and_selection():
    a, b = face(30), face(0)
    ordered = sorted_faces([a, b])
    assert ordered == [b, a]
    assert select_face(ordered, 1) is a


@pytest.mark.parametrize('index', [-1, 2])
def test_invalid_index(index):
    with pytest.raises(ValueError, match='선택'):
        select_face([face(0)], index)


def test_no_face():
    with pytest.raises(ValueError, match='얼굴'):
        select_face([], 0)


def test_cuda_session_fallback_is_not_silent():
    class Model:
        session = SimpleNamespace(get_providers=lambda: ['CPUExecutionProvider'])
    with pytest.raises(RuntimeError, match='CUDA'):
        verify_providers([Model()], 'cuda')
    assert verify_providers([Model()], 'cpu') == 'CPUExecutionProvider'


def test_swap_does_not_mutate_target():
    # Use the engine orchestration with a deterministic stand-in for ONNX inference.
    engine = object.__new__(FaceEngine)
    engine.provider = 'CPUExecutionProvider'
    engine.detect = lambda image: [face(0)]
    def perform(image, target, source, paste_back):
        image[:] = 255
        return image
    engine.swapper = SimpleNamespace(get=perform)
    target = np.zeros((30, 30, 3), dtype=np.uint8)
    result = engine.swap(target, target, 0, 0)
    assert target.max() == 0
    assert result.image.min() == 255
    assert result.bbox == (0, 0, 20, 20)
