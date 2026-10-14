[CmdletBinding()]
param(
    [switch] $Apply,
    [string] $ExpectedConfigSha256
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Apply -and $ExpectedConfigSha256 -notmatch '^[0-9A-Fa-f]{64}$') {
    throw 'PROD_EXPECTED_CONFIG_SHA256_REQUIRED'
}

# Sans -Apply, cette commande inspecte uniquement. Avec -Apply, elle ferme
# les nouveaux Checkout V2 sur l'API PROD, sauvegarde le JSON exact et
# redemarre seulement KermariaApiInternal. Aucun fournisseur n'est appele.
Invoke-Command -ComputerName KERMARIA-SRV-13.home.bzh `
    -ArgumentList $Apply.IsPresent, $ExpectedConfigSha256 -ScriptBlock {
    param([bool] $DoApply, [string] $ExpectedHash)
    $ErrorActionPreference = 'Stop'
    $path = 'C:\ProgramData\Kermaria\api-internal.config.json'
    $serviceName = 'KermariaApiInternal'
    $service = Get-CimInstance Win32_Service -Filter "Name = '$serviceName'"
    if ($null -eq $service -or
        $service.PathName -notmatch 'C:\\apps\\api-internal\\Kermaria\.ApiInternal\.exe' -or
        $service.PathName -match 'api-internal-dev') {
        throw 'PROD_SERVICE_IDENTITY_MISMATCH'
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw 'PROD_CONFIG_MISSING'
    }
    if ([Environment]::GetEnvironmentVariable(
            'BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED', 'Machine')) {
        throw 'PROD_CHECKOUT_MACHINE_OVERRIDE_PRESENT'
    }
    $serviceRegistry = Get-ItemProperty `
        'HKLM:\SYSTEM\CurrentControlSet\Services\KermariaApiInternal'
    if (@($serviceRegistry.Environment | Where-Object {
            $_ -like 'BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED=*'
        }).Count -ne 0) {
        throw 'PROD_CHECKOUT_SERVICE_OVERRIDE_PRESENT'
    }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    $raw = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
    $json = $raw | ConvertFrom-Json
    if ($json.STRIPE_MODE -ne 'live' -or
        $json.SQL_DATABASE -ne 'kermaria' -or
        $json.BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED -notin @('true','false')) {
        throw 'PROD_CONFIG_IDENTITY_MISMATCH'
    }
    $before = $json.BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED
    if (-not $DoApply) {
        [pscustomobject]@{ Mode='inspect'; CheckoutEnabled=$before;
            ConfigSha256=$hash; Service=$service.State; Database=$json.SQL_DATABASE }
        return
    }
    if ($hash -ne $ExpectedHash.ToUpperInvariant()) {
        throw 'PROD_CONFIG_SHA256_CHANGED'
    }
    if ($before -ne 'true') {
        throw 'PROD_CHECKOUT_NOT_OPEN_AS_EXPECTED'
    }
    $pattern = '(?m)(^\s*"BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED"\s*:\s*)"true"'
    if ([regex]::Matches($raw, $pattern).Count -ne 1) {
        throw 'PROD_CHECKOUT_CONFIG_MATCH_NOT_UNIQUE'
    }
    $newRaw = [regex]::Replace($raw, $pattern, '${1}"false"')
    $newJson = $newRaw | ConvertFrom-Json
    if ($newJson.BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED -ne 'false') {
        throw 'PROD_CHECKOUT_CONFIG_WRITE_INVALID'
    }
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backup = "$path.pre-vat-checkout-close-$stamp.bak"
    $staging = "$path.vat-close-$stamp.new"
    Copy-Item -LiteralPath $path -Destination $backup -ErrorAction Stop
    [IO.File]::WriteAllText($staging, $newRaw,
        (New-Object Text.UTF8Encoding($false)))
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $hash) {
        throw 'PROD_CONFIG_CHANGED_DURING_WRITE'
    }
    Move-Item -LiteralPath $staging -Destination $path -Force -ErrorAction Stop
    Restart-Service $serviceName -ErrorAction Stop
    $ready = $false
    for ($attempt=0; $attempt -lt 10; $attempt++) {
        try {
            $response = Invoke-WebRequest `
                -Uri 'http://192.168.100.213:5000/health/ready' `
                -UseBasicParsing -TimeoutSec 8
            if ($response.StatusCode -eq 200) { $ready = $true; break }
        }
        catch { Start-Sleep -Seconds 2 }
    }
    if (-not $ready) {
        throw 'PROD_CHECKOUT_CLOSED_BUT_API_HEALTH_UNVERIFIED'
    }
    [pscustomobject]@{ Mode='applied'; CheckoutEnabled='false';
        PreviousConfigSha256=$hash;
        ConfigSha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash;
        Backup=$backup; Service=(Get-Service $serviceName).Status.ToString();
        HealthStatus=200 }
}
