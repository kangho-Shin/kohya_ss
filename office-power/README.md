# 사무실 PC 전원 관리

같은 사무실 LAN에 있는 Windows PC 여러 대를 등록하고 켜거나 끄는 C# 프로젝트입니다.

**WinForms 관리 프로그램**과 Windows 서비스 에이전트를 제공합니다. **Visual Studio 2022 / .NET 8 / C# 12** 프로젝트이며 기존 얼굴 교체 프로그램과 별개입니다.

- 관리자 프로그램: PC 이름·IP·MAC 등록, 선택한 PC 켜기·끄기.
- 대상 PC: 자동 실행 Windows 서비스 설치.
- 켜기: Wake-on-LAN.
- 끄기: PC별 인증키와 인증서 검증을 사용하는 HTTPS 종료 요청.

## Windows에서 설치하기

1. 개발 PC에서 Git Pull 후 아래 `Publish-Windows.ps1`로 배포 폴더를 만듭니다. ZIP은 만들지 않습니다.
2. 대상 PC에는 `artifacts\PowerMaster-win-x64\Agent` **폴더 전체를 복사**합니다. 안의 AgentSetup.exe는 단독 설치도 가능하며 Agent-Tools.cmd는 현장 점검용입니다.
3. 대상 PC에서 **AgentSetup.exe 더블클릭 → 관리자 권한 승인 → 설치 / 업데이트**를 누릅니다. 기존 Agent가 있으면 먼저 삭제하지 마세요. 서비스를 중지하고 업데이트한 뒤 다시 시작하며 기존 인증키·인증서를 유지합니다. 이전 ProgramData 설정도 자동으로 옮깁니다.
4. 설치 화면에 PC 이름·IP·결과가 표시됩니다. **등록 파일 저장**을 눌러 USB 등 원하는 위치에 `컴퓨터이름-pairing.json`을 저장하고 관리 PC로 옮깁니다. 이 파일에는 전원 제어 인증키가 들어 있으므로 안전하게 전달하세요.
5. 관리 PC에는 배포 폴더의 `PowerMaster` 폴더를 복사하고 **PowerMaster.exe**를 실행합니다. 관리자 권한은 필요 없습니다.
6. **PC 등록 → 인증 파일 가져오기 → 저장**합니다. 여러 랜카드가 있으면 실제 사무실 망의 IP와 MAC인지 확인하세요. 기존에 등록한 PC는 업데이트 후 다시 등록할 필요가 없습니다.
7. PowerMaster를 시작하면 저장된 PC 전체의 연결을 자동 확인합니다. 최대 20대씩 동시에 확인하고 응답한 PC부터 상태를 표시합니다. **연결 확인 취소** 또는 창 닫기로 검사를 중단할 수 있으며, 이후 PC를 체크하고 **연결 확인**으로 다시 검사할 수 있습니다. 자동 확인은 PC 선택 체크를 변경하지 않으며 전원 요청을 보내지 않습니다.

대상 PC를 제어 대상에서 빼려면 **AgentSetup.exe → 서비스 제거**를 누릅니다. 이 작업은 서비스와 방화벽 규칙을 제거하며, 재설치 시 기존 등록을 유지할 수 있도록 프로그램 파일·인증 정보는 보존합니다. PowerMaster 목록에서 삭제하는 것과 서비스 제거는 별개입니다. 설치 프로그램은 다른 PC를 원격으로 제거하지 않으며 해당 대상 PC에서 실행해야 합니다.

배포 EXE에는 .NET 런타임이 들어 있으므로 실행 PC에 SDK/.NET을 별도 설치할 필요가 없습니다. Windows 10/11 x64를 지원합니다. 설치 중 오류는 설치 창에 표시됩니다. 기존 PowerShell/CMD 설치 파일도 호환용으로 남아 있습니다.

## 전원 켜기: 최초 하드웨어 설정

각 대상 PC에서 다음을 확인합니다. 프로그램 설치만으로 BIOS 설정이 바뀌지는 않습니다.

- 유선 랜선을 연결하고 PC의 전원 케이블과 멀티탭 전원을 유지합니다.
- BIOS/UEFI에서 **Wake-on-LAN / Power On by PCI-E / Resume by LAN**을 켭니다. 이름은 제조사별로 다릅니다.
- 장치 관리자 → 유선 랜카드 → 고급에서 **Wake on Magic Packet**을 켭니다.
- 랜카드 전원 관리에서 이 장치가 컴퓨터의 대기 모드를 해제하도록 허용하고 Magic Packet만 허용합니다. 완전 종료 상태의 지원 여부는 랜카드·BIOS 설정에 따라 다릅니다.
- Windows **빠른 시작**이 WOL을 방해하면 끄고 시험합니다. 공유기 DHCP 주소 예약 또는 고정 IP를 권장합니다.

**선택 PC 켜기**는 MAC 주소로 UDP 9번 매직 패킷을 보냅니다. 화면의 '켜기 요청 전송'은 부팅 성공이라는 뜻이 아닙니다. 부팅 후 연결 확인으로 서비스 응답을 확인하세요. 기본 브로드캐스트는 `255.255.255.255`이며 환경에 따라 해당 서브넷의 브로드캐스트(예: `192.168.0.255`)로 수정합니다. VLAN·공유기 간 분리나 Wi-Fi의 클라이언트 격리 기능은 WOL을 막을 수 있습니다.

## 전원 끄기

체크한 PC에 **10초 후 종료**를 요청합니다. 이름과 대수 확인창이 나오며 이 관리 PC 자체가 포함되면 다시 확인합니다. 저장되지 않은 앱을 강제로 종료하지 않으므로 먼저 작업을 저장하세요. 종료 예약 성공과 실제 종료 완료는 구분합니다. 서비스 연결 실패를 '전원 꺼짐'으로 단정하지 않습니다.

중복 종료 요청은 2분 동안 거부됩니다. Windows에서 종료를 취소하거나 앱 때문에 종료가 중단됐다면 2분 후 다시 요청할 수 있습니다. OS가 여전히 종료를 진행 중이면 새 요청이 실패할 수 있습니다.

## 인증·방화벽·저장 위치

- PC마다 무작위 256비트 인증키와 자체 서명 TLS 인증서를 생성합니다. 관리 앱은 가져온 인증서의 SHA-256 지문을 고정해서 검증하며 다른 인증서와 잘못된 인증키를 거부합니다.
- 대상 서비스는 `OfficePowerAgent` 이름으로 자동 시작하며 LocalSystem 계정으로 실행합니다. 임의 명령·파일 전송·화면 제어 기능은 없습니다.
- **HTTPS TCP 18765**, 방화벽 **Private/Domain 프로필 + LocalSubnet**만 허용합니다. 사무실 네트워크 프로필이 공용이면 회사 정책에 맞게 관리자에게 확인하세요. 인터넷에 포트를 개방하지 마세요.
- 대상 PC 설치 위치: `C:\Program Files\OfficePowerAgent`. `컴퓨터이름-pairing.json`과 인증 자료는 설치된 `OfficePower.Agent.exe`와 같은 폴더에 저장합니다 (SYSTEM·Administrators 전용).
- 관리 PC 등록 목록: PowerMaster 실행 파일 옆의 `RemoteData\computers.json`. 폴더는 처음 저장할 때 생성됩니다. 기존 AppData 파일은 자동 이전하지 않으므로 PowerMaster를 종료하고 직접 복사하세요. 인증키는 Windows 사용자 DPAPI로 암호화되므로 같은 PC·같은 Windows 계정에서 사용해야 합니다.
- 인증 파일은 안전하게 전달하고 관리 앱에 등록한 뒤 전달용 사본을 삭제하거나 보호해 보관하세요. 인증서는 5년 유효하며 교체 시 인증 파일을 다시 가져와야 합니다.
- 수정된 Agent를 다시 빌드·설치해야 새 저장 위치가 적용됩니다. 이전 버전의 ProgramData 인증 자료는 인증키·인증서를 유지해 실행 파일 폴더로 복사하며 원본도 보존합니다. 다시 설치하면 기존 인증키를 유지합니다. 제거는 `AgentSetup.exe`의 **서비스 제거**를 누르며 서비스·방화벽만 제거하고 파일과 인증 자료는 보존합니다.

### 설치 중 Join-Path의 Path가 비었다는 오류

이전 설치 파일이 매개 변수 기본값에서 스크립트 폴더를 찾지 못한 경우입니다. 최신 `Install-Agent.ps1`과 `Install-Agent.cmd`를 함께 복사한 뒤 관리자 권한으로 실행하세요. 이미 받은 파일로 진행하려면 관리자 PowerShell에서 Agent 폴더를 명시합니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "C:\PowerAgent\Install-Agent.ps1" -AgentSource "C:\PowerAgent\Agent"
```

`C:\PowerAgent\Agent\OfficePower.Agent.exe`가 있어야 하며, 실제 폴더가 다르면 `-AgentSource`를 그 폴더로 바꾸세요. 스크립트 본문을 콘솔에 붙여넣지 말고 저장된 `.ps1` 파일을 실행합니다.

## 소스에서 빌드하기

**Visual Studio 2022 17.11 이상**에서 `OfficePower.sln`을 여세요. Visual Studio Installer의 **.NET 데스크톱 개발**과 **ASP.NET 및 웹 개발** 워크로드를 설치하세요. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)도 필요하며 `global.json`은 SDK 8.0.426 이상 같은 패치 계열을 사용합니다.

솔루션 탐색기에서 **OfficePower.PowerMaster → 시작 프로젝트로 설정 → F5**로 관리 화면을 실행합니다. 에이전트는 서비스용이므로 대상 PC에서 AgentSetup.exe로 설치합니다. 테스트 탐색기에서 OfficePower.Tests를 실행할 수 있으며 실제 종료는 테스트하지 않습니다.

**폼 디자인 수정:** `MainForm.cs`, `RegistrationForm.cs` 또는 `SetupForm.cs`를 우클릭하고 **디자이너 보기(Shift+F7)**를 선택하세요. UI 배치는 각각 `MainForm.Designer.cs`, `RegistrationForm.Designer.cs`에 분리했습니다. 버튼 이벤트와 처리 로직은 기본 `.cs` 파일에 있습니다.

터미널에서 빌드할 때는 이 폴더에서 실행합니다.

```powershell
dotnet test tests/OfficePower.Tests/OfficePower.Tests.csproj -c Release
dotnet build OfficePower.sln -c Release
.\scripts\Publish-Windows.ps1
```

생성 위치는 `artifacts\PowerMaster-win-x64`입니다. 대상 PC에는 그 안의 `Agent` 폴더를 복사하고, 관리 PC에는 `PowerMaster` 폴더를 복사하세요. VS 일반 빌드는 런타임을 포함한 배포본과 다르므로 배포는 위 스크립트를 사용하세요. AgentSetup 프로젝트 빌드는 최신 Agent를 내부에서 게시해 포함하므로 시간이 조금 더 걸립니다.

**알림 정책:** GitHub Actions의 push/PR/태그 자동 실행과 자동 릴리스 게시를 껐습니다. Actions는 사용자가 직접 수동 실행할 때만 동작합니다. 계정 자체의 이메일 알림 설정은 변경하지 않았으며, 코드에서 이메일을 발송하지 않습니다.

## 검증 범위

클라우드에서 실제 UDP 패킷 수신과 실제 Kestrel HTTPS 통신을 테스트합니다. 인증 성공·잘못된 키·다른 인증서 거부·중복 요청·시간 초과를 포함하며 테스트에서는 실제 PC 종료를 호출하지 않습니다. Windows 타깃 빌드와 self-contained EXE 게시를 수행합니다.

**실제 사무실 PC의 BIOS/WOL, Windows 서비스 설치·방화벽·DPAPI, 실제 종료는 해당 PC에서 확인해야 합니다.** Linux에서 Windows EXE를 생성했다는 사실만으로 실제 Windows 실행이 확인된 것은 아닙니다.

[설계와 검증 범위](docs/design.md) · [구현 계획](docs/implementation-plan.md)

상위 폴더의 얼굴 교체 프로그램과는 별도 프로젝트입니다.

인증 파일 이름에는 생성한 PC의 컴퓨터 이름이 붙습니다. 예: `OFFICE-PC01-pairing.json`, `OFFICE-PC02-pairing.json`. 파일 내부의 PC 이름·IP·MAC도 등록 시 표시합니다. 이전 `pairing.json`은 재설치 시 새 이름으로 다시 생성하며 인증키는 유지합니다.

아이콘: PowerMaster는 파란색, Agent·AgentSetup은 초록색 PC 전원 아이콘을 사용합니다. 창과 작업 표시줄 아이콘도 EXE 내부 리소스를 사용하므로 별도 ICO 파일을 배포할 필요가 없습니다. 원본은 `assets`에 있으며 수정 시 `scripts/Generate-Icons.py`(Pillow 필요)로 다시 생성할 수 있습니다.

사무실·현장 설치 및 장애 대응 TXT: [OfficePower-Field-Guide-KO.txt](OfficePower-Field-Guide-KO.txt). 배포 폴더에도 함께 복사됩니다.

현장 점검 도구는 역할별로 나뉩니다. 대상 PC는 `Agent\Agent-Tools.cmd`에서 로컬 Agent 진단·Public 연결 변경·서비스 시작·WOL 설정을 처리합니다. 관리 PC는 `PowerMaster\PowerMaster-Tools.cmd`에서 대상 IP 연결 검사·관리 PC 네트워크 확인·RemoteData 열기를 처리하며 관리자 권한을 요청하지 않습니다. 각 폴더 전체를 복사하세요. 결과는 각 폴더의 `Reports`에 저장됩니다. 개발 PC는 `scripts\Publish-Windows.cmd` 더블클릭으로 배포본을 만듭니다.

이전 Controller에서 옮길 때 두 프로그램을 종료하고 기존 `RemoteData` 폴더를 `PowerMaster.exe` 옆으로 직접 복사하세요. 같은 PC·Windows 계정에서 인증 정보를 유지할 수 있습니다. 기존 배포 폴더는 자동 삭제하지 않습니다.
