[CmdletBinding()]
param([string]$OutputRoot = (Join-Path $PSScriptRoot '..\artifacts'))
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
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
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $bundle -Force
}
Copy-Item -LiteralPath (Join-Path $project 'README.md') -Destination $bundle -Force
$zip = Join-Path $OutputRoot 'OfficePower-win-x64.zip'
Compress-Archive -Path (Join-Path $bundle '*') -DestinationPath $zip -Force
Get-FileHash -Algorithm SHA256 -LiteralPath $zip | Format-List
Write-Host "Windows package: $zip"
