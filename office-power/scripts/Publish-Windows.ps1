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
foreach ($part in @('Agent', 'Controller')) {
    $csproj = Join-Path $project "src\OfficePower.$part\OfficePower.$part.csproj"
    & dotnet publish $csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:EnableWindowsTargeting=true -o (Join-Path $bundle $part)
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $part" }
}
foreach ($file in @('Install-Agent.ps1', 'Uninstall-Agent.ps1', 'Install-Agent.cmd', 'Start-Controller.cmd')) {
    Copy-Item -LiteralPath (Join-Path $scriptDirectory $file) -Destination $bundle -Force
}
Copy-Item -LiteralPath (Join-Path $project 'README.md') -Destination $bundle -Force
$zip = Join-Path $OutputRoot 'OfficePower-win-x64.zip'
Compress-Archive -Path (Join-Path $bundle '*') -DestinationPath $zip -Force
Get-FileHash -Algorithm SHA256 -LiteralPath $zip | Format-List
Write-Host "Windows package: $zip"
