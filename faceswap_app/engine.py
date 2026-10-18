from dataclasses import dataclass
from pathlib import Path
import gc

import numpy as np

from .models import require_models


def sorted_faces(faces):
    return sorted(faces, key=lambda face: (float(face.bbox[0]), float(face.bbox[1])))


def select_face(faces, index):
    if not faces:
        raise ValueError('얼굴을 찾지 못했습니다. 정면의 선명한 사진을 선택하세요.')
    if index < 0 or index >= len(faces):
        raise ValueError('얼굴 선택 번호가 올바르지 않습니다. 다시 탐지하세요.')
    return faces[index]


def verify_providers(models, device):
    expected = 'CUDAExecutionProvider' if device == 'cuda' else 'CPUExecutionProvider'
    for model in models:
        if expected not in model.session.get_providers():
            raise RuntimeError('CUDA 초기화 실패. NVIDIA 드라이버와 GPU 설치를 확인하거나 CPU를 선택하세요.')
        if device == 'cuda':
            # ORT otherwise catches an execution-provider failure and retries on CPU.
            # Individual unsupported graph nodes may still legitimately use CPU.
            model.session.disable_fallback()
    return expected


@dataclass
class SwapResult:
    image: np.ndarray
    bbox: tuple[int, int, int, int]
    provider: str


class FaceEngine:
    def __init__(self, model_dir: Path, device: str = 'cpu'):
        if device not in ('cpu', 'cuda'):
            raise ValueError('device must be cpu or cuda')
        paths = require_models(model_dir)
        import onnxruntime as ort
        if device == 'cuda':
            if hasattr(ort, 'preload_dlls'):
                ort.preload_dlls()
            if 'CUDAExecutionProvider' not in ort.get_available_providers():
                raise RuntimeError('CUDA 런타임이 없습니다. install-gpu.bat를 실행하거나 CPU를 선택하세요.')
        from insightface import model_zoo
        providers = ['CPUExecutionProvider']
        if device == 'cuda':
            providers.insert(0, 'CUDAExecutionProvider')
        options = ort.SessionOptions()
        options.intra_op_num_threads = 2
        self.detector = model_zoo.get_model(str(paths.detector), providers=providers, sess_options=options)
        self.recognizer = model_zoo.get_model(str(paths.recognizer), providers=providers, sess_options=options)
        self.swapper = model_zoo.get_model(str(paths.swapper), providers=providers, sess_options=options)
        self.provider = verify_providers([self.detector, self.recognizer, self.swapper], device)
        self.detector.prepare(ctx_id=0 if device == 'cuda' else -1, input_size=(640, 640), det_thresh=0.5)
        self.recognizer.prepare(ctx_id=0 if device == 'cuda' else -1)

    def detect(self, image: np.ndarray):
        from insightface.app.common import Face
        boxes, landmarks = self.detector.detect(image, max_num=0, metric='default')
        faces = []
        for index, box in enumerate(boxes):
            face = Face(bbox=box[:4], det_score=box[4], kps=landmarks[index])
            self.recognizer.get(image, face)
            faces.append(face)
        return sorted_faces(faces)

    def swap(self, source: np.ndarray, target: np.ndarray, source_index: int, target_index: int) -> SwapResult:
        source_face = select_face(self.detect(source), source_index)
        target_face = select_face(self.detect(target), target_index)
        try:
            image = self.swapper.get(target.copy(), target_face, source_face, paste_back=True)
        except Exception as exc:
            if 'memory' in str(exc).lower() or 'alloc' in str(exc).lower():
                raise RuntimeError('GPU 메모리가 부족합니다. 다른 GPU 프로그램을 종료하거나 CPU를 선택하세요.') from exc
            raise
        return SwapResult(image, tuple(int(x) for x in target_face.bbox), self.provider)

    def close(self):
        self.detector = self.recognizer = self.swapper = None
        gc.collect()
