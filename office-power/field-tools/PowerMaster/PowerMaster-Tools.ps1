[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Windows only.' }
function Test-RemoteConnection {
    $address = Read-Host '대상 PC의 IP를 입력하세요'
    $parsed = $null
    if (-not [Net.IPAddress]::TryParse($address, [ref]$parsed) -or $parsed.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) {
        throw '올바른 IPv4 주소를 입력하세요.'
    }
    Test-NetConnection -ComputerName $address -Port 18765 | Format-List
    Write-Host 'TcpTestSucceeded=True면 포트 연결 성공입니다. PowerMaster에서 연결 확인을 누르세요.'
}

function Show-MasterNetwork {
    Write-Host 'PowerMaster 관리 PC의 네트워크 정보입니다.'
    Get-NetConnectionProfile | Format-Table InterfaceIndex, InterfaceAlias, Name, NetworkCategory -AutoSize
    Get-NetIPAddress -AddressFamily IPv4 | Format-Table InterfaceAlias, IPAddress, PrefixLength -AutoSize
    Write-Host ('등록 데이터 위치: ' + (Join-Path $PSScriptRoot 'RemoteData\computers.json'))
    Write-Host '등록 파일 내용이나 인증키는 출력하지 않습니다.'
}

function Open-RemoteData {
    $dataFolder = Join-Path $PSScriptRoot 'RemoteData'
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'PowerMaster.exe')) ) {
        throw '이 도구를 PowerMaster.exe와 같은 폴더에 두고 사용하세요.'
    }
    if (-not (Test-Path -LiteralPath $dataFolder)) { Write-Host 'RemoteData가 없습니다. PowerMaster에서 PC를 처음 저장하면 생성됩니다.'; return }
    Start-Process -FilePath (Join-Path $env:SystemRoot 'explorer.exe') -ArgumentList ('"{0}"' -f $dataFolder)
}

while ($true) {
    Write-Host "`n========== PowerMaster 관리 PC 점검 =========="
    Write-Host ('현재 PC: ' + $env:COMPUTERNAME)
    Write-Host '1. 대상 PC 연결 검사 (IP 입력)'
    Write-Host '2. 관리 PC 네트워크와 데이터 위치 확인'
    Write-Host '3. RemoteData 폴더 열기'
    Write-Host '0. 종료'
    $choice = Read-Host '번호 선택'
    if ($choice -eq '0') { break }
    if ($choice -notin @('1','2','3')) { Write-Host '0~3 중 선택하세요.'; continue }
    $transcribing = $false
    $report = $null
    try {
        # Keep results beside the tools, with a writable fallback selected by the user.
        $reports = Join-Path $PSScriptRoot 'Reports'
        $reportName = 'PowerMaster-' + $env:COMPUTERNAME + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6) + '.txt'
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
            '2' { Show-MasterNetwork }
            '3' { Open-RemoteData }
        }
    } catch { Write-Host ('작업 실패: ' + $_.Exception.Message) }
    finally {
        if ($transcribing) { Stop-Transcript | Out-Null; Write-Host ('결과 TXT: ' + $report) }
    }
    Read-Host 'Enter를 누르면 메뉴로 돌아갑니다' | Out-Null
}
