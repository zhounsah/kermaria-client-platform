<#
.SYNOPSIS
  Deploie le WebPortal DEV sur SRV-12 (port 3100, API DEV 192.168.100.213:5100).

.DESCRIPTION
  Copie l'archive .tar.gz et les fichiers de scripts/dev-env/srv12, puis
  transmet l'environnement DEV sur l'entree standard de install-webportal-dev.sh
  (jamais en argument, jamais dans le depot). Ne touche pas au service PROD.

  Construire l'archive avec APP_ENV=Development (voir docs/DEV_ENVIRONMENT.md).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Archive,
    [string] $SshHost = 'kermaria-srv-12',
    [string] $DevSecretsFile = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent | Split-Path -Parent) 'kermaria-client-platform.dev.env.ps1'),
    [string] $PublicPortalUrl = 'https://dev.zachary-it.fr',
    [string] $InternalApiUrl = 'http://192.168.100.213:5100'
)

$ErrorActionPreference = 'Stop'
. $DevSecretsFile

$releaseName = [IO.Path]::GetFileName($Archive) -replace '\.tar\.gz$', ''
$stripeConfigured = [bool]$env:DEV_STRIPE_SECRET_KEY -and [bool]$env:DEV_STRIPE_PUBLISHABLE_KEY

$envLines = [ordered]@{
    NODE_ENV                                   = 'production'
    APP_ENV                                    = 'Development'
    HOSTNAME                                   = '192.168.100.212'
    PORT                                       = '3100'
    INTERNAL_API_URL                           = $InternalApiUrl
    SERVICE_AUTH_TOKEN                         = ${env:DEV_SERVICE_AUTH_TOKEN}
    PUBLIC_PORTAL_URL                          = $PublicPortalUrl
    WEBPORTAL_BASE_URL                         = $PublicPortalUrl
    SESSION_COOKIE_NAME                        = 'kermaria_dev_portal_session'
    SESSION_COOKIE_SAME_SITE                   = 'lax'
    SESSION_COOKIE_SECURE                      = 'true'
    SIGNUP_ENABLED                             = 'true'
    PUBLIC_VITRINE_ENABLED                     = 'true'
    HCAPTCHA_SITE_KEY                          = '10000000-ffff-ffff-ffff-000000000001'
    HCAPTCHA_SECRET_KEY                        = '0x0000000000000000000000000000000000000000'
    PAYPAL_MODE                                = 'disabled'
    STRIPE_MODE                                = $(if ($stripeConfigured) { 'test' } else { 'disabled' })
    BILLING_V2_AUTHORITATIVE_CHECKOUT_BFF_ENABLED = 'true'
    AD_PASSWORD_CHANGE_ENABLED                 = 'false'
    KOXO_EXPORT_API_TOKEN                      = $env:DEV_KOXO_EXPORT_API_TOKEN
    KOXO_EXPORT_REQUIRE_HTTPS                  = 'true'
}
if ($stripeConfigured) {
    $envLines.STRIPE_SECRET_KEY = $env:DEV_STRIPE_SECRET_KEY
    $envLines.STRIPE_PUBLISHABLE_KEY = $env:DEV_STRIPE_PUBLISHABLE_KEY
    if ($env:DEV_STRIPE_WEBHOOK_SECRET) {
        $envLines.STRIPE_WEBHOOK_SECRET = $env:DEV_STRIPE_WEBHOOK_SECRET
    }
}
foreach ($entry in $envLines.GetEnumerator()) {
    if ([string]::IsNullOrWhiteSpace($entry.Value)) { throw "Valeur DEV manquante : $($entry.Key)" }
    if ($entry.Value -match '(sk|rk|pk)_live_') { throw "$($entry.Key) contient une cle LIVE." }
}
$envText = ($envLines.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "`n"

$remoteDir = "/tmp/kermaria-webportal-dev-$releaseName"
ssh -o BatchMode=yes $SshHost "mkdir -p $remoteDir"
scp -q -o BatchMode=yes $Archive "${SshHost}:$remoteDir/release.tar.gz"
scp -q -o BatchMode=yes (Join-Path $PSScriptRoot 'srv12\install-webportal-dev.sh') (Join-Path $PSScriptRoot 'srv12\kermaria-webportal-dev.service') "${SshHost}:$remoteDir/"
$envText | ssh -o BatchMode=yes $SshHost "sed -i 's/\r$//' $remoteDir/install-webportal-dev.sh && sudo -n bash $remoteDir/install-webportal-dev.sh $remoteDir/release.tar.gz $releaseName && rm -rf $remoteDir"
