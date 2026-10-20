#Requires -RunAsAdministrator
[CmdletBinding()]
param([string]$AgentSource)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($AgentSource)) {
    $scriptDirectory = $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($scriptDirectory) -and -not [string]::IsNullOrWhiteSpace($PSCommandPath)) {
        $scriptDirectory = Split-Path -Parent $PSCommandPath
    }
    if ([string]::IsNullOrWhiteSpace($scriptDirectory) -and -not [string]::IsNullOrWhiteSpace($MyInvocation.MyCommand.Path)) {
        $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    }
    if ([string]::IsNullOrWhiteSpace($scriptDirectory)) {
        throw 'Cannot determine the script folder. Run the saved .ps1 file with -File or provide -AgentSource with the full Agent folder path.'
    }
    $AgentSource = Join-Path $scriptDirectory 'Agent'
}
$AgentSource = [IO.Path]::GetFullPath($AgentSource)
$serviceName = 'OfficePowerAgent'
$installDir = Join-Path $env:ProgramFiles 'OfficePowerAgent'
$dataDir = $installDir
$exe = Join-Path $installDir 'OfficePower.Agent.exe'

function Set-RestrictedDirectory([string]$Path) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    $acl = New-Object System.Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    $system = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-18')
    $admins = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-544')
    $acl.SetOwner($admins)
    foreach ($identity in @($system, $admins)) {
        $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
}

if (-not (Test-Path -LiteralPath (Join-Path $AgentSource 'OfficePower.Agent.exe'))) {
    throw 'Agent folder not found. Extract the full Windows release ZIP first.'
}
foreach ($path in @($installDir, $dataDir) | Select-Object -Unique) {
    if ((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Refusing a symbolic-link installation directory.'
    }
    Set-RestrictedDirectory $path
    # Refuse nested junctions before traversing or changing inherited permissions.
    $pending = New-Object 'System.Collections.Generic.Stack[string]'
    $pending.Push($path)
    while ($pending.Count -gt 0) {
        foreach ($item in (Get-ChildItem -LiteralPath ($pending.Pop()) -Force)) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing a nested symbolic link in the installation.' }
            if ($item.PSIsContainer) { $pending.Push($item.FullName) }
        }
    }
}
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    Stop-Service -Name $serviceName -Force
    $existing.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
}
Copy-Item -Path (Join-Path $AgentSource '*') -Destination $installDir -Recurse -Force
# Apply inherited protection to existing files, too, before secrets are read/written.
foreach ($path in @($installDir, $dataDir) | Select-Object -Unique) {
    & icacls.exe $path /setowner '*S-1-5-32-544' /T /Q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not secure file ownership.' }
    if (@(Get-ChildItem -LiteralPath $path -Force).Count -gt 0) {
        & icacls.exe (Join-Path $path '*') /reset /T /Q | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not protect inherited file permissions.' }
    }
}
& $exe --initialize
if ($LASTEXITCODE -ne 0) { throw 'Agent initialization failed. Existing credentials were preserved.' }

if (-not $existing) {
    New-Service -Name $serviceName -BinaryPathName ('"' + $exe + '"') -DisplayName 'Office Power Agent' -StartupType Automatic | Out-Null
} else {
    $serviceConfig = Get-CimInstance Win32_Service -Filter "Name='OfficePowerAgent'"
    $changed = Invoke-CimMethod -InputObject $serviceConfig -MethodName Change -Arguments @{
        PathName = ('"' + $exe + '"'); StartMode = 'Automatic'; StartName = 'LocalSystem'
    }
    if ($changed.ReturnValue -ne 0) { throw 'Service configuration failed.' }
}
$verified = Get-CimInstance Win32_Service -Filter "Name='OfficePowerAgent'"
if ($verified.PathName -cne ('"' + $exe + '"')) { throw 'Service executable path is not safely quoted.' }
Get-NetFirewallRule -Name 'OfficePowerAgent-LAN' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -Name 'OfficePowerAgent-LAN' -DisplayName 'Office Power Agent (LAN HTTPS)' `
    -Direction Inbound -Action Allow -Protocol TCP -LocalPort 18765 -Program $exe `
    -RemoteAddress LocalSubnet -Profile Domain,Private | Out-Null
Start-Service -Name $serviceName
(Get-Service -Name $serviceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
Write-Host 'Installed. Pairing file (contains a secret; transfer securely):'
Write-Host (Join-Path $dataDir ($env:COMPUTERNAME + '-pairing.json'))
Write-Host 'On the controller PC, import this file using PC registration.'
