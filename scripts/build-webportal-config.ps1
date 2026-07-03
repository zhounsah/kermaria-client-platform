<#
.SYNOPSIS
Génère webportal.config.json à partir d'un fichier PowerShell .env.

.DESCRIPTION
Miroir de build-api-config.ps1 pour le WEBPORTAL Node.js sur SRV-01.
Extrait toutes les valeurs `$env:KEY = "value"` du .env.ps1 sauf celles
qui sont uniquement server-side (SQL, AD, BPCE, SMTP, logs, session
timeouts…) et produit
C:\ProgramData\Kermaria\webportal.config.json.

Le wrapper start-webportal.ps1 lit ce fichier au démarrage du service
NSSM, injecte chaque clé comme variable d'environnement dans sa propre
session PowerShell (donc dans le process Node enfant), et exec
`node server.js`. Aucune variable Machine impliquée.

Ne dot-source PAS le fichier source.

Aucune valeur n'est jamais affichée à la console.

.PARAMETER InputPath
Fichier .ps1 source. Auto-détection si omis.

.PARAMETER OutputPath
Fichier JSON destination. Défaut :
`C:\ProgramData\Kermaria\webportal.config.json`.

.PARAMETER WhatIf
Aperçu sans écrire.

.EXAMPLE
.\scripts\build-webportal-config.ps1 -WhatIf

.EXAMPLE
.\scripts\build-webportal-config.ps1 `
  -OutputPath \\KERMARIA-SRV-01\C$\ProgramData\Kermaria\webportal.config.json
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$InputPath,
    [string]$OutputPath = "C:\ProgramData\Kermaria\webportal.config.json"
)

$ErrorActionPreference = "Stop"

try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
} catch { }

# Blocklist : server-side only (API-INTERNAL) + secrets démo + dérogations dev.
# Le WEBPORTAL ne doit jamais avoir accès à ces clés — le compte de service
# svc-kermaria-web tourne sur SRV-01, sans droit d'accès à MariaDB ni AD.
$Blocklist = @(
    # Base de données — API seulement (WEBPORTAL n'accède jamais à SQL)
    "SQL_PROVIDER", "SQL_HOST", "SQL_PORT", "SQL_DATABASE",
    "SQL_USERNAME", "SQL_PASSWORD",
    # Active Directory — API seulement
    "AD_INTEGRATION_MODE", "AD_DOMAIN", "AD_CLIENTS_OU_DN",
    "AD_SERVICE_ACCOUNT_USERNAME", "AD_SERVICE_ACCOUNT_PASSWORD",
    "AD_ALLOWED_GROUPS", "AD_CONNECT_TIMEOUT_MS", "AD_QUERY_TIMEOUT_MS",
    "AD_MAX_RESULTS", "AD_PASSWORD_CHANGE_ENABLED",
    # BPCE — API seulement (WEBPORTAL n'appelle jamais BPCE)
    "BPCE_INTEGRATION_MODE", "BPCE_REFRESH_TOKEN", "BPCE_BASE_URL",
    "BPCE_SENDER_ID", "BPCE_REQUEST_TIMEOUT_MS",
    # SMTP — envoi email server-side (API-INTERNAL)
    "SMTP_HOST", "SMTP_PORT", "SMTP_USE_STARTTLS", "SMTP_USERNAME",
    "SMTP_PASSWORD", "SMTP_FROM_ADDRESS", "SMTP_FROM_DISPLAY_NAME",
    "SMTP_TIMEOUT_MS",
    "EMAIL_INTEGRATION_MODE", "EMAIL_LIVE_ALLOWLIST_ONLY",
    "EMAIL_LIVE_ALLOWLIST", "CONTACT_FORM_RECIPIENT",
    # Logs & session server-side
    "LOG_LEVEL", "LOG_FILE_DIRECTORY", "LOG_FILE_LEVEL",
    "LOG_FILE_RETENTION_DAYS",
    "SESSION_DURATION_MINUTES", "LOGIN_MAX_FAILURES",
    "LOGIN_LOCKOUT_MINUTES",
    # Comptes démo (jamais en dehors du dev local)
    "DEMO_PORTAL_EMAIL", "DEMO_PORTAL_PASSWORD",
    "DEMO_INTERNAL_ADMIN_EMAIL", "DEMO_INTERNAL_ADMIN_PASSWORD",
    "RUN_MARIADB_TESTS", "ALLOW_LOCAL_INTERNAL_API_URL",
    # Env / API du côté service Windows (côté Node c'est NODE_ENV, pas ASPNETCORE_)
    "ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT",
    "KERMARIA_CONFIG_PATH"
)

# Auto-détection du fichier source si non fourni.
if (-not $InputPath) {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
    $repoName = Split-Path $repoRoot -Leaf
    $repoParent = Split-Path $repoRoot -Parent
    $candidates = @(
        (Join-Path $repoRoot ".local.env.ps1"),
        (Join-Path $repoParent "$repoName.local.env.ps1"),
        (Join-Path $repoParent ".local.env.ps1")
    )
    $found = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if ($found) {
        $InputPath = $found
    } else {
        Write-Error "Aucun fichier source trouve automatiquement."
        $candidates | ForEach-Object { Write-Error "  - $_" }
        Write-Error "Passer -InputPath <chemin> explicitement."
        exit 1
    }
}

if (-not (Test-Path -LiteralPath $InputPath)) {
    throw "Fichier source introuvable : $InputPath"
}

Write-Host "Source : $InputPath"
Write-Host "Cible  : $OutputPath"
Write-Host ""

$extracted = [ordered]@{}
$blocked = @()
$emptyValues = @()

$pattern = '^\s*\$env:([A-Z_][A-Z0-9_]*)\s*=\s*([''"])(.*)\2\s*(?:#.*)?\s*$'

Get-Content -LiteralPath $InputPath | ForEach-Object {
    if ($_ -match $pattern) {
        $key = $Matches[1]
        $value = $Matches[3]
        if ($Blocklist -contains $key) {
            $blocked += $key
        } elseif ([string]::IsNullOrEmpty($value)) {
            $emptyValues += $key
        } else {
            $extracted[$key] = $value
        }
    }
}

# Defaults Node runtime — ajoutés si non présents dans .env.ps1
if (-not $extracted.Contains("NODE_ENV")) { $extracted["NODE_ENV"] = "production" }
if (-not $extracted.Contains("HOSTNAME")) { $extracted["HOSTNAME"] = "127.0.0.1" }
if (-not $extracted.Contains("PORT"))     { $extracted["PORT"] = "3000" }

Write-Host "Cles extraites : $($extracted.Count) (dont NODE_ENV/HOSTNAME/PORT par defaut si absents)"
foreach ($k in $extracted.Keys) {
    Write-Host "  [OK]      $k"
}
if ($emptyValues.Count -gt 0) {
    Write-Host ""
    Write-Host "Cles ignorees (valeur vide) : $($emptyValues.Count)"
    foreach ($k in $emptyValues) { Write-Host "  [empty]   $k" }
}
if ($blocked.Count -gt 0) {
    Write-Host ""
    Write-Host "Cles refusees par blocklist (server-side only, DEMO_*, dev) : $($blocked.Count)"
    foreach ($k in $blocked) { Write-Host "  [blocked] $k" }
}
Write-Host ""

if ($extracted.Count -eq 0) {
    throw "Aucune cle exploitable trouvee dans $InputPath."
}

$json = $extracted | ConvertTo-Json -Depth 1
$outputDir = Split-Path -Parent $OutputPath

if ($PSCmdlet.ShouldProcess($OutputPath, "Ecrire le fichier de config")) {
    if (-not (Test-Path -LiteralPath $outputDir)) {
        New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
        Write-Host "Dossier cree : $outputDir"
    }
    [System.IO.File]::WriteAllText(
        $OutputPath,
        $json,
        [System.Text.UTF8Encoding]::new($false))
    Write-Host ""
    Write-Host "Fichier ecrit : $OutputPath"
    Write-Host ""
    Write-Host "Prochaines etapes sur KERMARIA-SRV-01 :"
    Write-Host "  1. ACL restrictive :"
    Write-Host "     icacls '$outputDir' /inheritance:r ``"
    Write-Host "       /grant:r '*S-1-5-32-544:(OI)(CI)F' ``"
    Write-Host "       /grant:r 'svc-kermaria-web:(OI)(CI)RX'"
    Write-Host "     icacls '$OutputPath' /inheritance:r ``"
    Write-Host "       /grant:r '*S-1-5-32-544:F' ``"
    Write-Host "       /grant:r 'svc-kermaria-web:R'"
    Write-Host "  2. Installer le service NSSM avec le wrapper :"
    Write-Host "     voir docs/DEPLOYMENT_WINDOWS.md section KERMARIA-SRV-01."
    Write-Host "  3. Restart-Service KermariaWebportal"
}
