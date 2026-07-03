<#
.SYNOPSIS
Génère api-internal.config.json à partir d'un fichier PowerShell .env.

.DESCRIPTION
Extrait TOUTES les valeurs `$env:KEY = "value"` d'un fichier PowerShell
(typiquement .local.env.ps1) et produit un JSON plat lisible par la source
Configuration de l'API (Program.cs charge
C:\ProgramData\Kermaria\api-internal.config.json par défaut).

L'objectif est de rassembler TOUTE la config runtime de l'API (SQL, secrets,
modes, logs, session, seuils) dans un seul fichier, plutôt que d'en éclater
la moitié dans des variables Machine et l'autre dans un fichier "secrets".

L'environnement (Staging/Production) NE se met PAS dans le JSON — il se
passe via l'argument `--environment Staging` du service Windows, parsé
par ASP.NET Core avant la lecture du config file (voir sc.exe binPath dans
docs/DEPLOYMENT_WINDOWS.md).

Ne dot-source PAS le fichier source (parsing regex uniquement) pour ne pas
exécuter du code arbitraire.

Aucune valeur n'est jamais affichée à la console — seuls les noms de clés
extraites, bloquées, ou ignorées (empty).

.PARAMETER InputPath
Fichier .ps1 source. Si non fourni, cherche automatiquement dans :
  1. <repo>/.local.env.ps1
  2. <repo-parent>/<repo-name>.local.env.ps1
  3. <repo-parent>/.local.env.ps1

.PARAMETER OutputPath
Fichier JSON destination. Défaut :
`C:\ProgramData\Kermaria\api-internal.config.json`.

.PARAMETER Override
Table de hachage de clés à forcer APRÈS extraction et defaults. Utile
pour les valeurs host-spécifiques qui diffèrent entre le poste de dev
et la cible sans éditer le `.local.env.ps1` : par exemple `SQL_HOST`
(dev local vs `192.168.100.207` de SRV-07), ou tout autre paramètre
dépendant de la topologie. Miroir du même mécanisme dans
build-webportal-config.ps1 (où il sert pour `INTERNAL_API_URL`).

.PARAMETER WhatIf
Affiche les clés qui seraient extraites sans écrire le fichier.

.EXAMPLE
.\scripts\build-api-config.ps1 -WhatIf

.EXAMPLE
.\scripts\build-api-config.ps1 `
  -OutputPath \\KERMARIA-SRV-02\C$\ProgramData\Kermaria\api-internal.config.json

.EXAMPLE
# Forcer SQL_HOST vers SRV-07 si le .local.env.ps1 de dev pointe ailleurs.
.\scripts\build-api-config.ps1 `
  -OutputPath \\KERMARIA-SRV-02\C$\ProgramData\Kermaria\api-internal.config.json `
  -Override @{ SQL_HOST = "192.168.100.207" }
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$InputPath,
    [string]$OutputPath = "C:\ProgramData\Kermaria\api-internal.config.json",
    [hashtable]$Override = @{}
)

$ErrorActionPreference = "Stop"

try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
} catch {
    # Ignore si la session ne le permet pas.
}

# Blocklist : ces clés ne doivent JAMAIS finir dans un fichier de config
# staging ou prod. Comptes démo, tests opt-in, dérogations locales dev.
$Blocklist = @(
    "DEMO_PORTAL_EMAIL",
    "DEMO_PORTAL_PASSWORD",
    "DEMO_INTERNAL_ADMIN_EMAIL",
    "DEMO_INTERNAL_ADMIN_PASSWORD",
    "RUN_MARIADB_TESTS",
    "ALLOW_LOCAL_INTERNAL_API_URL",
    # L'environnement se passe via --environment en CLI du service,
    # pas dans le config file (résolu avant la lecture du fichier par
    # ASP.NET Core).
    "ASPNETCORE_ENVIRONMENT",
    "DOTNET_ENVIRONMENT",
    # KERMARIA_CONFIG_PATH pointerait sur lui-même, non-sens.
    "KERMARIA_CONFIG_PATH",
    # Chemin machine-spécifique — le poste de dev n'a pas la même
    # arborescence que la machine cible. On force un default sensé
    # plus bas.
    "LOG_FILE_DIRECTORY"
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
        Write-Error "Aucun fichier source trouvé automatiquement. Chemins essayés :"
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

# Default du chemin logs cote machine cible (blocklisté depuis le source).
# La cible standard du runbook est C:\apps\api-internal\logs.
if (-not $extracted.Contains("LOG_FILE_DIRECTORY")) {
    $extracted["LOG_FILE_DIRECTORY"] = "C:\apps\api-internal\logs"
}

# Overrides host-specifiques (ex. -Override @{ SQL_HOST = "192.168.100.207" }).
# Appliques APRES extraction et default : ils gagnent sur la valeur du source.
# Permet de cibler la topologie staging/prod sans editer le .local.env.ps1 de dev.
$overridden = @()
foreach ($key in @($Override.Keys)) {
    $name = [string]$key
    if ($Blocklist -contains $name) {
        throw "Cle '$name' passee en -Override mais blocklistee (DEMO_*/dev/env) : refus."
    }
    $extracted[$name] = [string]$Override[$key]
    $overridden += $name
}

Write-Host "Cles extraites : $($extracted.Count)"
foreach ($k in $extracted.Keys) {
    Write-Host "  [OK]      $k"
}
if ($emptyValues.Count -gt 0) {
    Write-Host ""
    Write-Host "Cles ignorees (valeur vide) : $($emptyValues.Count)"
    foreach ($k in $emptyValues) {
        Write-Host "  [empty]   $k"
    }
}
if ($blocked.Count -gt 0) {
    Write-Host ""
    Write-Host "Cles refusees par blocklist : $($blocked.Count)"
    foreach ($k in $blocked) {
        Write-Host "  [blocked] $k"
    }
}
if ($overridden.Count -gt 0) {
    Write-Host ""
    Write-Host "Cles forcees via -Override : $($overridden.Count)"
    foreach ($k in $overridden) { Write-Host "  [override] $k" }
}
Write-Host ""

if ($extracted.Count -eq 0) {
    throw "Aucune cle exploitable trouvee dans $InputPath. Verifier le format ('`$env:KEY = ""value""')."
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
    Write-Host "Prochaines etapes sur la machine cible :"
    Write-Host "  1. Restreindre les ACL du dossier et du fichier :"
    Write-Host "     icacls '$outputDir' /inheritance:r ``"
    Write-Host "       /grant:r '*S-1-5-32-544:(OI)(CI)F' ``"
    Write-Host "       /grant:r 'svc-kermaria-api:(OI)(CI)RX'"
    Write-Host "     icacls '$OutputPath' /inheritance:r ``"
    Write-Host "       /grant:r '*S-1-5-32-544:F' ``"
    Write-Host "       /grant:r 'svc-kermaria-api:R'"
    Write-Host "  2. (Re)installer le service avec --environment en CLI :"
    Write-Host "     sc.exe create KermariaApiInternal ``"
    Write-Host "       binPath= '\"C:\apps\api-internal\Kermaria.ApiInternal.exe\" --environment Staging --urls http://<IP>:5000' ``"
    Write-Host "       DisplayName= 'Kermaria API Internal' ``"
    Write-Host "       start= auto ``"
    Write-Host "       obj= '.\svc-kermaria-api' password= '<pwd>'"
    Write-Host "  3. Nettoyer les anciennes variables Machine si presentes :"
    Write-Host "     Get-ChildItem Env: | ? Name -match '^(SQL_|BPCE_|PAYPAL_|STRIPE_|SMTP_|SERVICE_AUTH|HCAPTCHA_|LOG_|SESSION_|LOGIN_|AD_|EMAIL_|SIGNUP_|PUBLIC_VITRINE)' | % {"
    Write-Host "       [Environment]::SetEnvironmentVariable(`$_.Name, `$null, 'Machine') }"
    Write-Host "  4. Restart-Service KermariaApiInternal ; Invoke-RestMethod http://<IP>:5000/health/ready"
}
