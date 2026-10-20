# Offline regression checks. Never reads or changes the host network/services.
$ErrorActionPreference = 'Stop'
foreach ($role in @('Agent', 'PowerMaster')) {
    $source = Join-Path $PSScriptRoot ("..\field-tools\$role\$role-Tools.ps1")
    $tokens = $null; $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($source, [ref]$tokens, [ref]$errors)
    if ($errors.Count -gt 0) { throw 'Field tools PowerShell parse failed.' }
    $functions = $ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $false)
    if ($role -eq 'PowerMaster' -and ($functions.Name -contains 'Set-TrustedNetwork' -or $functions.Name -contains 'Start-AgentService')) {
        throw 'PowerMaster tools must not contain Agent mutations.'
    }
    if ($role -eq 'PowerMaster' -and (Get-Content -Raw $source) -match 'RunAs') { throw 'PowerMaster tools must not elevate.' }
    if ($role -eq 'Agent' -and $functions.Name -contains 'Test-RemoteConnection') { throw 'Agent tools must not contain the management-PC probe.' }
    foreach ($definition in $functions) { . ([scriptblock]::Create($definition.Extent.Text)) }
}
$script:answers = [System.Collections.Generic.Queue[string]]::new()
$script:changes = [System.Collections.Generic.List[int]]::new()
$script:profileKind = 'Public'
function Read-Host { param($Prompt) return $script:answers.Dequeue() }
function Get-NetConnectionProfile {
    param($InterfaceIndex, $ErrorAction)
    [pscustomobject]@{InterfaceIndex=12; InterfaceAlias='Test LAN'; Name='Test'; NetworkCategory=$script:profileKind}
}
function Set-NetConnectionProfile {
    param($InterfaceIndex, $NetworkCategory, $ErrorAction)
    if ($NetworkCategory -ne 'Private') { throw 'Unexpected category.' }
    $script:changes.Add($InterfaceIndex)
}
function Test-NetConnection { param($ComputerName,$Port) throw 'Invalid IP must not reach the network.' }
$script:answers.Enqueue('not-an-ip')
$rejected = $false
try { Test-RemoteConnection } catch { $rejected = $true }
if (-not $rejected) { throw 'Invalid IP was not rejected.' }
$script:answers.Enqueue('12'); $script:answers.Enqueue('')
Set-TrustedNetwork | Out-Null
if ($script:changes.Count -ne 0) { throw 'Cancelled action changed the network.' }
$script:answers.Enqueue('12'); $script:answers.Enqueue('YES')
Set-TrustedNetwork | Out-Null
if ($script:changes.Count -ne 1 -or $script:changes[0] -ne 12) { throw 'Must change exactly the selected interface.' }
$script:profileKind = 'DomainAuthenticated'
$script:answers.Enqueue('12')
Set-TrustedNetwork | Out-Null
if ($script:changes.Count -ne 1) { throw 'Domain connection was changed.' }
$script:answers.Enqueue('invalid')
$rejected = $false
try { Set-TrustedNetwork | Out-Null } catch { $rejected = $true }
if (-not $rejected -or $script:changes.Count -ne 1) { throw 'Invalid interface selection was accepted.' }
Write-Host 'Field tools passed: invalid IP/index rejected, cancel preserved state, exactly one selected Public interface changed, domain profile preserved. All network calls were mocked.'
