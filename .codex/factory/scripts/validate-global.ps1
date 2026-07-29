[CmdletBinding()]
param(
    [switch]$FactoryOnly,
    [switch]$Bootstrap
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-PhaseDefinition {
    param([Parameter(Mandatory)][IO.FileInfo]$File)

    $content = [IO.File]::ReadAllText($File.FullName)
    $match = [regex]::Match($content, '(?s)```factory-phase\s*(\{.*?\})\s*```')
    if (-not $match.Success) {
        throw "Bloc factory-phase absent dans $($File.FullName)."
    }
    return $match.Groups[1].Value | ConvertFrom-Json
}

function Invoke-ValidationCommand {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    Write-Host "[GLOBAL] ${Name}: $Executable $($Arguments -join ' ')"
    & $Executable @Arguments
    return $LASTEXITCODE
}

$factoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$repoRoot = [IO.Path]::GetFullPath((Join-Path $factoryRoot "..\.."))
$statePath = Join-Path $factoryRoot "STATE.json"
$state = [IO.File]::ReadAllText($statePath) | ConvertFrom-Json

if ($state.schemaVersion -ne 1) {
    throw "Version de schéma STATE.json non supportée : $($state.schemaVersion)."
}

$requiredStateProperties = @(
    "repository", "runStatus", "currentPhase", "currentStep", "attempts",
    "completedPhases", "lastValidatedCommit", "blocker", "interruption",
    "finalAudit", "phases", "history")
foreach ($property in $requiredStateProperties) {
    if ($property -notin $state.PSObject.Properties.Name) {
        throw "Propriété STATE.json manquante : $property."
    }
}

$phaseFiles = @(Get-ChildItem -LiteralPath (Join-Path $factoryRoot "phases") -Filter "P*.md" | Sort-Object Name)
$definitions = @($phaseFiles | ForEach-Object { Read-PhaseDefinition -File $_ })
$definitionIds = @($definitions | Sort-Object order | ForEach-Object { $_.id })
$stateIds = @($state.phases | ForEach-Object { $_.id })
if (($definitionIds -join ",") -ne ($stateIds -join ",")) {
    throw "Les phases de STATE.json et du dossier phases ne concordent pas."
}
if (@($definitionIds | Select-Object -Unique).Count -ne $definitionIds.Count) {
    throw "Identifiants de phase dupliqués."
}
if (@($definitions.order | Select-Object -Unique).Count -ne $definitions.Count) {
    throw "Ordres de phase dupliqués."
}

foreach ($definition in $definitions) {
    if (@($definition.allowedPaths).Count -eq 0 -or @($definition.validations).Count -eq 0) {
        throw "Définition incomplète : $($definition.id)."
    }
    foreach ($dependency in @($definition.dependencies)) {
        if ($dependency -notin $definitionIds) {
            throw "Dépendance inconnue $dependency dans $($definition.id)."
        }
        $dependencyOrder = ($definitions | Where-Object { $_.id -eq $dependency }).order
        if ([int]$dependencyOrder -ge [int]$definition.order) {
            throw "Dépendance non antérieure $dependency dans $($definition.id)."
        }
    }
}

$doneIds = @($state.phases | Where-Object { $_.status -eq "DONE" } | ForEach-Object { $_.id })
$completedIds = @($state.completedPhases | ForEach-Object { $_.id })
if (($doneIds -join ",") -ne ($completedIds -join ",")) {
    throw "completedPhases ne correspond pas aux phases DONE."
}

$pendingOrActive = @($state.phases | Where-Object { $_.status -ne "DONE" })
if ($null -ne $state.currentPhase) {
    if ($pendingOrActive.Count -eq 0 -or $pendingOrActive[0].id -ne $state.currentPhase) {
        throw "currentPhase n'est pas la première phase non terminée."
    }
}

$requiredRoles = @(
    "analyst.md", "test-designer.md", "security-reviewer.md", "implementer.md",
    "integrator.md", "code-reviewer.md", "qa-engineer.md", "fixer.md",
    "final-auditor.md")
$agentsRoot = Join-Path $factoryRoot "..\agents"
foreach ($role in $requiredRoles) {
    if (-not (Test-Path -LiteralPath (Join-Path $agentsRoot $role) -PathType Leaf)) {
        throw "Rôle manquant : $role."
    }
}

$requiredScripts = @(
    "check-git-state.ps1", "validate-phase.ps1", "validate-global.ps1",
    "update-state.ps1")
foreach ($script in $requiredScripts) {
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $script) -PathType Leaf)) {
        throw "Script manquant : $script."
    }
}

Push-Location $repoRoot
try {
    & git cat-file -e "$($state.repository.baseCommit)^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Commit de base introuvable."
    }
    $snapshotCommit = (& git rev-parse $state.repository.snapshotRef).Trim()
    if ($LASTEXITCODE -ne 0 -or $snapshotCommit -ne $state.repository.snapshotCommit) {
        throw "La référence historique ne pointe plus sur le commit enregistré."
    }
    & git cat-file -e "$($state.lastValidatedCommit)^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Dernier commit validé introuvable."
    }
} finally {
    Pop-Location
}

$gitMode = if ($Bootstrap) { "Bootstrap" } elseif ($FactoryOnly) { "Resume" } else { "FinalAudit" }
& (Join-Path $PSScriptRoot "check-git-state.ps1") -Mode $gitMode
if ($LASTEXITCODE -ne 0) {
    throw "Le contrôle Git global a échoué."
}

$results = @()
$failed = $false
Push-Location $repoRoot
try {
    $commands = @(
        [pscustomobject]@{ name = "diff-check"; executable = "git"; arguments = @("diff", "--check") }
    )
    if (-not $FactoryOnly) {
        if (@($state.phases | Where-Object { $_.status -ne "DONE" }).Count -ne 0) {
            throw "L'audit global complet exige toutes les phases DONE."
        }
        $commands = @(
            [pscustomobject]@{ name = "validate"; executable = "npm.cmd"; arguments = @("run", "validate") },
            [pscustomobject]@{ name = "downloads"; executable = "npm.cmd"; arguments = @("run", "test:downloads") },
            [pscustomobject]@{ name = "payments"; executable = "npm.cmd"; arguments = @("run", "test:payments") },
            [pscustomobject]@{ name = "subscriptions"; executable = "npm.cmd"; arguments = @("run", "test:subscriptions") },
            [pscustomobject]@{ name = "stripe"; executable = "npm.cmd"; arguments = @("run", "test:payments-stripe") },
            [pscustomobject]@{ name = "timezone"; executable = "npm.cmd"; arguments = @("run", "test:timezone") },
            [pscustomobject]@{ name = "signup"; executable = "npm.cmd"; arguments = @("run", "test:signup") },
            [pscustomobject]@{ name = "email-live-contract"; executable = "npm.cmd"; arguments = @("run", "test:email-live") },
            [pscustomobject]@{ name = "diff-check"; executable = "git"; arguments = @("diff", "--check") }
        )
    }

    foreach ($command in $commands) {
        $exitCode = Invoke-ValidationCommand `
            -Name $command.name `
            -Executable $command.executable `
            -Arguments @($command.arguments)
        $results += [pscustomobject]@{
            name = $command.name
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
    factoryOnly = [bool]$FactoryOnly
    bootstrap = [bool]$Bootstrap
    phaseCount = $definitions.Count
    roleCount = $requiredRoles.Count
    currentPhase = $state.currentPhase
    results = $results
} | ConvertTo-Json -Depth 8

if ($failed) {
    exit 1
}
