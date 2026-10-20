# Safe offline validation: reads resources only; never installs or stops services.
[CmdletBinding()]
param([string]$AssemblyPath)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($AssemblyPath)) {
    $AssemblyPath = Join-Path $PSScriptRoot '..\src\OfficePower.AgentSetup\bin\Release\net8.0-windows\AgentSetup.dll'
}
$assembly = [Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($AssemblyPath))
$expected = @{
    'Install-Agent.ps1' = Join-Path $PSScriptRoot 'Install-Agent.ps1'
    'Uninstall-Agent.ps1' = Join-Path $PSScriptRoot 'Uninstall-Agent.ps1'
    'OfficePower.Agent.exe' = Join-Path $PSScriptRoot '..\src\OfficePower.AgentSetup\obj\embedded-agent\Release\OfficePower.Agent.exe'
}
foreach ($name in $expected.Keys) {
    $stream = $assembly.GetManifestResourceStream($name)
    if ($null -eq $stream) { throw "Missing embedded resource: $name" }
    try {
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $sha.Dispose() }
        $diskHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $expected[$name]).Hash
        if ($hash -cne $diskHash) { throw "Embedded resource differs from current build: $name" }
    } finally { $stream.Dispose() }
}
Write-Host 'Setup payload passed: current Agent executable and both current scripts embedded. No installation executed.'
