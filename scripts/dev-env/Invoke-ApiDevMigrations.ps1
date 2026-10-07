<#
.SYNOPSIS
  Applique les migrations du depot a `kermaria_dev` (et le jeu de demo fictif).

.DESCRIPTION
  Execute sur SRV-13, depuis les binaires DEV (C:\apps\api-internal-dev), avec
  le compte `kermaria_dev_migrator` qui n'a de droits que sur `kermaria_dev`.

  Triple verrou contre une migration de la production :
    1. APP_ENV=Development (lu dans la config DEV) : l'API refuse toute base
       qui n'est pas *_dev et tout compte qui n'est pas *_dev* ;
    2. controle des droits reels (SHOW GRANTS) avant la moindre migration ;
    3. le compte migrateur DEV n'a aucun droit sur `kermaria`.

  Les variables SQL_* posees ici sont propres au processus : elles masquent les
  variables Machine SQL_* de la production sans les modifier.
#>
[CmdletBinding()]
param(
    [string] $ComputerName = 'KERMARIA-SRV-13.home.bzh',
    [string] $DevSecretsFile,
    [switch] $SeedDemoData
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($DevSecretsFile)) {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $DevSecretsFile = Join-Path (Split-Path -Parent $repoRoot) 'kermaria-client-platform.dev.env.ps1'
}
. $DevSecretsFile

$variables = @{
    KERMARIA_CONFIG_PATH   = 'C:\ProgramData\Kermaria-dev\api-internal.dev.config.json'
    ASPNETCORE_ENVIRONMENT = 'Development'
    DOTNET_ENVIRONMENT     = 'Development'
    APP_ENV                = 'Development'
    SQL_PROVIDER           = 'mariadb'
    SQL_HOST               = $env:DEV_SQL_HOST
    SQL_PORT               = $env:DEV_SQL_PORT
    SQL_DATABASE           = $env:DEV_SQL_DATABASE
    SQL_USERNAME           = $env:DEV_SQL_MIGRATOR_USERNAME
    SQL_PASSWORD           = ${env:DEV_SQL_MIGRATOR_PASSWORD}
}
if ($SeedDemoData) {
    # Comptes fictifs du seed : valeurs lues dans le fichier de secrets DEV.
    foreach ($name in 'DEMO_PORTAL_EMAIL', 'DEMO_PORTAL_PASSWORD', 'DEMO_INTERNAL_ADMIN_EMAIL', 'DEMO_INTERNAL_ADMIN_PASSWORD') {
        $variables[$name] = [Environment]::GetEnvironmentVariable("DEV_$name")
    }
    $variables['DEMO_PORTAL_STATUS'] = 'active'
}

Invoke-Command -ComputerName $ComputerName -ArgumentList $variables, $SeedDemoData.IsPresent -ScriptBlock {
    param($variables, $seed)
    if ($variables.SQL_DATABASE -notlike '*_dev') {
        throw "Garde-fou : base cible $($variables.SQL_DATABASE) refusee."
    }
    # SRV-13 porte en variables Machine des reglages de production (SQL_*,
    # AD_*, KOXO_*). Ils sont retires de CE processus seulement.
    Get-ChildItem Env: |
        Where-Object { $_.Name -match '^(SQL_|AD_|KOXO_|STRIPE_|PAYPAL_|BPCE_|SMTP_|EMAIL_|BILLING_)' } |
        ForEach-Object { [Environment]::SetEnvironmentVariable($_.Name, $null, 'Process') }
    foreach ($entry in $variables.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
    # Ne pas forcer AD_INTEGRATION_MODE=disabled ici : la configuration DEV
    # peut activer le registre des qualites KoXo, dont la validation exige le
    # mode controlled_write au moment de construire le conteneur. La commande
    # --apply-migrations quitte avant app.Run ; aucun worker AD/KoXo ne demarre.
    # Les variables Machine AD_* de PROD ont ete effacees ci-dessus ; seul le
    # JSON DEV existant apporte ce mode et ses bornes.
    # La config DEV n'est pas autoritaire ici : les SQL_* du migrateur priment.
    [Environment]::SetEnvironmentVariable('KERMARIA_CONFIG_AUTHORITATIVE', $null, 'Process')

    $arguments = @('--apply-migrations')
    if ($seed) { $arguments += '--seed-demo-data' }

    $ErrorActionPreference = 'Continue'
    $output = & 'C:\apps\api-internal-dev\Kermaria.ApiInternal.exe' @arguments 2>&1
    $code = $LASTEXITCODE
    $output | ForEach-Object { "$_" } |
        Where-Object { $_ -match 'Deployment environment|FATAL|migration|Migration|seed|Seed|"LogLevel":"(Error|Critical|Warning)"' } |
        ForEach-Object { if ($_.Length -gt 400) { $_.Substring(0, 400) + '...' } else { $_ } }
    "exit=$code"
    if ($code -ne 0) {
        throw "API DEV migration runner failed (exit=$code)."
    }
}
