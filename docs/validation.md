# 검증 결과와 남은 범위

2026-10-18, Linux 클라우드 Python 3.12.14. 실제 실행 대상은 별도 Windows RTX 3080 PC이다.

## 확인한 항목

- 전체 pytest: 28 passed.
- 공식 Buffalo L·INSwapper 다운로드와 공식 SHA-256 검증, 재실행 시 기존 모델 재사용.
- 실제 CPU ONNX 세션 로딩. InsightFace에 포함된 t1 샘플에서 얼굴 6개 탐지, 0번→1번 교체, 1280×886 PNG 생성 및 실제 픽셀 변화 확인.
- `python -m scripts.doctor --load`, `python -m scripts.smoke` 실제 실행 성공.
- 서명 검증된 Debian 패키지에서 추출한 Xvfb로 GUI 열기·닫기 확인.
- 실제 GUI 사진 선택, 얼굴 탐지·선택, 교체, 결과 미리보기와 PNG 저장, 잘못된 입력 이후 결과와 저장 버튼 무효화 확인.
- Windows 64비트 Python 3.11용 기본 및 ONNX GPU 패키지 binary-only 의존성 해결 성공. 실제 Windows 실행과는 구분한다.
- 선택적 SD 패키지 설치 및 StableDiffusionInpaintPipeline import, torch 2.7.1+cpu 확인. `pip check` 통과.
- 별도 리뷰어의 전체 변경 검토. ORT의 실행 중 CUDA 실패 후 자동 CPU 재시도를 차단하고 회귀 테스트 RED→GREEN 확인.

## 아직 확인하지 못한 항목

- Windows 배치 파일 실행, Windows GUI 및 RTX 3080 CUDA 모델 실행.
- 실제 SD 모델 다운로드 및 GPU 보정 결과. 클라우드의 Hugging Face 요청은 네트워크 프록시에서 403으로 차단됐다. 기본 얼굴 교체는 이 다운로드에 의존하지 않는다.
- Windows SD 설치는 대상 PC에서 검증해야 한다. Linux pip의 교차 플랫폼 dry-run은 Linux 환경 마커의 NCCL 의존성을 선택하므로 Windows 설치 성공·실패의 근거로 사용하지 않았다.
- EXE 패키징과 새 클라우드 작업으로의 스냅샷 복원.

## 알려진 작은 제한

SD 준비 상태 진단은 `model_index.json` 존재 여부를 확인한다. 다운로드가 중단되면 진단에서 파일이 있다고 표시될 수 있다. 실제 로더는 누락 파일에서 실패하며 기본 교체 결과는 유지한다. `prepare-sd.bat`를 다시 실행해 다운로드를 완료해야 한다.

## 구현 중 결정

- 기존 격리된 클라우드 체크아웃을 사용했다. 별도 worktree는 만들지 않았다.
- GitHub API 접근이 막혀 공식 GitHub 배포 페이지에 게시된 SHA-256을 고정했다. 공식 배포 파일 변경 시 검토와 명시적 업데이트가 필요하다.
- Linux 개발 환경의 SD import 검증에는 CPU PyTorch를 사용했다. Windows 설치 파일은 CUDA 12.6용 PyTorch를 사용하므로 실제 GPU 확인이 필요하다.
- GPU가 없는 클라우드에서 SD 보정 품질을 검증했다고 주장하지 않는다. 실행 중 모델을 순차 로딩해 RTX 3080 메모리 사용을 줄이는 설계를 유지한다.
