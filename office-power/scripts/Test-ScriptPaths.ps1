# Cross-platform bootstrap regression checks. No service, ACL or shutdown calls.
$ErrorActionPreference = 'Stop'
$installer = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Install-Agent.ps1') -Raw
# Stop before all installation operations; evaluate the actual parameter/bootstrap code.
$prefix = ($installer -split 'function Set-RestrictedDirectory', 2)[0] -replace '(?m)^#Requires.*\r?\n', ''
$sandbox = Join-Path ([IO.Path]::GetTempPath()) ('office-path-test-' + [Guid]::NewGuid())
$scriptDirectory = Join-Path $sandbox 'folder with spaces'
New-Item -ItemType Directory -Path $scriptDirectory -Force | Out-Null
$previousProgramFiles = $env:ProgramFiles
$previousProgramData = $env:ProgramData
try {
    $env:ProgramFiles = Join-Path $sandbox 'Program Files'
    $env:ProgramData = Join-Path $sandbox 'ProgramData'
    $fixture = Join-Path $scriptDirectory 'Install-Agent.ps1'
    Set-Content -LiteralPath $fixture -Value ($prefix + "`nWrite-Output `$AgentSource") -Encoding UTF8
    $resolved = & $fixture
    $expected = Join-Path $scriptDirectory 'Agent'
    if ($resolved -ne $expected) { throw 'Default agent path did not resolve beside the script.' }
    $explicit = Join-Path $sandbox 'explicit agent folder'
    $resolved = & ([scriptblock]::Create($prefix + "`nWrite-Output `$AgentSource")) -AgentSource $explicit
    if ($resolved -ne $explicit) { throw 'Explicit path failed when script-root metadata was empty.' }
    $caught = $null
    $resolved = $null
    try { $resolved = & ([scriptblock]::Create($prefix + "`nWrite-Output `$AgentSource")) } catch { $caught = $_.Exception.Message }
    if (($null -ne $caught -and $caught -notlike '*-AgentSource*') -or ($null -eq $caught -and [string]::IsNullOrWhiteSpace($resolved))) {
        throw 'Missing script metadata must explain -AgentSource instead of failing in Join-Path.'
    }
    $publisher = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Publish-Windows.ps1') -Raw
    $publisherPrefix = [regex]::Split($publisher, [regex]::Escape('New-Item -ItemType Directory -Path $bundle'), 2)[0]
    $publishFixture = Join-Path $scriptDirectory 'Publish-Windows.ps1'
    Set-Content -LiteralPath $publishFixture -Value ($publisherPrefix + "`nWrite-Output `$OutputRoot") -Encoding UTF8
    $resolved = & $publishFixture
    if ($resolved -ne (Join-Path $sandbox 'artifacts')) { throw 'Publisher default output folder did not resolve relative to its script.' }
    $explicit = Join-Path $sandbox 'explicit output folder'
    if ((& $publishFixture -OutputRoot $explicit) -ne $explicit) { throw 'Publisher ignored explicit output folder.' }
    foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1') {
        $tokens = $null; $errors = $null
        [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors) | Out-Null
        if ($errors.Count -gt 0) { throw ('PowerShell parse error in ' + $file.Name) }
    }
    Write-Host 'Script path checks passed: default file path, empty-root explicit path, root fallback/diagnostic, default/explicit publish output, all scripts parsed.'
} finally {
    $env:ProgramFiles = $previousProgramFiles
    $env:ProgramData = $previousProgramData
    Remove-Item -LiteralPath $sandbox -Recurse -Force
}
