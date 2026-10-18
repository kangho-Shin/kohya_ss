import argparse

from faceswap_app.models import DEFAULT_MODELS

MODEL_ID = 'stable-diffusion-v1-5/stable-diffusion-inpainting'


def main():
    parser = argparse.ArgumentParser(description='선택적 SD 1.5 인페인팅 모델 다운로드 (여러 GB)')
    parser.add_argument('--accept-model-license', action='store_true')
    args = parser.parse_args()
    if not args.accept_model_license:
        parser.error('모델 카드의 CreativeML OpenRAIL-M 이용 조건 확인 후 --accept-model-license를 지정하세요.')
    try:
        from huggingface_hub import snapshot_download
        print(f'https://huggingface.co/{MODEL_ID} 에서 여러 GB 다운로드', flush=True)
        snapshot_download(
            MODEL_ID, local_dir=DEFAULT_MODELS/'sd-inpainting',
            allow_patterns=['model_index.json', 'scheduler/*', 'tokenizer/*',
                            'feature_extractor/*', '*/config.json', '*/*.safetensors'],
            ignore_patterns=['*.fp16.*'],
        )
    except Exception as exc:
        parser.exit(1, f'SD 모델 다운로드 실패: {exc}\n')
    print('SD 모델 준비 완료')


if __name__ == '__main__':
    main()
