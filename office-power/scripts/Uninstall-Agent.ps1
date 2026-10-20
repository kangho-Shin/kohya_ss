#Requires -RunAsAdministrator
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$name = 'OfficePowerAgent'
$service = Get-Service -Name $name -ErrorAction SilentlyContinue
if ($service) {
    Stop-Service -Name $name -Force
    $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    & sc.exe delete $name
    if ($LASTEXITCODE -ne 0) { throw 'Service removal failed.' }
}
Get-NetFirewallRule -Name 'OfficePowerAgent-LAN' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
Write-Host 'Service and firewall rule removed.'
Write-Host 'Program files and credentials were preserved. Remove them manually only if no longer needed:'
Write-Host (Join-Path $env:ProgramFiles 'OfficePowerAgent')
Write-Host (Join-Path $env:ProgramData 'OfficePowerAgent')
