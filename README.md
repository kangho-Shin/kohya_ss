# 사무실 PC 전원 관리 — C# / Windows

여러 PC의 IP·MAC을 등록하고 같은 사무실 망에서 켜기·끄기를 실행하는 **VS2022 / .NET 8 / WinForms 관리 프로그램과 Windows 서비스**입니다.

**[C# 프로젝트](office-power/) · [VS2022 솔루션](office-power/OfficePower.sln) · [설치 및 사용 방법](office-power/README.md)**

VS2022에서 솔루션을 열고 `OfficePower.Controller`를 시작 프로젝트로 지정합니다. 배포용 ZIP 생성 방법은 설치 안내에 있습니다. 자동 빌드·자동 릴리스는 사용자의 알림 중단 요청에 따라 수동 실행으로 변경했습니다.

---

# 내 사진 얼굴 교체 — Windows / Python

사진 두 장을 골라 얼굴을 교체하고 PNG로 저장하는 개인용 데스크톱 프로그램입니다. **실행할 컴퓨터: Windows 10/11 64비트, RTX 3080.** 저장소 이름은 `kohya_ss`지만 kohya 학습 도구가 아닌 독립 프로그램입니다.

사진 처리는 로컬에서 수행하며 사진을 서버에 올리지 않습니다. 인터넷은 패키지와 모델을 설치할 때 필요합니다. 기본 얼굴 교체에 Stable Diffusion은 필요하지 않습니다. SD는 선택적인 경계 보정 기능입니다.

## 처음 설치할 때

1. [Python 3.11 Windows installer (64-bit)](https://www.python.org/downloads/release/python-3119/)를 설치합니다. **Python Launcher**와 **Tcl/Tk**를 포함한 기본 설치를 사용하세요.
2. GitHub의 **Code → Download ZIP**으로 내려받고 압축을 풉니다. ZIP 안에서 직접 실행하지 마세요. 예: `C:\Faceswap`.
3. **`install.bat`**를 더블클릭해 기본 패키지를 설치합니다.
4. **`install-gpu.bat`**를 실행합니다. RTX 3080의 최신 NVIDIA 드라이버가 필요하며 CUDA/cuDNN 사용자 런타임은 pip 패키지로 설치합니다. 별도 CUDA Toolkit 전체 설치는 요구하지 않습니다.
5. **`prepare-models.bat`**를 실행합니다. 공식 모델 이용 조건을 확인하고 Y를 누르면 약 850 MB를 다운로드하고 SHA-256을 확인합니다.
6. **`doctor-gpu.bat`**를 실행합니다. 세 모델이 실제로 `CUDAExecutionProvider`로 로딩되는지 확인합니다.
7. **`run.bat`**를 더블클릭합니다.

오류가 나오면 해당 오류를 해결한 뒤 다음 단계로 진행하세요. 설치 스크립트의 영어 안내는 Windows 콘솔 인코딩 호환성을 위한 것입니다. 프로그램 화면은 한국어입니다.

모델과 가상환경은 GitHub에 포함되지 않습니다. 다른 컴퓨터에 복사할 때 `.venv`를 재사용하지 말고 위 절차를 실행하세요. 설치 이후에는 `run.bat`만 실행하면 됩니다. 현재 제공 형식은 **Python + 배치 파일**이며 검증된 EXE는 아직 제공하지 않습니다.

## 사용하는 방법

1. **가져올 얼굴 사진**에서 얼굴의 원본 사진을 선택합니다.
2. **얼굴을 바꿀 대상 사진**에서 배경·몸·구도를 유지할 사진을 선택합니다.
3. 여러 얼굴이 있으면 빨간 번호를 보고 각각 교체할 얼굴을 선택합니다. 번호는 왼쪽부터 부여됩니다.
4. **선택한 얼굴 교체**를 누르고 결과를 확인합니다.
5. **현재 결과 저장 (PNG)**으로 저장합니다. 입력 사진을 덮어쓰는 저장은 차단합니다.

정면에 가깝고 얼굴이 선명한 사진이 유리합니다. 기본 INSwapper의 얼굴 생성 해상도는 128×128이어서 큰 얼굴이나 가려진 얼굴에는 한계가 있습니다. SD 보정으로 이 해상도 한계가 사라지는 것은 아닙니다.

## 선택: Stable Diffusion 경계 보정

기본 얼굴 교체가 먼저 동작하는지 확인하세요. 별도의 AUTOMATIC1111·Forge·ComfyUI 설치는 필요 없습니다.

1. **`install-sd.bat`**: CUDA 12.6용 PyTorch와 Diffusers를 설치합니다.
2. **`prepare-sd.bat`**: 모델 카드의 이용 조건을 확인한 뒤 SD 1.5 인페인팅 모델을 내려받습니다. 여러 GB의 추가 저장 공간이 필요합니다.
3. 얼굴을 교체한 다음 **선택: SD 경계 보정**을 누릅니다.

얼굴 중심을 보호하고 경계 주변만 낮은 강도로 보정합니다. 항상 개선되는 것은 아니므로 비교해서 저장하세요. **보정 전 교체 결과 저장**으로 기본 결과를 따로 저장할 수 있습니다. 보정 실패 시에도 기본 결과를 유지합니다. 보정 때는 교체 모델을 해제하고 SD 모델을 순차 로딩해 GPU 메모리를 줄입니다.

모델: [stable-diffusion-v1-5/stable-diffusion-inpainting](https://huggingface.co/stable-diffusion-v1-5/stable-diffusion-inpainting). `models/sd-inpainting`에 저장하며 실행할 때는 로컬 파일만 사용합니다.

## 문제 해결

- **Python 3.11 is required**: Python 3.11 64비트와 Launcher를 설치합니다.
- **CUDA 초기화 실패**: NVIDIA 드라이버를 확인하고 `install-gpu.bat`, `doctor-gpu.bat` 순서로 실행합니다. 장치를 `cpu`로 바꾸면 기본 교체를 CPU에서 사용할 수 있습니다. SD 보정에는 CUDA가 필요합니다.
- **CPU/GPU 런타임 중복 설치**: `install-gpu.bat`가 두 런타임을 제거한 뒤 GPU 런타임을 다시 설치합니다. `install.bat`를 재실행했다면 GPU 설치도 다시 실행하세요.
- **DLL / Visual C++ runtime 오류**: [Microsoft 공식 Visual C++ x64 재배포 패키지](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)를 설치합니다.
- **메모리 부족**: 다른 GPU 프로그램을 종료합니다. 기본 교체는 CPU를 선택할 수 있습니다.
- **모델 누락 / 체크섬 오류**: `prepare-models.bat`를 다시 실행합니다. 검증을 끄거나 체크섬을 임의로 변경하지 마세요.
- **모델 다운로드 실패**: `github.com`, `release-assets.githubusercontent.com` 접근이 필요합니다. SD는 `huggingface.co`와 Hugging Face 모델 CDN 접근도 필요합니다.
- **얼굴 없음**: 얼굴이 크고 선명한 사진을 사용하세요.

## 모델 출처와 이용 조건

InsightFace 코드는 MIT이지만 **공식 사전 학습 모델은 비상업 연구용**입니다. 개인 사용이라고 모든 이용이 자동 허용되는 것은 아니므로 모델 조건을 확인하세요. 모델 자체를 이 저장소에서 재배포하지 않습니다.

- [공식 INSwapper 설명](https://github.com/deepinsight/insightface/tree/master/examples/in_swapper)
- [공식 모델 배포](https://github.com/deepinsight/insightface/releases/tag/model-zoo)
- [SHA-256의 공식 출처](https://github.com/deepinsight/insightface/releases/expanded_assets/model-zoo)
- SD 모델은 해당 모델 카드의 CreativeML OpenRAIL-M 조건을 따릅니다.

## 개발 및 검증

Windows에서는 `.venv\Scripts\python.exe`를 사용합니다. Linux 개발 환경의 CPU 테스트:

```bash
python -m venv .venv
.venv/bin/python -m pip install -r requirements-dev.txt
.venv/bin/python -m pytest -q
.venv/bin/python -m faceswap_app.models --accept-model-license
.venv/bin/python -m scripts.doctor --load
.venv/bin/python -m scripts.smoke --source source.jpg --target target.jpg --output result.png
```

CLI 얼굴 번호는 0부터 시작하며 `--source-index`와 `--target-index`로 선택합니다. GUI 번호는 1부터 표시됩니다. Linux GUI에는 Tk와 디스플레이가 필요합니다.

클라우드에서는 공식 모델을 로딩하고 CPU 얼굴 탐지·교체·PNG 출력을 검증했습니다. **Windows 배치 실행, RTX 3080 CUDA 실행, 실제 SD 보정 화질은 대상 Windows PC에서 추가 검증해야 합니다.**
