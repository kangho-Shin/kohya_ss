[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Windows only.' }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    try {
        $arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $PSCommandPath
        $child = Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -Verb RunAs -ArgumentList $arguments -PassThru -Wait
        exit $child.ExitCode
    } catch { Write-Host '관리자 권한 승인이 필요합니다. 다시 실행해 주세요.'; exit 1 }
}

function Test-RemoteConnection {
    $address = Read-Host '대상 PC의 IP를 입력하세요'
    $parsed = $null
    if (-not [Net.IPAddress]::TryParse($address, [ref]$parsed) -or $parsed.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) {
        throw '올바른 IPv4 주소를 입력하세요.'
    }
    Test-NetConnection -ComputerName $address -Port 18765 | Format-List
    Write-Host 'TcpTestSucceeded=True면 포트 연결 성공입니다. Controller에서 연결 확인을 누르세요.'
}

function Show-TargetDiagnosis {
    Write-Host "`n===== Agent 서비스 ====="
    $service = Get-Service OfficePowerAgent -ErrorAction SilentlyContinue
    if ($service) { $service | Format-Table Status, Name, DisplayName -AutoSize } else { Write-Host '서비스 없음: AgentSetup.exe에서 설치하세요.' }
    Write-Host "`n===== 네트워크 종류 ====="
    Get-NetConnectionProfile | Format-Table InterfaceIndex, InterfaceAlias, Name, NetworkCategory -AutoSize
    Write-Host "`n===== IPv4 ====="
    Get-NetIPAddress -AddressFamily IPv4 | Format-Table InterfaceIndex, InterfaceAlias, IPAddress, PrefixLength -AutoSize
    Write-Host "`n===== 랜카드 MAC / 상태 ====="
    Get-NetAdapter | Format-Table ifIndex, Name, Status, MacAddress -AutoSize
    Write-Host "`n===== TCP 18765 대기 포트 ====="
    $listeners = Get-NetTCPConnection -LocalPort 18765 -State Listen -ErrorAction SilentlyContinue
    if ($listeners) { $listeners | Format-Table LocalAddress, LocalPort, OwningProcess -AutoSize } else { Write-Host '대기 포트 없음' }
    Write-Host "`n===== Agent 방화벽 ====="
    $rules = Get-NetFirewallRule -Name OfficePowerAgent-LAN -ErrorAction SilentlyContinue
    if ($rules) { $rules | Format-Table Enabled, Profile, Direction, Action -AutoSize } else { Write-Host '규칙 없음' }
    Write-Host "`n===== 등록 파일 위치 (파일 내용은 출력하지 않음) ====="
    $files = Get-ChildItem -LiteralPath (Join-Path $env:ProgramFiles 'OfficePowerAgent') -Filter '*pairing.json' -ErrorAction SilentlyContinue
    if ($files) { $files | Select-Object FullName | Format-List } else { Write-Host '파일 없음: 설치 화면에서 설치 완료 여부를 확인하세요.' }
}

function Set-TrustedNetwork {
    Get-NetConnectionProfile | Format-Table InterfaceIndex, InterfaceAlias, Name, NetworkCategory -AutoSize
    $inputIndex = Read-Host '신뢰하는 현장 유선 LAN의 InterfaceIndex 번호'
    $networkIndex = 0
    if (-not [int]::TryParse($inputIndex, [ref]$networkIndex)) { throw '번호를 입력하세요.' }
    $profiles = @(Get-NetConnectionProfile -InterfaceIndex $networkIndex -ErrorAction SilentlyContinue)
    if ($profiles.Count -ne 1) { throw '해당 연결을 하나로 확인할 수 없습니다. 번호를 확인하세요.' }
    if ($profiles[0].NetworkCategory -ne 'Public') { Write-Host 'Public 연결이 아니므로 변경하지 않았습니다.'; return }
    $profiles | Format-Table InterfaceIndex, InterfaceAlias, Name, NetworkCategory -AutoSize
    Write-Host '위 연결 하나만 Private로 변경합니다. 공용 Wi-Fi나 외부망이면 취소하세요.'
    if ((Read-Host '신뢰하는 사무실/현장 LAN이면 YES 입력, 그 외 Enter로 취소') -cne 'YES') { Write-Host '취소했습니다.'; return }
    Set-NetConnectionProfile -InterfaceIndex $networkIndex -NetworkCategory Private -ErrorAction Stop
    Get-NetConnectionProfile -InterfaceIndex $networkIndex | Format-Table InterfaceIndex, InterfaceAlias, NetworkCategory -AutoSize
    Write-Host '변경 완료. 관리 PC에서 연결 확인을 다시 실행하세요.'
}

function Start-AgentService {
    $service = Get-Service OfficePowerAgent -ErrorAction SilentlyContinue
    if (-not $service) { throw '서비스가 없습니다. AgentSetup.exe에서 설치하세요.' }
    if ($service.Status -eq 'Running') { Write-Host '이미 실행 중입니다.'; return }
    if ((Read-Host '이 PC의 Agent 서비스를 시작하려면 YES 입력') -cne 'YES') { Write-Host '취소했습니다.'; return }
    Start-Service OfficePowerAgent -ErrorAction Stop
    (Get-Service OfficePowerAgent).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
    Get-Service OfficePowerAgent | Format-Table Status, Name -AutoSize
}

while ($true) {
    Write-Host "`n========== Office Power 현장 점검 =========="
    Write-Host ('현재 PC: ' + $env:COMPUTERNAME)
    Write-Host '1. [관리 PC에서] 대상 PC 연결 검사 (IP 입력)'
    Write-Host '2. [대상 PC에서] 서비스/네트워크/방화벽 전체 진단'
    Write-Host '3. [대상 PC에서] 선택한 Public 연결을 Private로 변경'
    Write-Host '4. [대상 PC에서] 중지된 Agent 서비스 시작'
    Write-Host '5. [대상 PC에서] 랜카드 설정 화면 열기 (WOL 설정용)'
    Write-Host '6. [대상 PC에서] 전원 옵션 화면 열기 (빠른 시작 설정용)'
    Write-Host '0. 종료'
    $choice = Read-Host '번호 선택'
    if ($choice -eq '0') { break }
    if ($choice -notin @('1','2','3','4','5','6')) { Write-Host '0~6 중 선택하세요.'; continue }
    $transcribing = $false
    $report = $null
    try {
        # Keep results beside the tools, with a writable fallback selected by the user.
        $reports = Join-Path $PSScriptRoot 'Reports'
        $reportName = 'OfficePower-' + $env:COMPUTERNAME + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6) + '.txt'
        try {
            New-Item -ItemType Directory -Path $reports -Force -ErrorAction Stop | Out-Null
            $report = Join-Path $reports $reportName
            Start-Transcript -LiteralPath $report -ErrorAction Stop | Out-Null
        } catch {
            $reports = Read-Host '결과 저장 불가: 저장할 폴더의 전체 경로를 입력하세요'
            if ([string]::IsNullOrWhiteSpace($reports) -or -not [IO.Path]::IsPathRooted($reports)) { throw '저장할 전체 경로가 필요합니다.' }
            New-Item -ItemType Directory -Path $reports -Force -ErrorAction Stop | Out-Null
            $report = Join-Path $reports $reportName
            Start-Transcript -LiteralPath $report -ErrorAction Stop | Out-Null
        }
        $transcribing = $true
        Write-Host ('점검 메뉴: ' + $choice + ' / PC: ' + $env:COMPUTERNAME)
        switch ($choice) {
            '1' { Test-RemoteConnection }
            '2' { Show-TargetDiagnosis }
            '3' { Set-TrustedNetwork }
            '4' { Start-AgentService }
            '5' { Start-Process (Join-Path $env:SystemRoot 'System32\ncpa.cpl'); Write-Host '유선 랜카드 → 속성 → 구성 → 고급/전원 관리에서 Magic Packet을 확인하세요.' }
            '6' { Start-Process (Join-Path $env:SystemRoot 'System32\control.exe') -ArgumentList '/name Microsoft.PowerOptions'; Write-Host '전원 단추 작동 설정 → 현재 사용할 수 없는 설정 변경 → 빠른 시작 해제.' }
        }
    } catch { Write-Host ('작업 실패: ' + $_.Exception.Message) }
    finally {
        if ($transcribing) { Stop-Transcript | Out-Null; Write-Host ('결과 TXT: ' + $report) }
    }
    Read-Host 'Enter를 누르면 메뉴로 돌아갑니다' | Out-Null
}
