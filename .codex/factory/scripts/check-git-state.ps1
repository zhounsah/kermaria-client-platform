[CmdletBinding()]
param(
    [ValidateSet("Bootstrap", "Resume", "Work", "Ready", "PostCommit", "FinalAudit")]
    [string]$Mode = "Resume",
    [string]$PhaseId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Git {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $output = @(& git @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') a échoué : $($output -join [Environment]::NewLine)"
    }

    return $output
}

function Read-PhaseDefinition {
    param(
        [Parameter(Mandatory)][string]$FactoryRoot,
        [Parameter(Mandatory)][string]$Id
    )

    $file = Get-ChildItem -LiteralPath (Join-Path $FactoryRoot "phases") -Filter "$Id-*.md" |
        Select-Object -First 1
    if ($null -eq $file) {
        throw "Définition de phase introuvable pour $Id."
    }

    $content = [IO.File]::ReadAllText($file.FullName)
    $match = [regex]::Match(
        $content,
        '(?s)```factory-phase\s*(\{.*?\})\s*```')
    if (-not $match.Success) {
        throw "Bloc factory-phase absent ou invalide dans $($file.FullName)."
    }

    return $match.Groups[1].Value | ConvertFrom-Json
}

function Test-AllowedPath {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][object[]]$Patterns
    )

    $normalized = $Path.Replace('\', '/')
    foreach ($patternValue in $Patterns) {
        $pattern = ([string]$patternValue).Replace('\', '/')
        if ($normalized -like $pattern) {
            return $true
        }
    }

    return $false
}

$factoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$statePath = Join-Path $factoryRoot "STATE.json"
$repoRoot = (Invoke-Git -Arguments @("rev-parse", "--show-toplevel") | Select-Object -First 1).Trim()
$expectedRoot = [IO.Path]::GetFullPath((Join-Path $factoryRoot "..\.."))
if ([IO.Path]::GetFullPath($repoRoot) -ne $expectedRoot) {
    throw "Racine Git inattendue : $repoRoot ; attendu : $expectedRoot."
}

$state = [IO.File]::ReadAllText($statePath) | ConvertFrom-Json
$branch = (Invoke-Git -Arguments @("branch", "--show-current") | Select-Object -First 1).Trim()
if ([string]::IsNullOrWhiteSpace($branch)) {
    throw "HEAD détachée : l'usine refuse de continuer."
}
if ($branch -ne $state.repository.expectedBranch) {
    throw "Branche inattendue : $branch ; attendue : $($state.repository.expectedBranch)."
}
if ($branch -eq $state.repository.snapshotRef) {
    throw "La branche de sauvegarde ne peut jamais être la branche de travail."
}

& git merge-base --is-ancestor $state.repository.baseCommit HEAD 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "Le commit de base n'est pas un ancêtre de HEAD."
}

if (-not [string]::IsNullOrWhiteSpace([string]$state.lastValidatedCommit)) {
    & git merge-base --is-ancestor $state.lastValidatedCommit HEAD 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Le dernier commit validé n'est pas un ancêtre de HEAD."
    }
}

$effectivePhaseId = if ([string]::IsNullOrWhiteSpace($PhaseId)) {
    [string]$state.currentPhase
} else {
    $PhaseId
}

$allowedPatterns = @(".codex/factory/STATE.json")
if ($Mode -eq "Bootstrap") {
    $allowedPatterns += @(".codex/factory/*", ".codex/factory/**", ".codex/agents/*", ".codex/agents/**")
} elseif ($Mode -in @("Work", "Ready")) {
    if ([string]::IsNullOrWhiteSpace($effectivePhaseId)) {
        throw "Une phase est obligatoire en mode $Mode."
    }
    $definition = Read-PhaseDefinition -FactoryRoot $factoryRoot -Id $effectivePhaseId
    $allowedPatterns += @($definition.allowedPaths)
}

$entries = @()
$unexpected = @()
$staged = @()
$unmerged = @()
$statusLines = @(Invoke-Git -Arguments @("status", "--porcelain=v1", "--untracked-files=all"))
foreach ($lineValue in $statusLines) {
    $line = [string]$lineValue
    if ($line.Length -lt 4) {
        continue
    }

    $xy = $line.Substring(0, 2)
    $path = $line.Substring(3).Trim('"')
    if ($path.Contains(" -> ")) {
        $path = ($path -split " -> ")[-1].Trim('"')
    }
    $path = $path.Replace('\', '/')
    $entries += [pscustomobject]@{ status = $xy; path = $path }

    if ($xy -match "U" -or $xy -in @("AA", "DD")) {
        $unmerged += $path
    }
    if ($xy[0] -ne ' ' -and $xy[0] -ne '?') {
        $staged += $path
    }
    if (-not (Test-AllowedPath -Path $path -Patterns $allowedPatterns)) {
        $unexpected += $path
    }
}

if ($unmerged.Count -gt 0) {
    throw "Conflits Git détectés : $($unmerged -join ', ')."
}
if ($staged.Count -gt 0) {
    throw "L'index doit être vide avant cette étape : $($staged -join ', ')."
}
if ($unexpected.Count -gt 0) {
    throw "Fichiers hors périmètre en mode $Mode : $($unexpected -join ', ')."
}

if ($Mode -eq "Ready") {
    $phaseChanges = @($entries | Where-Object { $_.path -ne ".codex/factory/STATE.json" })
    if ($phaseChanges.Count -eq 0) {
        throw "Aucun changement de phase à committer."
    }
}

[pscustomobject]@{
    ok = $true
    mode = $Mode
    repositoryRoot = $repoRoot.Replace('\', '/')
    branch = $branch
    head = (Invoke-Git -Arguments @("rev-parse", "HEAD") | Select-Object -First 1).Trim()
    currentPhase = $effectivePhaseId
    changedPaths = @($entries | ForEach-Object { $_.path })
    managedStateDirty = [bool](@($entries | Where-Object { $_.path -eq ".codex/factory/STATE.json" }).Count)
} | ConvertTo-Json -Depth 6
