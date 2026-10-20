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
$bundle = Join-Path $OutputRoot 'OfficePower-win-x64'
New-Item -ItemType Directory -Path $bundle -Force | Out-Null
foreach ($part in @('Agent', 'Controller', 'AgentSetup')) {
    $csproj = Join-Path $project "src\OfficePower.$part\OfficePower.$part.csproj"
    & dotnet publish $csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:EnableWindowsTargeting=true -o (Join-Path $bundle $part)
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $part" }
}
foreach ($file in @('Install-Agent.ps1', 'Uninstall-Agent.ps1', 'Install-Agent.cmd', 'Start-Controller.cmd')) {
    Copy-Item -LiteralPath (Join-Path $scriptDirectory $file) -Destination $bundle -Force
}
Copy-Item -LiteralPath (Join-Path $project 'README.md') -Destination $bundle -Force
Copy-Item -LiteralPath (Join-Path $bundle 'AgentSetup\AgentSetup.exe') -Destination (Join-Path $bundle 'AgentSetup.exe') -Force
Remove-Item -LiteralPath (Join-Path $bundle 'AgentSetup') -Recurse -Force
Write-Host "Windows folder: $bundle"
Write-Host 'Target PC: copy AgentSetup.exe only, double-click, then choose Install / Update.'
Write-Host 'Controller PC: use the Controller folder.'
