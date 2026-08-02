[CmdletBinding()]
param(
    [string]$PhaseId,
    [switch]$DefinitionOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-PhaseDefinition {
    param(
        [Parameter(Mandatory)][string]$FactoryRoot,
        [Parameter(Mandatory)][string]$Id
    )

    $files = @(Get-ChildItem -LiteralPath (Join-Path $FactoryRoot "phases") -Filter "$Id-*.md")
    if ($files.Count -ne 1) {
        throw "Une définition unique est attendue pour $Id ; trouvé : $($files.Count)."
    }

    $content = [IO.File]::ReadAllText($files[0].FullName)
    $match = [regex]::Match($content, '(?s)```factory-phase\s*(\{.*?\})\s*```')
    if (-not $match.Success) {
        throw "Bloc factory-phase absent dans $($files[0].FullName)."
    }

    return $match.Groups[1].Value | ConvertFrom-Json
}

$factoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$repoRoot = [IO.Path]::GetFullPath((Join-Path $factoryRoot "..\.."))
$state = [IO.File]::ReadAllText((Join-Path $factoryRoot "STATE.json")) | ConvertFrom-Json
$effectivePhaseId = if ([string]::IsNullOrWhiteSpace($PhaseId)) {
    [string]$state.currentPhase
} else {
    $PhaseId
}
if ([string]::IsNullOrWhiteSpace($effectivePhaseId)) {
    throw "Aucune phase courante."
}

$definition = Read-PhaseDefinition -FactoryRoot $factoryRoot -Id $effectivePhaseId
if ($definition.id -ne $effectivePhaseId) {
    throw "L'identifiant interne $($definition.id) ne correspond pas à $effectivePhaseId."
}
if (@($definition.allowedPaths).Count -eq 0) {
    throw "allowedPaths est vide pour $effectivePhaseId."
}
if ($definition.requiresCommit -and [string]::IsNullOrWhiteSpace([string]$definition.commitMessage)) {
    throw "Un message de commit est obligatoire pour $effectivePhaseId."
}
if (@($definition.validations).Count -eq 0) {
    throw "Aucune validation n'est déclarée pour $effectivePhaseId."
}

$knownIds = @($state.phases | ForEach-Object { $_.id })
foreach ($dependency in @($definition.dependencies)) {
    if ($dependency -notin $knownIds) {
        throw "Dépendance inconnue $dependency dans $effectivePhaseId."
    }
    $dependencyState = $state.phases | Where-Object { $_.id -eq $dependency } | Select-Object -First 1
    if ($dependencyState.status -ne "DONE") {
        throw "Dépendance non terminée : $dependency ($($dependencyState.status))."
    }
}

foreach ($validation in @($definition.validations)) {
    if ([string]::IsNullOrWhiteSpace([string]$validation.name) -or
        [string]::IsNullOrWhiteSpace([string]$validation.executable)) {
        throw "Validation incomplète dans $effectivePhaseId."
    }
}

if ($DefinitionOnly) {
    [pscustomobject]@{
        ok = $true
        definitionOnly = $true
        phase = $definition.id
        order = $definition.order
        validations = @($definition.validations | ForEach-Object { $_.name })
    } | ConvertTo-Json -Depth 6
    exit 0
}

& (Join-Path $PSScriptRoot "check-git-state.ps1") -Mode Work -PhaseId $effectivePhaseId
if ($LASTEXITCODE -ne 0) {
    throw "Le contrôle Git de phase a échoué."
}

$results = @()
$failed = $false
Push-Location $repoRoot
try {
    foreach ($validation in @($definition.validations)) {
        $arguments = @($validation.arguments | ForEach-Object { [string]$_ })
        Write-Host "[$effectivePhaseId] $($validation.name): $($validation.executable) $($arguments -join ' ')"
        & ([string]$validation.executable) @arguments
        $exitCode = $LASTEXITCODE
        $results += [pscustomobject]@{
            name = [string]$validation.name
            exitCode = $exitCode
            passed = ($exitCode -eq 0)
        }
        if ($exitCode -ne 0) {
            $failed = $true
        }
    }
} finally {
    Pop-Location
}

[pscustomobject]@{
    ok = (-not $failed)
    phase = $effectivePhaseId
    results = $results
} | ConvertTo-Json -Depth 8

if ($failed) {
    exit 1
}
