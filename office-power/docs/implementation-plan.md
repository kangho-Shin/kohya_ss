# Office Power 구현 계획

사용자가 추가 설계 확인 없이 직접 구현·업로드를 요청했다. 이 요청에 따라 중간 승인 단계 없이 구현한다. 기존 얼굴 교체 프로젝트는 변경하지 않는다.

1. 사용자 요청에 따라 Visual Studio 2022·.NET 8·C# 12와 기본 .sln 형식으로 구성한다. SDK는 공식 SHA-512로 검증하고 Core, Agent, Controller(WinForms), Tests를 포함한다.
2. 테스트를 먼저 작성하고 실패를 확인한 뒤 MAC 검증·WOL UDP 전송, 암호화된 등록 저장, 인증서 고정 HTTPS 클라이언트를 구현한다.
3. 실제 Kestrel HTTPS 통합 테스트에서 정상 인증, 인증 거부, 인증서 불일치, 중복 종료 및 지연·실패를 검증한다. OS 종료는 테스트 대역만 사용한다.
4. Windows 서비스 에이전트에 인증 자료 초기화, 제한된 API, 강제 종료 없는 30초 종료 예약과 서비스 수명 관리를 구현한다.
5. WinForms 목록·PC 등록/편집/삭제·인증 파일 가져오기·선택 PC 켜기/끄기·종료 확인·결과 표시를 구현한다. 폼 디자이너 파일은 분리하고 비밀 값은 DPAPI로 보호한다.
6. 관리자 권한 설치/제거 PowerShell, Windows x64 self-contained 게시, 다운로드 ZIP 및 사용 문서를 준비한다.
7. 전체 테스트·Windows 타깃 빌드·게시 결과를 확인하고 독립 코드 리뷰 후 중요한 결함을 수정한다. 실제 Windows 설치·하드웨어 WOL·종료 여부는 현장 검증으로 명확히 구분한다.
8. GitHub main에 올리고 원격 커밋과 다운로드 경로를 확인한다. 클라우드 환경 게시와 GitHub 업로드를 혼동하지 않는다.

설계: [design.md](design.md). 포트 18765, TLS 인증서 SHA-256 고정, PC별 256비트 인증키, 방화벽 LocalSubnet/Private·Domain, 종료 지연 30초와 force=false를 유지한다.
