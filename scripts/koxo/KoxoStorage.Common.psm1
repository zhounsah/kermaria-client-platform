Set-StrictMode -Version Latest

# Reconciliation CIBLEE d'un quota de stockage KoXo.
#
# Ce module ne synchronise jamais le CSV global. La synchronisation globale
# reconcilie TOUTE la branche et, avec DisableOrphanedAccounts, une ligne
# manquante vaut ordre de desactivation : la declencher pour poser un quota
# serait hors de proportion avec l'intention. Les deux chemins restent donc
# separes, jusqu'au verrou qu'ils partagent.
#
# Les fiches KoXo sont en UTF-8 SANS marque d'ordre d'octets et contiennent
# « CLIENTS DEMO » accentue dans leur chemin. Un aller-retour [xml] + .Save()
# sous PowerShell 5.1 reencode le document et casse cet accent : le profil vise
# alors un groupe inexistant et la synchro devient un no-op silencieux (constate
# le 2026-08-06). Toute lecture/ecriture passe donc par des octets bruts et une
# substitution ciblee, jamais par un modele objet XML.

$script:StorageModuleRoot = $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot 'KoxoSync.Common.psm1') -Force -DisableNameChecking

$script:EnableQuotaPattern = '<EnableFolderQuota>\s*(?<value>-?\d+)\s*</EnableFolderQuota>'
$script:QuotaPattern = '<FolderQuota>\s*(?<value>-?\d+)\s*</FolderQuota>'

function Get-KoxoStorageConfiguration {
    [CmdletBinding()]
    param(
        [string]$DataRoot = 'C:\Program Files\KoXo Dev\KoXoAdm\Data',

        [string]$WorkingDirectory = (Join-Path $script:StorageModuleRoot 'work'),

        [hashtable]$Overrides = @{}
    )

    $resolvedDataRoot = [System.IO.Path]::GetFullPath(
        (Get-KoxoSetting -Name 'KOXO_STORAGE_DATA_ROOT' -DefaultValue $DataRoot -Overrides $Overrides)
    )
    $workRoot = [System.IO.Path]::GetFullPath($WorkingDirectory)
    if (-not (Test-Path -LiteralPath $workRoot)) {
        New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
    }

    $logDirectory = [System.IO.Path]::GetFullPath(
        (Get-KoxoSetting -Name 'KOXO_LOG_DIRECTORY' -DefaultValue (Join-Path $workRoot 'logs') -Overrides $Overrides)
    )
    if (-not (Test-Path -LiteralPath $logDirectory)) {
        New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    }

    $backupDirectory = Join-Path $workRoot 'storage-backups'
    if (-not (Test-Path -LiteralPath $backupDirectory)) {
        New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
    }

    $configuration = [ordered]@{
        DataRoot = $resolvedDataRoot
        UsersRoot = Join-Path $resolvedDataRoot 'Users'
        KoxoExecutablePath = Get-KoxoSetting -Name 'KOXO_EXECUTABLE_PATH' -DefaultValue 'C:\Program Files\KoXo Dev\KoXoAdm\KoXoAdm.exe' -Overrides $Overrides
        KoxoWorkingDirectory = Get-KoxoSetting -Name 'KOXO_WORKING_DIRECTORY' -DefaultValue 'C:\Program Files\KoXo Dev\KoXoAdm' -Overrides $Overrides
        # Le journal KoXo est la SEULE preuve d'execution exploitable :
        # KoXoAdm.exe sort en code 1 meme en succes. Sans motif de journal, une
        # reparation ne peut pas etre declaree reussie.
        KoxoLogGlob = Get-KoxoSetting -Name 'KOXO_KOXO_LOG_GLOB' -DefaultValue '' -Overrides $Overrides
        SyncTimeoutSeconds = [int](Get-KoxoSetting -Name 'KOXO_SYNC_TIMEOUT_SECONDS' -DefaultValue '90' -Overrides $Overrides)
        BackupRetentionCount = [int](Get-KoxoSetting -Name 'KOXO_BACKUP_RETENTION_COUNT' -DefaultValue '10' -Overrides $Overrides)
        BackupDirectory = $backupDirectory
        WorkingDirectory = $workRoot
        LogDirectory = $logDirectory
        LogPath = Join-Path $logDirectory ("koxo-storage-{0}.log" -f (Get-Date -Format 'yyyyMMdd'))
        # Verrou COMMUN avec la synchronisation globale, et c'est voulu :
        # KoXoAdm.exe ne supporte pas deux instances concurrentes, quel que soit
        # le chemin qui l'invoque.
        LockPath = Join-Path $logDirectory 'koxo-sync.lock'
        FsrmEnabled = Test-KoxoBooleanSetting -Name 'KOXO_STORAGE_FSRM_ENABLED' -Overrides $Overrides
        FsrmServer = Get-KoxoSetting -Name 'KOXO_STORAGE_FSRM_SERVER' -DefaultValue '' -Overrides $Overrides
        FsrmUserPathTemplate = Get-KoxoSetting -Name 'KOXO_STORAGE_FSRM_USER_PATH_TEMPLATE' -DefaultValue '' -Overrides $Overrides
        FsrmGroupPathTemplate = Get-KoxoSetting -Name 'KOXO_STORAGE_FSRM_GROUP_PATH_TEMPLATE' -DefaultValue '' -Overrides $Overrides
    }

    if ($configuration.SyncTimeoutSeconds -lt 5) {
        throw 'KOXO_SYNC_TIMEOUT_SECONDS must be >= 5.'
    }

    if ($configuration.BackupRetentionCount -lt 1) {
        throw 'KOXO_BACKUP_RETENTION_COUNT must be >= 1.'
    }

    if ($configuration.FsrmEnabled -and
        [string]::IsNullOrWhiteSpace($configuration.FsrmUserPathTemplate) -and
        [string]::IsNullOrWhiteSpace($configuration.FsrmGroupPathTemplate)) {
        throw 'KOXO_STORAGE_FSRM_ENABLED requires at least one FSRM path template.'
    }

    [pscustomobject]$configuration
}

function Test-KoxoStorageNameComponent {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    # Ces valeurs viennent d'une requete distante et servent a composer un
    # chemin de fichier. Un composant vide, un separateur ou un « .. » sortirait
    # de l'arborescence KoXo : le refus est prealable a toute resolution.
    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw ("{0} is required." -f $Name)
    }

    if ($Value -ne $Value.Trim()) {
        throw ("{0} must not be padded with whitespace." -f $Name)
    }

    if ($Value.Contains('\') -or $Value.Contains('/') -or $Value.Contains('..')) {
        throw ("{0} must not contain a path separator." -f $Name)
    }

    $invalid = [System.IO.Path]::GetInvalidFileNameChars()
    foreach ($char in $Value.ToCharArray()) {
        if ($invalid -contains $char) {
            throw ("{0} contains an invalid file name character." -f $Name)
        }
    }

    $Value
}

function Resolve-KoxoStorageTargetPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$UsersRoot,

        [Parameter(Mandatory = $true)]
        [ValidateSet('user', 'secondary_group')]
        [string]$TargetKind,

        [Parameter(Mandatory = $true)]
        [string]$PrimaryGroup,

        [Parameter(Mandatory = $true)]
        [string]$SecondaryGroup,

        [string]$UserId = ''
    )

    # Aucune recherche approximative : la fiche est a un emplacement determine
    # par la topologie KoXo. Si elle n'y est pas, l'objet n'est pas materialise,
    # et le balayer par nom rattacherait le quota au premier homonyme venu.
    [void](Test-KoxoStorageNameComponent -Value $PrimaryGroup -Name 'primaryGroup')
    [void](Test-KoxoStorageNameComponent -Value $SecondaryGroup -Name 'secondaryGroup')

    $root = [System.IO.Path]::GetFullPath($UsersRoot)
    if ($TargetKind -eq 'user') {
        [void](Test-KoxoStorageNameComponent -Value $UserId -Name 'userId')
        $candidate = Join-Path (Join-Path (Join-Path $root $PrimaryGroup) $SecondaryGroup) ($UserId + '.xml')
    }
    else {
        if (-not [string]::IsNullOrWhiteSpace($UserId)) {
            throw 'userId must be absent for a secondary group target.'
        }

        $candidate = Join-Path (Join-Path $root $PrimaryGroup) ($SecondaryGroup + '.xml')
    }

    $full = [System.IO.Path]::GetFullPath($candidate)
    $rootPrefix = $root.TrimEnd('\') + '\'
    if (-not $full.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Resolved KoXo path escapes the KoXo data root.'
    }

    $full
}

function Read-KoxoStorageQuotaState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [pscustomobject]@{
            Exists = $false
            Enabled = $false
            QuotaMib = $null
            Content = $null
            Ambiguous = $false
            AmbiguityReason = $null
        }
    }

    # Lecture par octets puis decodage explicite : Get-Content laisserait la
    # page de codes ANSI decider a notre place.
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $content = (New-Object System.Text.UTF8Encoding($false)).GetString($bytes)
    if ($content.Length -gt 0 -and $content[0] -eq [char]0xFEFF) {
        $content = $content.Substring(1)
    }

    $enableMatches = [regex]::Matches($content, $script:EnableQuotaPattern)
    $quotaMatches = [regex]::Matches($content, $script:QuotaPattern)

    # Zero occurrence : la fiche n'a pas la forme attendue et completer sa
    # structure serait une invention. Plusieurs occurrences : on ne sait pas
    # laquelle fait foi. Les deux cas se ferment.
    $ambiguityReason = $null
    if ($enableMatches.Count -ne 1) {
        $ambiguityReason = ('EnableFolderQuota occurrences: {0}.' -f $enableMatches.Count)
    }
    elseif ($quotaMatches.Count -ne 1) {
        $ambiguityReason = ('FolderQuota occurrences: {0}.' -f $quotaMatches.Count)
    }

    if ($ambiguityReason) {
        return [pscustomobject]@{
            Exists = $true
            Enabled = $false
            QuotaMib = $null
            Content = $content
            Ambiguous = $true
            AmbiguityReason = $ambiguityReason
        }
    }

    $quotaValue = [long]$quotaMatches[0].Groups['value'].Value
    if ($quotaValue -lt 0) {
        return [pscustomobject]@{
            Exists = $true
            Enabled = $false
            QuotaMib = $null
            Content = $content
            Ambiguous = $true
            AmbiguityReason = 'FolderQuota is negative.'
        }
    }

    [pscustomobject]@{
        Exists = $true
        Enabled = ($enableMatches[0].Groups['value'].Value -eq '1')
        QuotaMib = $quotaValue
        Content = $content
        Ambiguous = $false
        AmbiguityReason = $null
    }
}

function Get-KoxoStorageDecision {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $State,

        [Parameter(Mandatory = $true)]
        [long]$DesiredQuotaMib
    )

    if ($DesiredQuotaMib -le 0) {
        return [pscustomobject]@{
            Decision = 'FAILED_CLOSED'
            Reason = 'Desired quota must be greater than zero.'
        }
    }

    if (-not $State.Exists) {
        return [pscustomobject]@{
            Decision = 'NOT_MATERIALIZED'
            Reason = 'The KoXo object does not exist.'
        }
    }

    if ($State.Ambiguous) {
        return [pscustomobject]@{
            Decision = 'FAILED_CLOSED'
            Reason = $State.AmbiguityReason
        }
    }

    # Quota desactive : quelle que soit la valeur enregistree, la limite n'est
    # pas opposable. L'activer est une modification, jamais un no-op. On refuse
    # neanmoins d'abaisser au passage une valeur deja enregistree : la fiche
    # peut avoir ete desactivee temporairement sans que le dossier ait maigri.
    if (-not $State.Enabled) {
        if ($State.QuotaMib -gt $DesiredQuotaMib) {
            return [pscustomobject]@{
                Decision = 'BLOCKED_REDUCTION'
                Reason = ('Recorded quota {0} MiB is greater than the desired {1} MiB.' -f $State.QuotaMib, $DesiredQuotaMib)
            }
        }

        return [pscustomobject]@{
            Decision = 'APPLY_INCREASE'
            Reason = 'Folder quota is disabled and must be enabled.'
        }
    }

    if ($State.QuotaMib -eq $DesiredQuotaMib) {
        return [pscustomobject]@{
            Decision = 'NOOP'
            Reason = 'The applied quota already matches the desired quota.'
        }
    }

    if ($State.QuotaMib -gt $DesiredQuotaMib) {
        # Abaisser un quota sous l'occupation reelle bloque immediatement
        # l'utilisateur sans rien liberer. Cette phase ne reduit jamais.
        return [pscustomobject]@{
            Decision = 'BLOCKED_REDUCTION'
            Reason = ('Applied quota {0} MiB is greater than the desired {1} MiB.' -f $State.QuotaMib, $DesiredQuotaMib)
        }
    }

    [pscustomobject]@{
        Decision = 'APPLY_INCREASE'
        Reason = ('Applied quota {0} MiB is lower than the desired {1} MiB.' -f $State.QuotaMib, $DesiredQuotaMib)
    }
}

function Set-KoxoStorageQuotaContent {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content,

        [Parameter(Mandatory = $true)]
        [long]$DesiredQuotaMib
    )

    # Substitution CIBLEE : seuls les deux contenus d'element changent. Tout le
    # reste de la fiche — ordre, indentation, elements inconnus de ce module —
    # est preserve au caractere pres, parce que KoXo la reapplique integralement
    # a l'annuaire a chaque synchronisation.
    $updated = [regex]::Replace(
        $Content,
        $script:EnableQuotaPattern,
        '<EnableFolderQuota>1</EnableFolderQuota>',
        [System.Text.RegularExpressions.RegexOptions]::None)
    $updated = [regex]::Replace(
        $updated,
        $script:QuotaPattern,
        ('<FolderQuota>{0}</FolderQuota>' -f $DesiredQuotaMib),
        [System.Text.RegularExpressions.RegexOptions]::None)

    $updated
}

function Write-KoxoStorageQuotaFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Configuration,

        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    # Ecriture dans un fichier temporaire puis remplacement atomique, avec
    # sauvegarde locale de la version precedente : une coupure au milieu d'une
    # ecriture directe laisserait une fiche tronquee, que KoXo reappliquerait
    # ensuite a l'annuaire.
    $tempPath = Join-Path $Configuration.WorkingDirectory (
        'koxo-storage-{0}.tmp' -f ([guid]::NewGuid().ToString('N'))
    )
    Write-KoxoTextFile -Path $tempPath -Content $Content -EncodingName 'utf8'
    Invoke-KoxoSafeReplacement `
        -TempPath $tempPath `
        -TargetPath $Path `
        -BackupDirectory $Configuration.BackupDirectory `
        -RetentionCount $Configuration.BackupRetentionCount
}

function Get-KoxoStorageRepairArguments {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('user', 'secondary_group')]
        [string]$TargetKind,

        [Parameter(Mandatory = $true)]
        [string]$PrimaryGroup,

        [Parameter(Mandatory = $true)]
        [string]$SecondaryGroup,

        [string]$UserId = ''
    )

    # Type="Storage" et rien d'autre : une reparation complete reappliquerait
    # aussi les groupes, le mot de passe et les acces, alors que cette operation
    # ne porte que sur le quota.
    if ($TargetKind -eq 'user') {
        return ('/RepairUser UserId="{0}" Type="Storage"' -f $UserId)
    }

    '/RepairSecondaryGroup Group="{0}" PrimaryGroup="{1}" Type="Storage"' -f $SecondaryGroup, $PrimaryGroup
}

function Test-KoxoStorageFsrmQuota {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Configuration,

        [Parameter(Mandatory = $true)]
        [ValidateSet('user', 'secondary_group')]
        [string]$TargetKind,

        [Parameter(Mandatory = $true)]
        [string]$PrimaryGroup,

        [Parameter(Mandatory = $true)]
        [string]$SecondaryGroup,

        [string]$UserId = '',

        [Parameter(Mandatory = $true)]
        [long]$DesiredQuotaMib
    )

    if (-not $Configuration.FsrmEnabled) {
        return [pscustomobject]@{
            Attempted = $false
            Verified = $false
            Reason = 'FSRM verification is disabled.'
        }
    }

    $template = if ($TargetKind -eq 'user') {
        $Configuration.FsrmUserPathTemplate
    }
    else {
        $Configuration.FsrmGroupPathTemplate
    }

    if ([string]::IsNullOrWhiteSpace($template)) {
        return [pscustomobject]@{
            Attempted = $true
            Verified = $false
            Reason = ('No FSRM path template is configured for target kind {0}.' -f $TargetKind)
        }
    }

    $path = $template.
        Replace('{primaryGroup}', $PrimaryGroup).
        Replace('{secondaryGroup}', $SecondaryGroup).
        Replace('{userId}', $UserId)

    if (-not (Get-Command -Name 'Get-FsrmQuota' -ErrorAction SilentlyContinue)) {
        return [pscustomobject]@{
            Attempted = $true
            Verified = $false
            Reason = 'Get-FsrmQuota is not available on this host.'
        }
    }

    try {
        $parameters = @{ Path = $path; ErrorAction = 'Stop' }
        if (-not [string]::IsNullOrWhiteSpace($Configuration.FsrmServer)) {
            $parameters['CimSession'] = $Configuration.FsrmServer
        }

        $quota = Get-FsrmQuota @parameters
    }
    catch {
        return [pscustomobject]@{
            Attempted = $true
            Verified = $false
            Reason = ('FSRM quota could not be read: {0}' -f $_.Exception.Message)
        }
    }

    # FSRM raisonne en octets. La comparaison se fait donc apres conversion,
    # jamais sur une valeur supposee deja en mebioctets.
    $expectedBytes = [long]$DesiredQuotaMib * 1048576L
    if ([long]$quota.Size -ne $expectedBytes) {
        return [pscustomobject]@{
            Attempted = $true
            Verified = $false
            Reason = ('FSRM quota is {0} bytes, expected {1} bytes.' -f $quota.Size, $expectedBytes)
        }
    }

    [pscustomobject]@{
        Attempted = $true
        Verified = $true
        Reason = 'FSRM quota matches the desired quota.'
    }
}

function Invoke-KoxoStorageReconcile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Configuration,

        [Parameter(Mandatory = $true)]
        [ValidateSet('user', 'secondary_group')]
        [string]$TargetKind,

        [Parameter(Mandatory = $true)]
        [string]$PrimaryGroup,

        [Parameter(Mandatory = $true)]
        [string]$SecondaryGroup,

        [string]$UserId = '',

        [Parameter(Mandatory = $true)]
        [long]$DesiredQuotaMib,

        [Parameter(Mandatory = $true)]
        [string]$CorrelationId,

        [string]$TargetKey = '',

        [string]$SubscriptionItemId = '',

        # Injection de la reparation, pour que la decision et l'ecriture soient
        # verifiables sans KoXoAdm.exe installe. En production, ce parametre
        # reste vide et le vrai processus est lance.
        [scriptblock]$RepairInvoker = $null
    )

    $auditData = @{
        correlation_id = $CorrelationId
        target_kind = $TargetKind
        primary_group = $PrimaryGroup
        secondary_group = $SecondaryGroup
        user_id = $UserId
        desired_quota_mib = $DesiredQuotaMib
        subscription_item_id = $SubscriptionItemId
        target_key = $TargetKey
    }

    $path = Resolve-KoxoStorageTargetPath `
        -UsersRoot $Configuration.UsersRoot `
        -TargetKind $TargetKind `
        -PrimaryGroup $PrimaryGroup `
        -SecondaryGroup $SecondaryGroup `
        -UserId $UserId

    # Le verrou est pris AVANT la lecture qui fonde la decision : lire puis
    # verrouiller laisserait une seconde reconciliation modifier la fiche entre
    # les deux, et la decision porterait sur un etat perime.
    $lock = Acquire-KoxoFileLock -LockPath $Configuration.LockPath
    try {
        $state = Read-KoxoStorageQuotaState -Path $path
        $decision = Get-KoxoStorageDecision -State $state -DesiredQuotaMib $DesiredQuotaMib

        Write-KoxoSyncLog -Configuration $Configuration -Level 'info' -Message 'KoXo storage reconcile decided.' -Data (
            $auditData + @{
                decision = $decision.Decision
                decision_reason = $decision.Reason
                current_quota_mib = $state.QuotaMib
                current_quota_enabled = $state.Enabled
            }
        )

        switch ($decision.Decision) {
            'NOOP' {
                return New-KoxoStorageResult -Status 'noop' -ReasonCode 'BILLING_V2_KOXO_STORAGE_NOOP' -Verification 'xml_verified' -TargetKey $TargetKey -CorrelationId $CorrelationId
            }
            'NOT_MATERIALIZED' {
                return New-KoxoStorageResult -Status 'not_materialized' -ReasonCode 'BILLING_V2_KOXO_STORAGE_TARGET_NOT_MATERIALIZED' -Verification 'none' -TargetKey $TargetKey -CorrelationId $CorrelationId
            }
            'BLOCKED_REDUCTION' {
                return New-KoxoStorageResult -Status 'blocked_reduction' -ReasonCode 'BILLING_V2_KOXO_STORAGE_QUOTA_DECREASE_REFUSED' -Verification 'none' -TargetKey $TargetKey -CorrelationId $CorrelationId
            }
            'FAILED_CLOSED' {
                return New-KoxoStorageResult -Status 'failed' -ReasonCode 'BILLING_V2_KOXO_STORAGE_SHEET_AMBIGUOUS' -Verification 'none' -TargetKey $TargetKey -CorrelationId $CorrelationId
            }
        }

        $updated = Set-KoxoStorageQuotaContent -Content $state.Content -DesiredQuotaMib $DesiredQuotaMib
        Write-KoxoStorageQuotaFile -Configuration $Configuration -Path $path -Content $updated | Out-Null

        $arguments = Get-KoxoStorageRepairArguments `
            -TargetKind $TargetKind `
            -PrimaryGroup $PrimaryGroup `
            -SecondaryGroup $SecondaryGroup `
            -UserId $UserId

        # Invoke-KoxoProcess tranche sur les marqueurs du journal KoXo et non
        # sur le code de sortie : KoXoAdm.exe sort en 1 meme en succes et peut
        # rester vivant apres avoir termine. Il leve quand rien ne prouve
        # l'execution.
        if ($RepairInvoker) {
            & $RepairInvoker $arguments
        }
        else {
            Invoke-KoxoProcess `
                -Configuration $Configuration `
                -ExecutablePath $Configuration.KoxoExecutablePath `
                -WorkingDirectory $Configuration.KoxoWorkingDirectory `
                -Arguments $arguments | Out-Null
        }

        # Relecture apres coup : l'ecriture n'est pas sa propre preuve. Une
        # reparation qui reecrirait la fiche depuis la base KoXo annulerait
        # silencieusement la modification.
        $finalState = Read-KoxoStorageQuotaState -Path $path
        if (-not $finalState.Exists -or $finalState.Ambiguous -or
            -not $finalState.Enabled -or $finalState.QuotaMib -ne $DesiredQuotaMib) {
            Write-KoxoSyncLog -Configuration $Configuration -Level 'error' -Message 'KoXo storage reconcile could not be verified in the sheet.' -Data (
                $auditData + @{ final_quota_mib = $finalState.QuotaMib; final_quota_enabled = $finalState.Enabled }
            )
            return New-KoxoStorageResult -Status 'failed' -ReasonCode 'BILLING_V2_KOXO_STORAGE_VERIFICATION_FAILED' -Verification 'none' -TargetKey $TargetKey -CorrelationId $CorrelationId
        }

        $fsrm = Test-KoxoStorageFsrmQuota `
            -Configuration $Configuration `
            -TargetKind $TargetKind `
            -PrimaryGroup $PrimaryGroup `
            -SecondaryGroup $SecondaryGroup `
            -UserId $UserId `
            -DesiredQuotaMib $DesiredQuotaMib

        # La verification FSRM demandee mais non concluante ferme le resultat :
        # elle n'a de sens que si son echec compte.
        if ($fsrm.Attempted -and -not $fsrm.Verified) {
            Write-KoxoSyncLog -Configuration $Configuration -Level 'error' -Message 'KoXo storage reconcile failed the FSRM verification.' -Data (
                $auditData + @{ fsrm_reason = $fsrm.Reason }
            )
            return New-KoxoStorageResult -Status 'failed' -ReasonCode 'BILLING_V2_KOXO_STORAGE_FSRM_VERIFICATION_FAILED' -Verification 'xml_verified' -TargetKey $TargetKey -CorrelationId $CorrelationId
        }

        $verification = if ($fsrm.Verified) { 'fully_verified' } else { 'xml_verified' }
        Write-KoxoSyncLog -Configuration $Configuration -Level 'info' -Message 'KoXo storage reconcile applied.' -Data (
            $auditData + @{ verification = $verification }
        )

        New-KoxoStorageResult -Status 'applied' -ReasonCode 'BILLING_V2_KOXO_STORAGE_APPLIED' -Verification $verification -TargetKey $TargetKey -CorrelationId $CorrelationId
    }
    finally {
        Release-KoxoFileLock -LockHandle $lock
    }
}

function New-KoxoStorageResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Status,

        [Parameter(Mandatory = $true)]
        [string]$ReasonCode,

        [Parameter(Mandatory = $true)]
        [string]$Verification,

        [AllowEmptyString()]
        [string]$TargetKey = '',

        [Parameter(Mandatory = $true)]
        [string]$CorrelationId
    )

    [pscustomobject]@{
        status = $Status
        reasonCode = $ReasonCode
        verification = $Verification
        targetKey = $TargetKey
        correlationId = $CorrelationId
    }
}

function Read-KoxoStorageRequest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Body
    )

    # Validation STRICTE du contrat : rien n'est devine, rien n'est complete
    # par defaut. Un champ incoherent avec le type de cible est refuse plutot
    # que neutralise, parce qu'il signale un appelant qui ne decrit pas ce
    # qu'il croit decrire.
    if ([string]::IsNullOrWhiteSpace($Body)) {
        throw 'A JSON body is required.'
    }

    $payload = $Body | ConvertFrom-Json

    $targetKind = [string](Get-KoxoPropertyValue -InputObject $payload -Name 'targetKind')
    if ($targetKind -ne 'user' -and $targetKind -ne 'secondary_group') {
        throw 'targetKind must be "user" or "secondary_group".'
    }

    $correlationId = [string](Get-KoxoPropertyValue -InputObject $payload -Name 'correlationId')
    if ([string]::IsNullOrWhiteSpace($correlationId)) {
        throw 'correlationId is required.'
    }

    $primaryGroup = [string](Get-KoxoPropertyValue -InputObject $payload -Name 'primaryGroup')
    $secondaryGroup = [string](Get-KoxoPropertyValue -InputObject $payload -Name 'secondaryGroup')
    [void](Test-KoxoStorageNameComponent -Value $primaryGroup -Name 'primaryGroup')
    [void](Test-KoxoStorageNameComponent -Value $secondaryGroup -Name 'secondaryGroup')

    $userId = [string](Get-KoxoPropertyValue -InputObject $payload -Name 'userId')
    if ($targetKind -eq 'user') {
        [void](Test-KoxoStorageNameComponent -Value $userId -Name 'userId')
    }
    elseif (-not [string]::IsNullOrWhiteSpace($userId)) {
        throw 'userId must be absent for a secondary group target.'
    }
    else {
        $userId = ''
    }

    $rawQuota = Get-KoxoPropertyValue -InputObject $payload -Name 'desiredQuotaMib'
    $quota = 0L
    if (-not [long]::TryParse([string]$rawQuota, [ref]$quota) -or $quota -le 0) {
        throw 'desiredQuotaMib must be a positive integer.'
    }

    [pscustomobject]@{
        CorrelationId = $correlationId
        TargetKind = $targetKind
        PrimaryGroup = $primaryGroup
        SecondaryGroup = $secondaryGroup
        UserId = $userId
        DesiredQuotaMib = $quota
        TargetKey = [string](Get-KoxoPropertyValue -InputObject $payload -Name 'targetKey')
        SubscriptionItemId = [string](Get-KoxoPropertyValue -InputObject $payload -Name 'subscriptionItemId')
    }
}

Export-ModuleMember -Function `
    Get-KoxoStorageConfiguration, `
    Get-KoxoStorageDecision, `
    Get-KoxoStorageRepairArguments, `
    Invoke-KoxoStorageReconcile, `
    New-KoxoStorageResult, `
    Read-KoxoStorageQuotaState, `
    Read-KoxoStorageRequest, `
    Resolve-KoxoStorageTargetPath, `
    Set-KoxoStorageQuotaContent, `
    Test-KoxoStorageFsrmQuota, `
    Test-KoxoStorageNameComponent, `
    Write-KoxoStorageQuotaFile
