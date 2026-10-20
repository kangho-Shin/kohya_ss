# 사무실 PC 전원 관리

같은 사무실 LAN에 있는 Windows PC 여러 대를 등록하고 켜거나 끄는 C# 프로젝트입니다.

WPF 관리 프로그램과 Windows 서비스 에이전트를 제공합니다. **Visual Studio 2022 / .NET 8 / C# 12** 프로젝트이며 기존 얼굴 교체 프로그램과 별개입니다.

- 관리자 프로그램: PC 이름·IP·MAC 등록, 선택한 PC 켜기·끄기.
- 대상 PC: 자동 실행 Windows 서비스 설치.
- 켜기: Wake-on-LAN.
- 끄기: PC별 인증키와 인증서 검증을 사용하는 HTTPS 종료 요청.

## Windows에서 설치하기

1. [Office Power 릴리스](https://github.com/kangho-Shin/kohya_ss/releases/tag/office-power-v0.1.0)에서 **OfficePower-win-x64.zip**을 받습니다. 릴리스가 아직 빌드 중이면 [Actions → Office Power Windows](https://github.com/kangho-Shin/kohya_ss/actions/workflows/office-power.yml)의 완료 여부를 확인하세요.
2. 내려받은 ZIP의 **속성 → 차단 해제**가 있다면 출처를 확인한 후 해제하고 압축을 풉니다. **Code → Download ZIP은 소스 코드**이므로 실행용 ZIP과 다릅니다.
3. 관리할 대상 PC마다 **Install-Agent.cmd → 우클릭 → 관리자 권한으로 실행**합니다. 현재 PC의 설정을 바꾸므로 대상 PC에서만 실행하세요.
4. 설치가 끝나면 대상 PC의 `C:\ProgramData\OfficePowerAgent\pairing.json`을 관리자 권한으로 복사해 관리 PC로 안전하게 옮깁니다. 이 파일에는 해당 PC를 끌 수 있는 인증키가 있으므로 GitHub·공용 폴더·채팅에 올리지 마세요.
5. 관리 PC에서는 **Start-Controller.cmd** 또는 `Controller\OfficePower.Controller.exe`를 실행합니다. 관리자 권한은 필요 없습니다.
6. **PC 등록 → 인증 파일 가져오기**로 대상 PC의 파일을 선택합니다. 이름·IP·유선 랜카드 MAC 주소를 확인한 뒤 저장합니다. 다중 랜카드/VPN이 있으면 실제 사무실 망의 IP와 유선 MAC으로 수정하세요.
7. 목록의 체크박스를 선택하고 **연결 확인**을 누릅니다. 서비스 정상 연결을 확인한 뒤 켜기·끄기를 사용하세요.

실행용 ZIP에는 .NET 런타임이 들어 있으므로 대상 PC와 관리 PC에 SDK/.NET을 별도 설치할 필요가 없습니다. 지원 대상은 Windows 10/11 x64입니다. 처음에는 PC 한 대만 등록해 시험하세요.

## 전원 켜기: 최초 하드웨어 설정

각 대상 PC에서 다음을 확인합니다. 프로그램 설치만으로 BIOS 설정이 바뀌지는 않습니다.

- 유선 랜선을 연결하고 PC의 전원 케이블과 멀티탭 전원을 유지합니다.
- BIOS/UEFI에서 **Wake-on-LAN / Power On by PCI-E / Resume by LAN**을 켭니다. 이름은 제조사별로 다릅니다.
- 장치 관리자 → 유선 랜카드 → 고급에서 **Wake on Magic Packet**을 켭니다.
- 랜카드 전원 관리에서 이 장치가 컴퓨터의 대기 모드를 해제하도록 허용하고 Magic Packet만 허용합니다. 완전 종료 상태의 지원 여부는 랜카드·BIOS 설정에 따라 다릅니다.
- Windows **빠른 시작**이 WOL을 방해하면 끄고 시험합니다. 공유기 DHCP 주소 예약 또는 고정 IP를 권장합니다.

**선택 PC 켜기**는 MAC 주소로 UDP 9번 매직 패킷을 보냅니다. 화면의 '켜기 요청 전송'은 부팅 성공이라는 뜻이 아닙니다. 부팅 후 연결 확인으로 서비스 응답을 확인하세요. 기본 브로드캐스트는 `255.255.255.255`이며 환경에 따라 해당 서브넷의 브로드캐스트(예: `192.168.0.255`)로 수정합니다. VLAN·공유기 간 분리나 Wi-Fi의 클라이언트 격리 기능은 WOL을 막을 수 있습니다.

## 전원 끄기

체크한 PC에 **30초 후 종료**를 요청합니다. 이름과 대수 확인창이 나오며 이 관리 PC 자체가 포함되면 다시 확인합니다. 저장되지 않은 앱을 강제로 종료하지 않으므로 먼저 작업을 저장하세요. 종료 예약 성공과 실제 종료 완료는 구분합니다. 서비스 연결 실패를 '전원 꺼짐'으로 단정하지 않습니다.

중복 종료 요청은 2분 동안 거부됩니다. Windows에서 종료를 취소하거나 앱 때문에 종료가 중단됐다면 2분 후 다시 요청할 수 있습니다. OS가 여전히 종료를 진행 중이면 새 요청이 실패할 수 있습니다.

## 인증·방화벽·저장 위치

- PC마다 무작위 256비트 인증키와 자체 서명 TLS 인증서를 생성합니다. 관리 앱은 가져온 인증서의 SHA-256 지문을 고정해서 검증하며 다른 인증서와 잘못된 인증키를 거부합니다.
- 대상 서비스는 `OfficePowerAgent` 이름으로 자동 시작하며 LocalSystem 계정으로 실행합니다. 임의 명령·파일 전송·화면 제어 기능은 없습니다.
- **HTTPS TCP 18765**, 방화벽 **Private/Domain 프로필 + LocalSubnet**만 허용합니다. 사무실 네트워크 프로필이 공용이면 회사 정책에 맞게 관리자에게 확인하세요. 인터넷에 포트를 개방하지 마세요.
- 대상 PC 설치 위치: `C:\Program Files\OfficePowerAgent`. 인증 자료: `C:\ProgramData\OfficePowerAgent` (SYSTEM·Administrators 전용).
- 관리 PC 등록 목록: `%LOCALAPPDATA%\OfficePower\computers.json`. 인증키는 Windows 사용자 DPAPI로 암호화되므로 다른 계정·컴퓨터에 이 파일만 복사해서 쓸 수 없습니다.
- 인증 파일은 안전하게 전달하고 관리 앱에 등록한 뒤 전달용 사본을 삭제하거나 보호해 보관하세요. 인증서는 5년 유효하며 교체 시 인증 파일을 다시 가져와야 합니다.
- 다시 설치하면 기존 인증키를 유지합니다. 제거는 관리자 PowerShell에서 `Uninstall-Agent.ps1`을 실행하며 서비스·방화벽만 제거하고 파일과 인증 자료는 보존합니다.

## 소스에서 빌드하기

**Visual Studio 2022 17.11 이상**에서 `OfficePower.sln`을 여세요. Visual Studio Installer의 **.NET 데스크톱 개발**과 **ASP.NET 및 웹 개발** 워크로드를 설치하세요. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)도 필요하며 `global.json`은 SDK 8.0.426 이상 같은 패치 계열을 사용합니다.

솔루션 탐색기에서 **OfficePower.Controller → 시작 프로젝트로 설정 → F5**로 관리 화면을 실행합니다. 에이전트는 서비스용이므로 대상 PC의 설치 스크립트로 설치합니다. 테스트 탐색기에서 OfficePower.Tests를 실행할 수 있으며 실제 종료는 테스트하지 않습니다.

터미널에서 빌드할 때는 이 폴더에서 실행합니다.

```powershell
dotnet test tests/OfficePower.Tests/OfficePower.Tests.csproj -c Release
dotnet build OfficePower.sln -c Release
.\scripts\Publish-Windows.ps1
```

생성 위치는 `artifacts\OfficePower-win-x64.zip`입니다. 소스에서 직접 게시한 경우에도 `Install-Agent.ps1`과 `Agent` 폴더가 같은 폴더에 있어야 합니다. GitHub Actions도 같은 명령으로 Windows에서 테스트·배포 ZIP을 생성합니다.

## 검증 범위

클라우드에서 실제 UDP 패킷 수신과 실제 Kestrel HTTPS 통신을 테스트합니다. 인증 성공·잘못된 키·다른 인증서 거부·중복 요청·시간 초과를 포함하며 테스트에서는 실제 PC 종료를 호출하지 않습니다. Windows 타깃 빌드와 self-contained EXE 게시를 수행합니다.

**실제 사무실 PC의 BIOS/WOL, Windows 서비스 설치·방화벽·DPAPI, 실제 종료는 해당 PC에서 확인해야 합니다.** Linux에서 Windows EXE를 생성했다는 사실만으로 실제 Windows 실행이 확인된 것은 아닙니다.

[설계와 검증 범위](docs/design.md) · [구현 계획](docs/implementation-plan.md)

상위 폴더의 얼굴 교체 프로그램과는 별도 프로젝트입니다.
