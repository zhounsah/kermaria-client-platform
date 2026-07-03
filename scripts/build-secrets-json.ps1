<#
.SYNOPSIS
Génère api-internal.secrets.json à partir d'un fichier PowerShell .env.

.DESCRIPTION
Extrait les valeurs `$env:KEY = "value"` d'un fichier PowerShell (typiquement
.local.env.ps1) et produit un JSON plat lisible par la source Configuration
de l'API (Program.cs charge C:\ProgramData\Kermaria\api-internal.secrets.json
par défaut).

Seules les clés de l'allowlist sont extraites, pour éviter de fuiter des
variables non-sensibles (SQL_HOST, modes, etc.) ou de dev (DEMO_*).

Ne dot-source PAS le fichier source (parsing regex uniquement) pour ne pas
exécuter du code arbitraire.

Aucune valeur de secret n'est affichée à la console — seuls les noms de
clés trouvées/manquantes sont listés.

.PARAMETER InputPath
Fichier .ps1 source. Si non fourni, cherche automatiquement dans :
  1. <repo>/.local.env.ps1
  2. <repo-parent>/<repo-name>.local.env.ps1
  3. <repo-parent>/.local.env.ps1

.PARAMETER OutputPath
Fichier JSON destination. Défaut :
`C:\ProgramData\Kermaria\api-internal.secrets.json`.

.PARAMETER WhatIf
Affiche les clés qui seraient extraites sans écrire le fichier.

.EXAMPLE
.\scripts\build-secrets-json.ps1

.EXAMPLE
.\scripts\build-secrets-json.ps1 -InputPath D:\secrets\dev.env.ps1 `
  -OutputPath D:\deploy\api-internal.secrets.json

.EXAMPLE
.\scripts\build-secrets-json.ps1 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$InputPath,
    [string]$OutputPath = "C:\ProgramData\Kermaria\api-internal.secrets.json"
)

# Auto-detection du fichier source si non fourni. Cherche dans l'ordre :
#   1. <repo>/.local.env.ps1                              (dans le repo, caché)
#   2. <repo-parent>/<repo-name>.local.env.ps1            (à côté du repo, préfixé)
#   3. <repo-parent>/.local.env.ps1                       (à côté du repo, caché)
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

$ErrorActionPreference = "Stop"

# Force la console en UTF-8 pour éviter les accents mangled sur cp850.
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
} catch {
    # Ignore si la session ne le permet pas.
}

# Allowlist stricte : seules ces clés sont extraites. Doit correspondre à
# la structure documentée dans docs/DEPLOYMENT_WINDOWS.md.
$Allowlist = @(
    "SQL_PASSWORD",
    "SERVICE_AUTH_TOKEN",
    "BPCE_REFRESH_TOKEN",
    "PAYPAL_CLIENT_SECRET",
    "STRIPE_SECRET_KEY",
    "STRIPE_WEBHOOK_SECRET",
    "SMTP_PASSWORD",
    "HCAPTCHA_SECRET_KEY"
)

if (-not (Test-Path -LiteralPath $InputPath)) {
    throw "Fichier source introuvable : $InputPath"
}

Write-Host "Source : $InputPath"
Write-Host "Cible  : $OutputPath"
Write-Host ""

$extracted = [ordered]@{}

# Regex : $env:KEY = "value" ou $env:KEY = 'value'
# Ignore les commentaires # en début de ligne. Le groupe 3 capture la valeur
# entre les quotes du même type que le groupe 2.
$pattern = '^\s*\$env:([A-Z_][A-Z0-9_]*)\s*=\s*([''"])(.*)\2\s*(?:#.*)?\s*$'

Get-Content -LiteralPath $InputPath | ForEach-Object {
    if ($_ -match $pattern) {
        $key = $Matches[1]
        $value = $Matches[3]
        if ($Allowlist -contains $key) {
            $extracted[$key] = $value
        }
    }
}

# Rapport (noms uniquement, jamais les valeurs)
Write-Host "Clés extraites ($($extracted.Count) / $($Allowlist.Count)) :"
foreach ($k in $Allowlist) {
    if ($extracted.Contains($k)) {
        Write-Host "  [OK]      $k"
    } else {
        Write-Host "  [absent]  $k"
    }
}
Write-Host ""

if ($extracted.Count -eq 0) {
    throw "Aucune clé de l'allowlist trouvée dans $InputPath. Vérifier le format ('`$env:KEY = ""value""')."
}

$json = $extracted | ConvertTo-Json -Depth 1
$outputDir = Split-Path -Parent $OutputPath

if ($PSCmdlet.ShouldProcess($OutputPath, "Écrire le fichier de secrets")) {
    if (-not (Test-Path -LiteralPath $outputDir)) {
        New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
        Write-Host "Dossier créé : $outputDir"
    }
    # UTF-8 sans BOM pour compat cross-platform ; JsonConfigurationSource
    # de .NET accepte les deux mais évitons les surprises.
    [System.IO.File]::WriteAllText(
        $OutputPath,
        $json,
        [System.Text.UTF8Encoding]::new($false))
    Write-Host ""
    Write-Host "Fichier écrit : $OutputPath"
    Write-Host ""
    Write-Host "Prochaines étapes :"
    Write-Host "  1. Restreindre les ACL du dossier et du fichier :"
    Write-Host "     icacls '$outputDir' /inheritance:r ``"
    Write-Host "       /grant:r '*S-1-5-32-544:(OI)(CI)F' ``"
    Write-Host "       /grant:r 'svc-kermaria-api:(OI)(CI)RX'"
    Write-Host "     icacls '$OutputPath' /inheritance:r ``"
    Write-Host "       /grant:r '*S-1-5-32-544:F' ``"
    Write-Host "       /grant:r 'svc-kermaria-api:R'"
    Write-Host "  2. Retirer les mêmes secrets des variables Machine :"
    foreach ($k in $extracted.Keys) {
        Write-Host "     [Environment]::SetEnvironmentVariable('$k',`$null,'Machine')"
    }
    Write-Host "  3. Restart-Service KermariaApiInternal"
}
