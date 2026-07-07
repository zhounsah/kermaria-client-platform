<#
.SYNOPSIS
Génère webportal.config.json à partir d'un fichier PowerShell .env.

.DESCRIPTION
Miroir de build-api-config.ps1 pour le WEBPORTAL Node.js sur SRV-01.
Extrait toutes les valeurs du .env.ps1 déclarées soit sous la forme
`$env:KEY = "value"`, soit sous la forme
`Set-Item -Path 'Env:KEY-WITH-HYPHEN' -Value 'value'`, sauf celles qui
sont uniquement server-side (SQL, AD, BPCE, SMTP, logs, session
timeouts…) et produit C:\ProgramData\Kermaria\webportal.config.json.

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

.PARAMETER Override
Table de hachage de clés à forcer APRÈS extraction et defaults. Utile
pour les valeurs host-spécifiques qui diffèrent entre le poste de dev
et la cible : typiquement `INTERNAL_API_URL`, qui vaut `localhost:5000`
en dev local (correct) mais doit pointer l'IP VLAN de SRV-02 en staging
split-host. On garde ainsi un seul `.local.env.ps1` de dev sans risquer
de réinjecter la mauvaise valeur au prochain build staging.

.PARAMETER WhatIf
Aperçu sans écrire.

.EXAMPLE
.\scripts\build-webportal-config.ps1 -WhatIf

.EXAMPLE
.\scripts\build-webportal-config.ps1 `
  -OutputPath \\KERMARIA-SRV-01\C$\ProgramData\Kermaria\webportal.config.json

.EXAMPLE
# Staging split-host : INTERNAL_API_URL doit viser l'IP VLAN de SRV-02,
# jamais localhost (sinon /api/health/ready = 503 en production).
.\scripts\build-webportal-config.ps1 `
  -OutputPath \\KERMARIA-SRV-01\C$\ProgramData\Kermaria\webportal.config.json `
  -Override @{ INTERNAL_API_URL = "http://192.168.100.202:5000" }
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$InputPath,
    [string]$OutputPath = "C:\ProgramData\Kermaria\webportal.config.json",
    [hashtable]$Override = @{}
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

$assignmentPattern =
    '^\s*\$env:([A-Z_][A-Z0-9_]*)\s*=\s*([''"])(.*)\2\s*(?:#.*)?\s*$'
$setItemPattern =
    '^\s*Set-Item\s+-Path\s+([''"])Env:([^''"]+)\1\s+-Value\s+([''"])(.*)\3\s*(?:#.*)?\s*$'

Get-Content -LiteralPath $InputPath | ForEach-Object {
    $key = $null
    $value = $null

    if ($_ -match $assignmentPattern) {
        $key = $Matches[1]
        $value = $Matches[3]
    } elseif ($_ -match $setItemPattern) {
        $key = $Matches[2]
        $value = $Matches[4]
    }

    if ($null -ne $key) {
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

# Overrides host-specifiques (ex. -Override @{ INTERNAL_API_URL = "http://192.168.100.202:5000" }).
# Appliques APRES extraction et defaults : ils gagnent sur la valeur du .env.ps1
# et sur les defaults. Permet de garder INTERNAL_API_URL=localhost:5000 (correct
# pour le dev local) dans le .local.env.ps1 unique tout en ciblant l'IP VLAN de
# SRV-02 au build de la config staging/prod split-host.
$overridden = @()
foreach ($key in @($Override.Keys)) {
    $name = [string]$key
    if ($Blocklist -contains $name) {
        throw "Cle '$name' passee en -Override mais blocklistee (server-side/dev only) : refus."
    }
    $extracted[$name] = [string]$Override[$key]
    $overridden += $name
}

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
if ($overridden.Count -gt 0) {
    Write-Host ""
    Write-Host "Cles forcees via -Override : $($overridden.Count)"
    foreach ($k in $overridden) { Write-Host "  [override] $k" }
}
Write-Host ""

if ($extracted.Count -eq 0) {
    throw "Aucune cle exploitable trouvee dans $InputPath."
}

# Garde-fou : miroir de getInternalApiUrl()/validateServerRuntimeConfiguration()
# dans apps/webportal/lib/runtime-config.ts. En NODE_ENV=production une
# INTERNAL_API_URL pointant un hostname local fait throw le webportal au
# demarrage (ALLOW_LOCAL_INTERNAL_API_URL est blocklistee ici, donc jamais
# true dans la config generee) => /api/health/ready renvoie 503. C'est le cas
# quand on regenere une config staging split-host depuis le .local.env.ps1 de
# dev (INTERNAL_API_URL=localhost:5000). On previent au build plutot qu'a la
# recette.
if ($extracted["NODE_ENV"] -eq "production" -and $extracted.Contains("INTERNAL_API_URL")) {
    $localHosts = @("localhost", "127.0.0.1", "::1")
    $apiHost = $null
    try { $apiHost = ([System.Uri]$extracted["INTERNAL_API_URL"]).Host.Trim('[', ']') } catch { }
    if ($apiHost -and ($localHosts -contains $apiHost.ToLower())) {
        Write-Warning "INTERNAL_API_URL vise un hostname LOCAL ($apiHost) avec NODE_ENV=production."
        Write-Warning "Le webportal throw au demarrage (runtime-config.ts) => /api/health/ready = 503."
        Write-Warning "Deploiement multi-hotes : passer -Override @{ INTERNAL_API_URL = 'http://<IP_VLAN_SRV-02>:5000' }."
    }
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
