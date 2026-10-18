"""Optional local SD inpainting, limited to the face boundary."""
from pathlib import Path
import gc

import numpy as np
from PIL import Image


def make_region(shape, bbox):
    height, width = shape[:2]
    left, top, right, bottom = bbox
    left, top = max(0, left), max(0, top)
    right, bottom = min(width, right), min(height, bottom)
    if right - left < 8 or bottom - top < 8:
        raise ValueError('보정할 얼굴 영역이 너무 작거나 사진 밖에 있습니다.')
    padx, pady = (right - left) * 0.4, (bottom - top) * 0.4
    box = (max(0, int(left-padx)), max(0, int(top-pady)),
           min(width, int(right+padx)), min(height, int(bottom+pady)))
    x1, y1, x2, y2 = box
    yy, xx = np.mgrid[y1:y2, x1:x2]
    radius = np.sqrt(((xx-(left+right)/2)/((right-left)/2))**2 +
                     ((yy-(top+bottom)/2)/((bottom-top)/2))**2)
    ring = np.clip(1 - np.abs(radius-1.0) / 0.3, 0, 1)
    return box, Image.fromarray((ring * 255).astype(np.uint8))


def merge_region(image, refined, box, mask):
    x1, y1, x2, y2 = box
    alpha = np.asarray(mask, dtype=np.float32)[:, :, None] / 255
    result = image.copy()
    result[y1:y2, x1:x2] = np.clip(
        refined.astype(np.float32) * alpha + image[y1:y2, x1:x2] * (1-alpha), 0, 255
    ).astype(np.uint8)
    return result


def refine(image: np.ndarray, bbox: tuple[int, int, int, int], model_dir: Path,
           strength: float = 0.2) -> np.ndarray:
    if not 0.05 <= strength <= 0.4:
        raise ValueError('보정 강도는 0.05~0.4 사이여야 합니다.')
    if not (Path(model_dir) / 'model_index.json').is_file():
        raise FileNotFoundError('SD 모델이 없습니다. prepare-sd.bat를 먼저 실행하세요.')
    try:
        import torch
        from diffusers import StableDiffusionInpaintPipeline
    except ImportError as exc:
        raise RuntimeError('SD 보정 패키지가 없습니다. install-sd.bat를 실행하세요.') from exc
    if not torch.cuda.is_available():
        raise RuntimeError('SD 보정에는 CUDA가 필요합니다. NVIDIA 드라이버와 PyTorch 설치를 확인하세요.')
    box, mask = make_region(image.shape, bbox)
    x1, y1, x2, y2 = box
    rgb = Image.fromarray(image[y1:y2, x1:x2, ::-1])
    pipeline = None
    try:
        pipeline = StableDiffusionInpaintPipeline.from_pretrained(
            str(model_dir), torch_dtype=torch.float16, local_files_only=True,
            use_safetensors=True,
        )
        pipeline.enable_attention_slicing()
        pipeline.enable_vae_slicing()
        pipeline.enable_model_cpu_offload()
        output = pipeline(
            prompt='natural photograph, consistent skin texture and lighting, seamless face boundary',
            negative_prompt='distorted face, artifacts, blurry',
            image=rgb.resize((512, 512)), mask_image=mask.resize((512, 512)),
            strength=strength, num_inference_steps=30, guidance_scale=4.0,
            generator=torch.Generator(device='cpu').manual_seed(42),
        )
        if output.nsfw_content_detected and any(output.nsfw_content_detected):
            raise RuntimeError('보정 모델에서 결과를 반환하지 않았습니다. 원본 교체 결과를 사용하세요.')
        refined = np.asarray(output.images[0].resize(rgb.size))[:, :, ::-1]
        return merge_region(image, refined, box, mask)
    except torch.cuda.OutOfMemoryError as exc:
        raise RuntimeError('SD 보정 중 GPU 메모리가 부족합니다. 다른 GPU 프로그램을 종료하세요.') from exc
    finally:
        if pipeline is not None:
            del pipeline
        gc.collect()
        torch.cuda.empty_cache()
