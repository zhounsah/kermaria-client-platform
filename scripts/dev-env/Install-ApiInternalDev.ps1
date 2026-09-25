<#
.SYNOPSIS
  Installe ou met a jour l'API-INTERNAL DEV sur SRV-13, a cote de la production.

.DESCRIPTION
  Purement additif. Ne lit, n'arrete, ne modifie ni ne redemarre jamais le
  service de production `KermariaApiInternal`, son dossier
  `C:\apps\api-internal` ni `C:\ProgramData\Kermaria\api-internal.config.json`.

  Instance DEV :
    - service      KermariaApiInternalDev ("ZacharyIT API Internal DEV")
    - compte       NT SERVICE\KermariaApiInternalDev (compte virtuel : aucun
                   acces aux fichiers de configuration de production)
    - binaires     C:\apps\api-internal-dev
    - config       C:\ProgramData\Kermaria-dev\api-internal.dev.config.json
                   (KERMARIA_CONFIG_AUTHORITATIVE=true : le fichier l'emporte
                   sur les variables Machine SQL_* de la production)
    - journaux     D:\Kermaria-dev\Logs\ApiInternal
    - ecoute       http://192.168.100.213:5100, pare-feu : SRV-12 et SRV-13 seuls

  Les secrets proviennent de `<parent du depot>\kermaria-client-platform.dev.env.ps1`
  (hors Git), jamais du fichier `.local.env.ps1` qui porte les secrets LIVE.

.EXAMPLE
  dotnet publish apps/api-internal/Kermaria.ApiInternal.csproj -c Release -p:UseAppHost=true -o $env:TEMP\api-internal-dev
  .\scripts\dev-env\Install-ApiInternalDev.ps1 -PublishDirectory $env:TEMP\api-internal-dev
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $PublishDirectory,
    [string] $ComputerName = 'KERMARIA-SRV-13.home.bzh',
    [string] $DevSecretsFile = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent | Split-Path -Parent) 'kermaria-client-platform.dev.env.ps1'),
    [string] $PublicPortalUrl = 'https://dev.zachary-it.fr',
    [switch] $NoStart
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Join-Path $PublishDirectory 'Kermaria.ApiInternal.exe'))) {
    throw "Kermaria.ApiInternal.exe absent de $PublishDirectory : publier avec -p:UseAppHost=true."
}
if (-not (Test-Path $DevSecretsFile)) {
    throw "Fichier de secrets DEV introuvable : $DevSecretsFile"
}
. $DevSecretsFile

foreach ($key in 'DEV_STRIPE_SECRET_KEY', 'DEV_STRIPE_PUBLISHABLE_KEY') {
    $value = [Environment]::GetEnvironmentVariable($key)
    if ($value -and ($value -like 'sk_live_*' -or $value -like 'rk_live_*' -or $value -like 'pk_live_*')) {
        throw "$key contient une cle LIVE : refus d'installation."
    }
}

$stripeConfigured = [bool]$env:DEV_STRIPE_SECRET_KEY -and [bool]$env:DEV_STRIPE_PUBLISHABLE_KEY

$config = [ordered]@{
    APP_ENV                                     = 'Development'

    SQL_PROVIDER                                = 'mariadb'
    SQL_HOST                                    = $env:DEV_SQL_HOST
    SQL_PORT                                    = $env:DEV_SQL_PORT
    SQL_DATABASE                                = $env:DEV_SQL_DATABASE
    SQL_USERNAME                                = $env:DEV_SQL_USERNAME
    SQL_PASSWORD                                = ${env:DEV_SQL_PASSWORD}

    SERVICE_AUTH_TOKEN                          = ${env:DEV_SERVICE_AUTH_TOKEN}

    SESSION_COOKIE_SECURE                       = 'true'
    LOG_FILE_DIRECTORY                          = 'D:\Kermaria-dev\Logs\ApiInternal'
    LOG_FILE_LEVEL                              = 'Information'
    LOG_FILE_RETENTION_DAYS                     = '14'
    DOWNLOAD_STORAGE_ROOT                       = 'D:\Kermaria-dev\Downloads'

    PUBLIC_PORTAL_URL                           = $PublicPortalUrl
    WEBPORTAL_BASE_URL                          = $PublicPortalUrl
    PUBLIC_VITRINE_ENABLED                      = 'true'
    SIGNUP_ENABLED                              = 'true'

    HCAPTCHA_SITE_KEY                           = '10000000-ffff-ffff-ffff-000000000001'
    HCAPTCHA_SECRET_KEY                         = '0x0000000000000000000000000000000000000000'

    # Même pipeline que PROD, mais sans effets réels.
    AD_INTEGRATION_MODE                         = 'test'
    BPCE_INTEGRATION_MODE 			= 'live'
    ALLOW_DEV_BPCE_LIVE   			= 'true'
    BPCE_BASE_URL                               = $env:DEV_BPCE_BASE_URL
    BPCE_SENDER_ID                              = $env:DEV_BPCE_SENDER_ID
    BPCE_REFRESH_TOKEN                          = ${env:DEV_BPCE_REFRESH_TOKEN}
    PAYPAL_MODE                                 = 'sandbox'
    EMAIL_INTEGRATION_MODE                      = 'mock'
    EMAIL_LIVE_ALLOWLIST_ONLY                   = 'true'
    STRIPE_MODE                                 = 'test'

    # Billing V2 : même orchestration que PROD.
    BILLING_V2_NEW_SUBSCRIPTIONS_ENABLED        = 'true'
    BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED   = 'true'
    BILLING_V2_FIRST_REAL_SUBSCRIPTION_APPROVED = 'true'
    BILLING_V2_PROVIDER_OUTBOX_ENABLED          = 'true'
    BILLING_V2_PROVIDER_EXECUTOR_ENABLED        = 'true'
    BILLING_V2_RECONCILIATION_WORKER_ENABLED    = 'true'

    # Effets d'infrastructure toujours neutralisés.
    BILLING_V2_PROVISIONING_ENABLED             = 'true'
    BILLING_V2_ADDITIONAL_USER_PROVISIONING_ENABLED = 'true'
    BILLING_V2_SERVICE_FULFILLMENT_ENABLED      = 'true'
    BILLING_V2_VPS_LOCAL_PROVISIONING_ENABLED   = 'true'
    BILLING_V2_VPS_CLOUD_AUTOMATION_ENABLED     = 'true'

    BILLING_V2_STRIPE_RECURRING_MUTATION_ENABLED = 'true'
    BILLING_V2_SUBSCRIPTION_CHANGES_ENABLED     = 'true'
    BILLING_V2_GENERIC_SELECTION_ENABLED        = 'true'

    PROVISIONING_ENABLED                        = 'true'
    ALLOW_DEV_PROVISIONING                      = 'true'
}
if ($stripeConfigured) {
    $config.STRIPE_SECRET_KEY = $env:DEV_STRIPE_SECRET_KEY
    $config.STRIPE_PUBLISHABLE_KEY = $env:DEV_STRIPE_PUBLISHABLE_KEY
    if ($env:DEV_STRIPE_WEBHOOK_SECRET) {
        $config.STRIPE_WEBHOOK_SECRET = $env:DEV_STRIPE_WEBHOOK_SECRET
    }
}
$missing = $config.GetEnumerator() | Where-Object { [string]::IsNullOrWhiteSpace($_.Value) } | ForEach-Object Key
if ($missing) {
    throw "Valeurs DEV manquantes : $($missing -join ', ')"
}
$configJson = $config | ConvertTo-Json -Depth 2

$session = New-PSSession -ComputerName $ComputerName
try {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $staging = "C:\apps\api-internal-dev-staging-$stamp"
    Invoke-Command -Session $session -ScriptBlock {
        param($staging)
        New-Item -ItemType Directory -Force -Path $staging | Out-Null
    } -ArgumentList $staging
    Copy-Item -Path (Join-Path $PublishDirectory '*') -Destination $staging -Recurse -ToSession $session

    Invoke-Command -Session $session -ArgumentList $staging, $stamp, $configJson, $NoStart.IsPresent -ScriptBlock {
        param($staging, $stamp, $configJson, $noStart)
        $ErrorActionPreference = 'Stop'

        $serviceName = 'KermariaApiInternalDev'
        $account = "NT SERVICE\$serviceName"
        $appDir = 'C:\apps\api-internal-dev'
        $configDir = 'C:\ProgramData\Kermaria-dev'
        $configPath = Join-Path $configDir 'api-internal.dev.config.json'
        $dataRoot = 'D:\Kermaria-dev'

        if ($serviceName -eq 'KermariaApiInternal' -or $appDir -eq 'C:\apps\api-internal') {
            throw 'Garde-fou : cible de production detectee.'
        }

        $existing = Get-Service $serviceName -ErrorAction SilentlyContinue
        if ($existing -and $existing.Status -ne 'Stopped') {
            Stop-Service $serviceName -Force
            $existing.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
        }

        # Bascule des binaires : l'ancienne version DEV est conservee.
        if (Test-Path $appDir) {
            Rename-Item $appDir "api-internal-dev-old-$stamp"
        }
        Rename-Item $staging (Split-Path $appDir -Leaf)

        foreach ($dir in $configDir, "$dataRoot\Logs\ApiInternal", "$dataRoot\Downloads") {
            New-Item -ItemType Directory -Force -Path $dir | Out-Null
        }

        # Configuration : UTF-8 sans BOM, sauvegarde de la precedente.
        if (Test-Path $configPath) {
            Copy-Item $configPath "$configPath.bak-$stamp"
        }
        [IO.File]::WriteAllText($configPath, $configJson, [Text.UTF8Encoding]::new($false))

        if (-not $existing) {
            New-Service -Name $serviceName `
                -DisplayName 'ZacharyIT API Internal DEV' `
                -Description 'API-INTERNAL environnement DEV (kermaria_dev, Stripe TEST, provisioning ferme). Independant de KermariaApiInternal.' `
                -BinaryPathName "`"$appDir\Kermaria.ApiInternal.exe`" --environment Staging --urls http://192.168.100.213:5100" `
                -StartupType Manual | Out-Null
            & sc.exe config $serviceName obj= $account start= delayed-auto | Out-Null
            & sc.exe failure $serviceName reset= 86400 actions= restart/10000/restart/30000/restart/60000 | Out-Null
        }

        # Variables propres au service (n'affectent aucun autre processus).
        Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName" -Name Environment -Type MultiString -Value @(
            "KERMARIA_CONFIG_PATH=$configPath",
            'KERMARIA_CONFIG_AUTHORITATIVE=true',
            'ASPNETCORE_ENVIRONMENT=Staging',
            'DOTNET_ENVIRONMENT=Staging'
        )

        # ACL : le compte virtuel DEV lit ses binaires et sa config, ecrit ses
        # journaux ; la config n'est lisible que par lui et les administrateurs.
        & icacls $configPath /inheritance:r /grant:r '*S-1-5-32-544:(F)' 'SYSTEM:(F)' "${account}:(R)" | Out-Null
        & icacls $appDir /grant "${account}:(OI)(CI)RX" | Out-Null
        & icacls $dataRoot /grant "${account}:(OI)(CI)M" | Out-Null

        # Pare-feu : 5100 refuse a toute adresse sauf SRV-12 et SRV-13.
        $ruleName = 'Kermaria API DEV 5100 - bloquer hors SRV-12'
        if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
            New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort 5100 `
                -Action Block -RemoteAddress '0.0.0.0-192.168.100.211', '192.168.100.214-255.255.255.255' `
                -Profile Any | Out-Null
        }

        if (-not $noStart) {
            Start-Service $serviceName
        }
        Get-Service $serviceName | Select-Object Name, DisplayName, Status, StartType
    }
}
finally {
    Remove-PSSession $session
}
