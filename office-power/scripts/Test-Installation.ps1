#Requires -RunAsAdministrator
# Isolated Windows CI validation. NEVER sends /shutdown.
$ErrorActionPreference = 'Stop'
$bundle = Join-Path $PSScriptRoot '..\artifacts\OfficePower-win-x64'
$data = Join-Path $env:ProgramData 'OfficePowerAgent'
if (Get-Service OfficePowerAgent -ErrorAction SilentlyContinue) { throw 'This test requires an isolated machine without an existing Office Power service.' }

Add-Type -TypeDefinition @'
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
public static class OfficePowerPinnedProbe {
    public static string Status(string token, string pin) {
        var h = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false };
        h.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => cert != null &&
            String.Equals(Convert.ToHexString(SHA256.HashData(cert.RawData)), pin, StringComparison.OrdinalIgnoreCase);
        using var c = new HttpClient(h) { Timeout = TimeSpan.FromSeconds(5) };
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c.GetStringAsync("https://127.0.0.1:18765/status").GetAwaiter().GetResult();
    }
}
'@
$stage = 'initial installation'
try {
    & (Join-Path $bundle 'Install-Agent.ps1')
    $first = Get-Content -LiteralPath (Join-Path $data 'pairing.json') -Raw | ConvertFrom-Json
    $ready = $false
    $stage = 'pinned HTTPS status from installed service'
    for ($i=0; $i -lt 15; $i++) {
        try {
            $status = [OfficePowerPinnedProbe]::Status($first.token, $first.certificateSha256) | ConvertFrom-Json
            if ($status.state -eq 'online') { $ready=$true; break }
        } catch { Start-Sleep -Seconds 1 }
    }
    if (-not $ready) { throw 'Installed SYSTEM service failed pinned HTTPS health check.' }
    # Make an existing child ACL too broad; reinstall must remove this explicit ACE.
    $stage = 'test ACL setup'
    $configPath = Join-Path $data 'agent-settings.json'
    & icacls.exe $configPath /grant '*S-1-5-32-545:R' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Test ACL grant failed.' }
    & icacls.exe $configPath /setowner '*S-1-5-32-545' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Test ACL setup failed.' }
    $stage = 'reinstallation'
    & (Join-Path $bundle 'Install-Agent.ps1')
    $second = Get-Content -LiteralPath (Join-Path $data 'pairing.json') -Raw | ConvertFrom-Json
    if ($first.token -cne $second.token -or $first.certificateSha256 -cne $second.certificateSha256) { throw 'Reinstallation changed credentials.' }
    $stage = 'ownership and DACL verification'
    $acl = Get-Acl -LiteralPath $configPath
    if ($acl.GetOwner([System.Security.Principal.SecurityIdentifier]).Value -ne 'S-1-5-32-544') { throw 'Unsafe child owner.' }
    foreach ($rule in $acl.Access) {
        $sid = $rule.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value
        if ($sid -notin @('S-1-5-18','S-1-5-32-544')) { throw 'Unexpected credential-file access rule.' }
    }
    $stage = 'service path verification'
    $service = Get-CimInstance Win32_Service -Filter "Name='OfficePowerAgent'"
    $expected = '"' + (Join-Path $env:ProgramFiles 'OfficePowerAgent\OfficePower.Agent.exe') + '"'
    if ($service.PathName -cne $expected) { throw 'Unsafe service executable quoting.' }
    Write-Host 'Windows install, pinned HTTPS, reinstall preservation, ACL and path quoting passed. No shutdown executed.'
} catch {
    $safeMessage = $_.Exception.Message -replace '[\r\n]', ' '
    Write-Host "::error title=Office Power Windows install ($stage)::$safeMessage"
    throw
} finally {
    & (Join-Path $bundle 'Uninstall-Agent.ps1')
}
