[CmdletBinding()]
param([string]$OutputRoot)
$ErrorActionPreference = 'Stop'
$scriptDirectory = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDirectory) -and -not [string]::IsNullOrWhiteSpace($PSCommandPath)) {
    $scriptDirectory = Split-Path -Parent $PSCommandPath
}
if ([string]::IsNullOrWhiteSpace($scriptDirectory) -and -not [string]::IsNullOrWhiteSpace($MyInvocation.MyCommand.Path)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ([string]::IsNullOrWhiteSpace($scriptDirectory)) { throw 'Run the saved Publish-Windows.ps1 file using -File.' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $scriptDirectory '..\artifacts' }
$project = (Resolve-Path (Join-Path $scriptDirectory '..')).Path
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$bundle = Join-Path $OutputRoot 'PowerMaster-win-x64'
New-Item -ItemType Directory -Path $bundle -Force | Out-Null
$agentOutput = Join-Path $bundle 'Agent'
New-Item -ItemType Directory -Path $agentOutput -Force | Out-Null
foreach ($part in @('PowerMaster', 'AgentSetup')) {
    $csproj = Join-Path $project "src\OfficePower.$part\OfficePower.$part.csproj"
    $destination = if ($part -eq 'AgentSetup') { Join-Path $bundle '.build-AgentSetup' } else { Join-Path $bundle 'PowerMaster' }
    & dotnet publish $csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:EnableWindowsTargeting=true -o $destination
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $part" }
}
Copy-Item -LiteralPath (Join-Path $bundle '.build-AgentSetup\AgentSetup.exe') -Destination $agentOutput -Force
Remove-Item -LiteralPath (Join-Path $bundle '.build-AgentSetup') -Recurse -Force
foreach ($role in @('Agent', 'PowerMaster')) {
    $roleOutput = Join-Path $bundle $role
    foreach ($tool in @("$role-Tools.cmd", "$role-Tools.ps1", 'README-KO.txt')) {
        Copy-Item -LiteralPath (Join-Path $project ("field-tools\$role\" + $tool)) -Destination $roleOutput -Force
    }
}
foreach ($file in @('README.md', 'OfficePower-Field-Guide-KO.txt')) {
    Copy-Item -LiteralPath (Join-Path $project $file) -Destination $bundle -Force
}
Copy-Item -LiteralPath (Join-Path $scriptDirectory 'Start-PowerMaster.cmd') -Destination $bundle -Force
Write-Host "Windows folder: $bundle"
Write-Host 'Target PC: copy the Agent folder. Run AgentSetup.exe or Agent-Tools.cmd.'
Write-Host 'Management PC: copy the PowerMaster folder. Run PowerMaster.exe or PowerMaster-Tools.cmd.'
