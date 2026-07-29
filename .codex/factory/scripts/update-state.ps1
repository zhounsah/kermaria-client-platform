[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet(
        "StartPhase", "SetStep", "RecordQaFailure", "RecordQaPass",
        "CompletePhase", "Block", "ClearBlocker", "Interrupt", "Resume",
        "RecordFinalAuditPass", "Deliver")]
    [string]$Action,
    [string]$PhaseId,
    [ValidateSet("PRECHECK", "ANALYSIS", "PLAN", "PRODUCTION", "INTEGRATION", "QA", "FIX", "RE_QA", "READY", "COMMIT", "ADVANCE", "FINAL_AUDIT", "DELIVERY", "DELIVERED")]
    [string]$Step,
    [string]$Fingerprint,
    [switch]$Progress,
    [string]$Reason,
    [string]$GateCode,
    [string]$Commit,
    [string]$Report,
    [switch]$ValidateOnly
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
    $files = @(Get-ChildItem -LiteralPath (Join-Path $FactoryRoot "phases") -Filter "$Id-*.md")
    if ($files.Count -ne 1) {
        throw "Définition de phase introuvable ou dupliquée pour $Id."
    }
    $content = [IO.File]::ReadAllText($files[0].FullName)
    $match = [regex]::Match($content, '(?s)```factory-phase\s*(\{.*?\})\s*```')
    if (-not $match.Success) {
        throw "Bloc factory-phase absent pour $Id."
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
        if ($normalized -like ([string]$patternValue).Replace('\', '/')) {
            return $true
        }
    }
    return $false
}

function Reset-Blocker {
    param([Parameter(Mandatory)]$State)
    $State.blocker.active = $false
    $State.blocker.type = $null
    $State.blocker.code = $null
    $State.blocker.phase = $null
    $State.blocker.step = $null
    $State.blocker.reason = $null
    $State.blocker.fingerprint = $null
    $State.blocker.since = $null
}

function Add-HistoryEntry {
    param(
        [Parameter(Mandatory)]$State,
        [Parameter(Mandatory)][string]$HistoryAction,
        [AllowNull()][string]$HistoryPhase,
        [string]$Detail
    )
    $entry = [pscustomobject]@{
        at = (Get-Date).ToUniversalTime().ToString("o")
        action = $HistoryAction
        phase = $HistoryPhase
        step = $State.currentStep
        detail = $Detail
    }
    $State.history = @($State.history) + @($entry)
}

$factoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$repoRoot = [IO.Path]::GetFullPath((Join-Path $factoryRoot "..\.."))
$statePath = [IO.Path]::GetFullPath((Join-Path $factoryRoot "STATE.json"))
$expectedStatePath = [IO.Path]::GetFullPath((Join-Path $repoRoot ".codex\factory\STATE.json"))
if ($statePath -ne $expectedStatePath) {
    throw "Chemin STATE.json inattendu."
}

$state = [IO.File]::ReadAllText($statePath) | ConvertFrom-Json
$effectivePhaseId = if ([string]::IsNullOrWhiteSpace($PhaseId)) {
    [string]$state.currentPhase
} else {
    $PhaseId
}
$phaseState = if ([string]::IsNullOrWhiteSpace($effectivePhaseId)) {
    $null
} else {
    $state.phases | Where-Object { $_.id -eq $effectivePhaseId } | Select-Object -First 1
}
if ($null -ne $phaseState -and $effectivePhaseId -ne $state.currentPhase) {
    throw "Seule la phase courante $($state.currentPhase) peut être mise à jour."
}

$now = (Get-Date).ToUniversalTime().ToString("o")
$historyDetail = $null
$historyPhase = $effectivePhaseId

switch ($Action) {
    "StartPhase" {
        if ($state.blocker.active -or $state.interruption.active) {
            throw "Impossible de démarrer avec un blocker ou une interruption active."
        }
        if ($null -eq $phaseState -or $phaseState.status -ne "PENDING") {
            throw "La phase courante doit être PENDING."
        }
        $definition = Read-PhaseDefinition -FactoryRoot $factoryRoot -Id $effectivePhaseId
        foreach ($dependency in @($definition.dependencies)) {
            $dependencyState = $state.phases | Where-Object { $_.id -eq $dependency } | Select-Object -First 1
            if ($dependencyState.status -ne "DONE") {
                throw "Dépendance non terminée : $dependency."
            }
        }
        $phaseState.status = "ACTIVE"
        $phaseState.startedAt = $now
        $state.runStatus = "RUNNING"
        $state.currentStep = "ANALYSIS"
        $historyDetail = "phase démarrée"
    }
    "SetStep" {
        if ([string]::IsNullOrWhiteSpace($Step)) {
            throw "-Step est obligatoire."
        }
        if ($state.blocker.active -or $state.interruption.active) {
            throw "Impossible de changer d'étape avec un arrêt actif."
        }
        if ($null -eq $phaseState -or $phaseState.status -notin @("ACTIVE", "FIXING", "QA_FAILED")) {
            throw "Statut de phase incompatible avec SetStep."
        }
        $state.currentStep = $Step
        $historyDetail = "étape définie sur $Step"
    }
    "RecordQaFailure" {
        if ([string]::IsNullOrWhiteSpace($Fingerprint)) {
            throw "-Fingerprint est obligatoire et ne doit contenir aucune donnée sensible."
        }
        if ($null -eq $phaseState -or $state.currentStep -notin @("QA", "RE_QA")) {
            throw "RecordQaFailure exige une QA en cours."
        }
        $state.attempts.qaRuns = [int]$state.attempts.qaRuns + 1
        $state.attempts.correctionCycles = [int]$state.attempts.correctionCycles + 1
        $hadPrevious = -not [string]::IsNullOrWhiteSpace([string]$state.attempts.lastFailureFingerprint)
        if ($Progress) {
            $state.attempts.noProgressCycles = 0
        } elseif ($hadPrevious) {
            $state.attempts.noProgressCycles = [int]$state.attempts.noProgressCycles + 1
        } else {
            $state.attempts.noProgressCycles = 0
        }
        $state.attempts.lastFailureFingerprint = $Fingerprint

        if ([int]$state.attempts.noProgressCycles -ge 3) {
            $phaseState.status = "BLOCKED"
            $state.runStatus = "BLOCKED"
            $state.currentStep = "FIX"
            $state.blocker.active = $true
            $state.blocker.type = "TECHNICAL"
            $state.blocker.code = "QA_NO_PROGRESS_LIMIT"
            $state.blocker.phase = $effectivePhaseId
            $state.blocker.step = "FIX"
            $state.blocker.reason = "Trois cycles consécutifs de correction sans progrès démontré."
            $state.blocker.fingerprint = $Fingerprint
            $state.blocker.since = $now
            $historyDetail = "limite de correction sans progrès atteinte"
        } else {
            $phaseState.status = "FIXING"
            $state.currentStep = "FIX"
            $historyDetail = "QA en échec ; correction requise"
        }
    }
    "RecordQaPass" {
        if ($null -eq $phaseState -or $state.currentStep -notin @("QA", "RE_QA")) {
            throw "RecordQaPass exige une QA en cours."
        }
        $state.attempts.qaRuns = [int]$state.attempts.qaRuns + 1
        $phaseState.status = "READY"
        $state.currentStep = "READY"
        $historyDetail = "QA réussie ; phase prête à committer"
    }
    "CompletePhase" {
        if ($null -eq $phaseState -or $phaseState.status -ne "READY" -or $state.currentStep -ne "READY") {
            throw "CompletePhase exige une phase READY."
        }
        $definition = Read-PhaseDefinition -FactoryRoot $factoryRoot -Id $effectivePhaseId
        if (-not $definition.requiresCommit) {
            throw "Les phases de cette feuille de route exigent un commit."
        }

        Push-Location $repoRoot
        try {
            $resolvedCommit = if ([string]::IsNullOrWhiteSpace($Commit)) {
                (Invoke-Git -Arguments @("rev-parse", "HEAD") | Select-Object -First 1).Trim()
            } else {
                (Invoke-Git -Arguments @("rev-parse", "$Commit^{commit}") | Select-Object -First 1).Trim()
            }
            $headCommit = (Invoke-Git -Arguments @("rev-parse", "HEAD") | Select-Object -First 1).Trim()
            if ($resolvedCommit -ne $headCommit) {
                throw "CompletePhase doit enregistrer HEAD, pas un commit antérieur."
            }
            if ($resolvedCommit -eq $state.lastValidatedCommit) {
                throw "HEAD n'a pas avancé depuis le dernier commit validé."
            }
            & git merge-base --is-ancestor $state.lastValidatedCommit $resolvedCommit 2>$null
            if ($LASTEXITCODE -ne 0) {
                throw "Le commit de phase n'est pas descendant du dernier commit validé."
            }
            $subject = (Invoke-Git -Arguments @("show", "-s", "--format=%s", $resolvedCommit) | Select-Object -First 1).Trim()
            if ($subject -ne $definition.commitMessage) {
                throw "Message de commit inattendu : '$subject' ; attendu : '$($definition.commitMessage)'."
            }
            $changedPaths = @(Invoke-Git -Arguments @("diff-tree", "--no-commit-id", "--name-only", "-r", $resolvedCommit) |
                ForEach-Object { ([string]$_).Replace('\', '/') } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            if ($changedPaths.Count -eq 0) {
                throw "Le commit de phase est vide."
            }
            $invalidPaths = @($changedPaths | Where-Object {
                -not (Test-AllowedPath -Path $_ -Patterns @($definition.allowedPaths))
            })
            if ($invalidPaths.Count -gt 0) {
                throw "Le commit contient des fichiers hors phase : $($invalidPaths -join ', ')."
            }
            $runtimeChanges = @(Invoke-Git -Arguments @("status", "--porcelain=v1", "--untracked-files=all"))
            foreach ($lineValue in $runtimeChanges) {
                $line = [string]$lineValue
                if ($line.Length -lt 4) { continue }
                $xy = $line.Substring(0, 2)
                $path = $line.Substring(3).Trim('"').Replace('\', '/')
                if ($path -ne ".codex/factory/STATE.json") {
                    throw "Après commit, seul STATE.json peut rester modifié : $path."
                }
                if ($xy[0] -ne ' ' -and $xy[0] -ne '?') {
                    throw "STATE.json ne doit pas rester indexé après le commit de phase."
                }
            }
        } finally {
            Pop-Location
        }

        $phaseState.status = "DONE"
        $phaseState.commit = $resolvedCommit
        $phaseState.completedAt = $now
        $state.completedPhases = @($state.completedPhases) + @([pscustomobject]@{
            id = $effectivePhaseId
            commits = @($resolvedCommit)
            completedAt = $now
        })
        $state.lastValidatedCommit = $resolvedCommit
        $state.attempts.qaRuns = 0
        $state.attempts.correctionCycles = 0
        $state.attempts.noProgressCycles = 0
        $state.attempts.lastFailureFingerprint = $null

        $nextPhase = $state.phases | Where-Object { $_.status -ne "DONE" } | Select-Object -First 1
        if ($null -eq $nextPhase) {
            $state.currentPhase = $null
            $state.currentStep = "FINAL_AUDIT"
            $state.runStatus = "FINAL_AUDIT"
            $historyDetail = "dernière phase committée ; audit final démarré"
        } else {
            $state.currentPhase = $nextPhase.id
            $state.currentStep = "PRECHECK"
            $state.runStatus = "READY_TO_RUN"
            $historyDetail = "phase committée ; avance automatique vers $($nextPhase.id)"
        }
    }
    "Block" {
        if ([string]::IsNullOrWhiteSpace($Reason)) {
            throw "-Reason est obligatoire et doit être assaini."
        }
        if ($null -eq $phaseState) {
            throw "Une phase courante est obligatoire."
        }
        $validGateCodes = @(
            "HG-GIT-REMOTE", "HG-DEPLOY", "HG-AD-REAL", "HG-MARIADB-REAL",
            "HG-KOXO", "HG-NETWORK", "HG-SECRET", "HG-PUBLIC-CONTRACT",
            "HG-PROD-DEPENDENCY", "HG-DESTRUCTIVE-MIGRATION", "HG-LEGAL",
            "HG-BUSINESS")
        if (-not [string]::IsNullOrWhiteSpace($GateCode) -and $GateCode -notin $validGateCodes) {
            throw "Code de porte humaine inconnu : $GateCode."
        }
        $isHumanGate = -not [string]::IsNullOrWhiteSpace($GateCode)
        $phaseState.status = if ($isHumanGate) { "HUMAN_GATE" } else { "BLOCKED" }
        $state.runStatus = $phaseState.status
        $state.blocker.active = $true
        $state.blocker.type = if ($isHumanGate) { "HUMAN_GATE" } else { "TECHNICAL" }
        $state.blocker.code = if ($isHumanGate) { $GateCode } else { "TECHNICAL_BLOCKER" }
        $state.blocker.phase = $effectivePhaseId
        $state.blocker.step = $state.currentStep
        $state.blocker.reason = $Reason
        $state.blocker.fingerprint = $Fingerprint
        $state.blocker.since = $now
        $historyDetail = "arrêt enregistré : $($state.blocker.code)"
    }
    "ClearBlocker" {
        if (-not $state.blocker.active) {
            throw "Aucun blocker actif."
        }
        if ([string]::IsNullOrWhiteSpace($Reason)) {
            throw "-Reason doit référencer la preuve ou la décision de résolution."
        }
        if ($null -ne $phaseState) {
            $phaseState.status = "PENDING"
        }
        Reset-Blocker -State $state
        $state.runStatus = "READY_TO_RUN"
        $state.currentStep = "PRECHECK"
        $historyDetail = "blocker résolu : $Reason"
    }
    "Interrupt" {
        if ([string]::IsNullOrWhiteSpace($Reason)) {
            throw "-Reason est obligatoire et doit être assaini."
        }
        if ($state.interruption.active) {
            throw "Une interruption est déjà active."
        }
        if ($state.blocker.active) {
            throw "Un blocker actif est déjà un checkpoint persistant ; ne pas ajouter d'interruption."
        }
        $state.interruption.active = $true
        $state.interruption.reason = $Reason
        $state.interruption.at = $now
        $state.interruption.previousRunStatus = $state.runStatus
        $state.interruption.previousStep = $state.currentStep
        $state.runStatus = "INTERRUPTED"
        $historyDetail = "interruption enregistrée"
    }
    "Resume" {
        if (-not $state.interruption.active) {
            throw "Aucune interruption active."
        }
        if ($state.blocker.active) {
            throw "Le blocker actif doit être résolu avant la reprise."
        }
        $state.runStatus = $state.interruption.previousRunStatus
        $state.currentStep = $state.interruption.previousStep
        $state.interruption.active = $false
        $state.interruption.reason = $null
        $state.interruption.at = $null
        $state.interruption.previousRunStatus = $null
        $state.interruption.previousStep = $null
        $historyDetail = "exécution reprise au checkpoint"
    }
    "RecordFinalAuditPass" {
        if ($state.runStatus -ne "FINAL_AUDIT" -or $state.currentStep -ne "FINAL_AUDIT") {
            throw "L'usine n'est pas en audit final."
        }
        if (@($state.phases | Where-Object { $_.status -ne "DONE" }).Count -gt 0 -or $state.blocker.active) {
            throw "Toutes les phases doivent être DONE et sans blocker."
        }
        $state.finalAudit.status = "PASSED"
        $state.finalAudit.validatedAt = $now
        $state.finalAudit.report = $Report
        $state.runStatus = "DELIVERY"
        $state.currentStep = "DELIVERY"
        $historyDetail = "audit final réussi"
    }
    "Deliver" {
        if ($state.runStatus -ne "DELIVERY" -or $state.finalAudit.status -ne "PASSED") {
            throw "La livraison exige un audit final réussi."
        }
        $state.runStatus = "DELIVERED"
        $state.currentStep = "DELIVERED"
        $historyDetail = "livraison locale terminée ; aucune publication distante"
    }
}

$state.updatedAt = $now
Add-HistoryEntry -State $state -HistoryAction $Action -HistoryPhase $historyPhase -Detail $historyDetail
if (-not $ValidateOnly) {
    $json = $state | ConvertTo-Json -Depth 20
    $encoding = New-Object Text.UTF8Encoding($false)
    $tempPath = "$statePath.tmp"
    [IO.File]::WriteAllText($tempPath, $json + [Environment]::NewLine, $encoding)
    Move-Item -LiteralPath $tempPath -Destination $statePath -Force
}

[pscustomobject]@{
    ok = $true
    persisted = (-not $ValidateOnly)
    action = $Action
    runStatus = $state.runStatus
    currentPhase = $state.currentPhase
    currentStep = $state.currentStep
    blockerActive = $state.blocker.active
    lastValidatedCommit = $state.lastValidatedCommit
} | ConvertTo-Json -Depth 6
