# Verification executable sous Windows PowerShell 5.1, sans dependance Pester.
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path (Split-Path -Parent $PSScriptRoot) 'KoxoSync.Common.psm1') -Force -DisableNameChecking
function Assert-Contract([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
$user = [pscustomobject]@{
    civilite='M.';nom='Test';prenom='Noe';dateNaissance='1990-01-01'
    identifiantUnique='CLI-000001';groupeSecondaire='CLI-TEST';groupePrimaire='CLIENTS'
    email='noe@example.invalid';qualitesSupplementaires='GG_RDS_E2E_DEV,GG_VPN_E2E_DEV'
    motDePasse='FAKE;CSV"TEST'
}
$payload = [pscustomobject]@{schemaVersion=3;generatedAt='2026-10-04T00:00:00Z';userCount=1;users=@($user)}
Assert-Contract (Test-KoxoExportPayload $payload).IsValid 'Valid qualities rejected'
$file = Join-Path $env:TEMP ('koxo-qualities-' + [guid]::NewGuid().ToString('N') + '.csv')
try {
    Write-KoxoTextFile -Path $file -Content (ConvertTo-KoxoCsvContent @($user)) -EncodingName utf8bom
    Assert-Contract (Test-KoxoCsvFile $file) 'Invalid CSV'
    $row = @(Import-Csv -LiteralPath $file -Delimiter ';')[0]
    Assert-Contract (@($row.PSObject.Properties).Count -eq 15) 'Column width'
    Assert-Contract ($row.MotDePasse -ceq $user.motDePasse) 'Password escaping or position changed'
    $properties = @($row.PSObject.Properties)
    Assert-Contract ($properties[14].Value -ceq $user.qualitesSupplementaires) 'Quality column changed'
    $user.qualitesSupplementaires = ''
    Assert-Contract (Test-KoxoExportPayload $payload).IsValid 'Explicit empty qualities rejected'
    Write-KoxoTextFile -Path $file -Content (ConvertTo-KoxoCsvContent @($user)) -EncodingName utf8bom
    Assert-Contract (Test-KoxoCsvFile $file) 'Empty qualities broke CSV width'
    $row = @(Import-Csv -LiteralPath $file -Delimiter ';')[0]
    Assert-Contract (@($row.PSObject.Properties)[14].Value -ceq '') 'Removed quality retained'
    foreach ($invalid in @("GG_VPN;GG_ADMIN", "GG_VPN`r`nGG_ADMIN", 'GG_VPN,,GG_ADMIN')) {
        $user.qualitesSupplementaires = $invalid
        Assert-Contract (-not (Test-KoxoExportPayload $payload).IsValid) 'Injected quality accepted'
    }
    $user.qualitesSupplementaires = ''
    $payload.schemaVersion = 2
    Assert-Contract (-not (Test-KoxoExportPayload $payload).IsValid) 'Old schema accepted'
    $payload.schemaVersion = 3
    $user.PSObject.Properties.Remove('qualitesSupplementaires')
    Assert-Contract (-not (Test-KoxoExportPayload $payload).IsValid) 'Missing qualities treated as removal'
    Write-Output 'KoXo qualities CSV contract PASS (15 columns, escaping, removal, invalid values, schema)'
} finally {
    Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue
}
