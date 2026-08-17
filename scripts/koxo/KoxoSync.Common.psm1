Set-StrictMode -Version Latest

function Get-KoxoSyncConfiguration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$CsvTargetPath,

        [string]$WorkingDirectory = (Join-Path $PSScriptRoot 'work'),

        [hashtable]$Overrides = @{}
    )

    $targetPath = [System.IO.Path]::GetFullPath($CsvTargetPath)
    $workRoot = [System.IO.Path]::GetFullPath($WorkingDirectory)
    $targetDirectory = Split-Path -Parent $targetPath
    if (-not (Test-Path -LiteralPath $targetDirectory)) {
        New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
    }

    if (-not (Test-Path -LiteralPath $workRoot)) {
        New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
    }

    $logDirectory = [System.IO.Path]::GetFullPath(
        (Get-KoxoSetting -Name 'KOXO_LOG_DIRECTORY' -DefaultValue (Join-Path $workRoot 'logs') -Overrides $Overrides)
    )
    if (-not (Test-Path -LiteralPath $logDirectory)) {
        New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    }

    $configuration = [ordered]@{
        ApiUrl = Get-KoxoSetting -Name 'KOXO_API_URL' -Required -Overrides $Overrides
        ApiToken = Get-KoxoSetting -Name 'KOXO_API_TOKEN' -Required -Overrides $Overrides
        AllowInsecureHttp = Test-KoxoBooleanSetting -Name 'KOXO_ALLOW_INSECURE_HTTP' -Overrides $Overrides
        # utf8bom et non utf8 : sans marque d'ordre d'octets, KoXo relit le
        # fichier en ANSI et « LAUMAILLÉ » arrive dans l'annuaire sous la forme
        # « LAUMAILLÃ‰ ». Le défaut doit donc être sûr par lui-même, la variable
        # d'environnement ne servant qu'à en sortir volontairement.
        CsvEncoding = (Get-KoxoSetting -Name 'KOXO_CSV_ENCODING' -DefaultValue 'utf8bom' -Overrides $Overrides).ToLowerInvariant()
        MinUserCount = [int](Get-KoxoSetting -Name 'KOXO_MIN_USER_COUNT' -DefaultValue '0' -Overrides $Overrides)
        # 20 et non 100 : à 100 le garde-fou ne pouvait pas se déclencher, la
        # comparaison étant STRICTEMENT supérieure et une chute ne pouvant pas
        # dépasser 100 %. Il était donc décoratif. Or retirer une ligne du CSV
        # vaut ordre de désactivation côté KoXo (DisableOrphanedAccounts) : un
        # export partiel — requête interrompue, client filtré par erreur —
        # coupe l'accès de vrais clients sans qu'aucune erreur ne soit levée.
        MaxUserDropPercent = [int](Get-KoxoSetting -Name 'KOXO_MAX_USER_DROP_PERCENT' -DefaultValue '20' -Overrides $Overrides)
        # Sortie de secours pour une baisse légitime (résiliations groupées).
        # Explicite et journalisée : on veut que contourner le garde-fou soit
        # un geste conscient, pas un effet de bord d'une variable oubliée.
        AllowUserDrop = Test-KoxoBooleanSetting -Name 'KOXO_ALLOW_USER_DROP' -Overrides $Overrides
        # Un CSV vide n'est pas un CSV « sans changement » : avec
        # DisableOrphanedAccounts, il vaut ordre de désactivation de TOUTES les
        # identités de la branche. Le garde-fou de volumétrie ne couvre pas ce
        # cas au premier passage, où la référence vaut zéro. Par défaut on saute
        # donc le profil en laissant son fichier en l'état — périmé vaut mieux
        # que destructeur — et il faut ce drapeau pour vider délibérément.
        AllowEmptyCsv = Test-KoxoBooleanSetting -Name 'KOXO_ALLOW_EMPTY_CSV' -Overrides $Overrides
        # Les autres CSV de la meme installation, separes par « ; ». Sert a
        # verifier qu'aucun IdentifiantUnique n'est revendique par deux profils.
        OtherCsvPaths = @(
            (Get-KoxoSetting -Name 'KOXO_OTHER_CSV_PATHS' -DefaultValue '' -Overrides $Overrides) -split ';' |
                ForEach-Object { $_.Trim() } |
                Where-Object { $_ }
        )
        SyncTimeoutSeconds = [int](Get-KoxoSetting -Name 'KOXO_SYNC_TIMEOUT_SECONDS' -DefaultValue '90' -Overrides $Overrides)
        LogDirectory = $logDirectory
        KoxoLogGlob = Get-KoxoSetting -Name 'KOXO_KOXO_LOG_GLOB' -DefaultValue '' -Overrides $Overrides
        BackupRetentionCount = [int](Get-KoxoSetting -Name 'KOXO_BACKUP_RETENTION_COUNT' -DefaultValue '10' -Overrides $Overrides)
        CsvTargetPath = $targetPath
        WorkingDirectory = $workRoot
        BackupDirectory = Join-Path $targetDirectory 'backups'
        # Un état PAR PROFIL, nommé d'après son CSV. Un état partagé ferait
        # alterner les références de deux profils de tailles différentes : la
        # baisse apparente déclencherait le garde-fou de volumétrie à chaque
        # passage, sur une variation qui n'existe pas.
        StatePath = Join-Path $logDirectory (
            'koxo-sync.state.{0}.json' -f [System.IO.Path]::GetFileNameWithoutExtension($targetPath)
        )
        # Le verrou, lui, reste COMMUN à tous les profils, et c'est voulu :
        # KoXoAdm.exe ne supporte pas deux instances concurrentes.
        LockPath = Join-Path $logDirectory 'koxo-sync.lock'
        LogPath = Join-Path $logDirectory ("koxo-sync-{0}.log" -f (Get-Date -Format 'yyyyMMdd'))
    }

    if (-not (Test-Path -LiteralPath $configuration.BackupDirectory)) {
        New-Item -ItemType Directory -Path $configuration.BackupDirectory -Force | Out-Null
    }

    if ($configuration.MinUserCount -lt 0) {
        throw 'KOXO_MIN_USER_COUNT must be >= 0.'
    }

    if ($configuration.MaxUserDropPercent -lt 0 -or $configuration.MaxUserDropPercent -gt 100) {
        throw 'KOXO_MAX_USER_DROP_PERCENT must be between 0 and 100.'
    }

    if ($configuration.SyncTimeoutSeconds -lt 5) {
        throw 'KOXO_SYNC_TIMEOUT_SECONDS must be >= 5.'
    }

    if ($configuration.BackupRetentionCount -lt 1) {
        throw 'KOXO_BACKUP_RETENTION_COUNT must be >= 1.'
    }

    Test-KoxoApiUrl -ApiUrl $configuration.ApiUrl -AllowInsecureHttp:$configuration.AllowInsecureHttp
    [pscustomobject]$configuration
}

function Invoke-KoxoSync {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$CsvTargetPath,

        [string]$WorkingDirectory = (Join-Path $PSScriptRoot 'work'),

        [hashtable]$Overrides = @{},

        [switch]$DryRun,

        [switch]$LaunchKoxo,

        [string]$KoxoExecutablePath = 'C:\Program Files\KoXo Dev\KoXoAdm\KoXoAdm.exe',

        [string]$KoxoWorkingDirectory = 'C:\Program Files\KoXo Dev\KoXoAdm',

        [string]$KoxoSyncArgument = '/Synchro=CLIENTS.xml',

        # Groupe primaire pris en charge par ce profil. Seules les identites qui
        # le portent atteignent le CSV. Facultatif pour rester utilisable sur une
        # installation mono-profil, mais un export qui en publie plusieurs sans
        # aiguillage est refuse : ecrire les deux populations dans le meme
        # fichier ferait appliquer le modele et le quota des uns aux autres.
        [string]$PrimaryGroup,

        $PayloadObject
    )

    $configuration = Get-KoxoSyncConfiguration -CsvTargetPath $CsvTargetPath -WorkingDirectory $WorkingDirectory -Overrides $Overrides
    $lock = $null

    try {
        $lock = Acquire-KoxoFileLock -LockPath $configuration.LockPath
        Write-KoxoSyncLog -Configuration $configuration -Level 'info' -Message 'KoXo sync started.' -Data @{
            dry_run = [bool]$DryRun
            api_url = $configuration.ApiUrl
            target_path = $configuration.CsvTargetPath
            csv_encoding = $configuration.CsvEncoding
        }

        if ($null -eq $PayloadObject) {
            $payload = Invoke-KoxoApiRequest -Configuration $configuration
        }
        else {
            $payload = $PayloadObject
        }

        $validation = Test-KoxoExportPayload -Payload $payload
        if (-not $validation.IsValid) {
            Write-KoxoSyncLog -Configuration $configuration -Level 'error' -Message 'KoXo payload validation failed.' -Data @{
                code = 'KOXO_EXPORT_VALIDATION_FAILED'
                errors = $validation.Errors
            }
            throw (New-Object System.InvalidOperationException('KOXO_EXPORT_VALIDATION_FAILED'))
        }

        $payloadGroups = @(Get-KoxoPayloadPrimaryGroups -Payload $payload)
        if ($PrimaryGroup) {
            $payload = Select-KoxoPayloadByPrimaryGroup -Payload $payload -PrimaryGroup $PrimaryGroup
            Write-KoxoSyncLog -Configuration $configuration -Level 'info' -Message 'KoXo payload filtered on its primary group.' -Data @{
                primary_group = $PrimaryGroup
                selected_user_count = [int]$payload.userCount
                payload_primary_groups = $payloadGroups
            }
        }
        elseif ($payloadGroups.Count -gt 1) {
            throw (
                "Payload publishes {0} primary groups ({1}) but no -PrimaryGroup was given. A single CSV cannot serve two KoXo profiles." -f
                $payloadGroups.Count,
                ($payloadGroups -join ', ')
            )
        }

        # Avant tout le reste : un profil sans identite laisse son CSV EN L'ETAT
        # et ne lance pas KoXo. Ecrire un fichier vide reviendrait a declarer
        # orpheline chaque identite de la branche, donc a les desactiver toutes.
        if ([int]$payload.userCount -eq 0 -and -not $configuration.AllowEmptyCsv) {
            Write-KoxoSyncLog -Configuration $configuration -Level 'warning' -Message 'KoXo profile skipped: the export publishes no identity for it.' -Data @{
                primary_group = $PrimaryGroup
                target_path = $configuration.CsvTargetPath
            }

            return [pscustomobject]@{
                Status = 'skipped_empty_profile'
                UserCount = 0
                PrimaryGroup = $PrimaryGroup
                CsvEncoding = $configuration.CsvEncoding
                Hash = $null
                TempPath = $null
                TargetPath = $configuration.CsvTargetPath
                BackupPath = $null
                LogPath = $configuration.LogPath
                KoxoLaunch = $null
                ApplicationLogs = @()
            }
        }

        $state = Read-KoxoSyncState -StatePath $configuration.StatePath
        $guardRails = Test-KoxoGuardRails -Configuration $configuration -Payload $payload -State $state
        Write-KoxoSyncLog -Configuration $configuration -Level 'info' -Message 'KoXo guardrails evaluated.' -Data @{
            user_count = $guardRails.UserCount
            baseline_user_count = $guardRails.BaselineUserCount
            drop_percent = $guardRails.DropPercent
            max_drop_percent = $guardRails.MaxDropPercent
            bypassed = $guardRails.Bypassed
        }

        $ownership = Test-KoxoIdentifierOwnership `
            -Identifiers @($payload.users | ForEach-Object { [string](Get-KoxoPropertyValue -InputObject $_ -Name 'identifiantUnique') }) `
            -OtherCsvPaths $configuration.OtherCsvPaths `
            -EncodingName $configuration.CsvEncoding
        Write-KoxoSyncLog -Configuration $configuration -Level 'info' -Message 'KoXo identifier ownership verified.' -Data @{
            published_count = $ownership.PublishedCount
            checked_files = $ownership.CheckedFiles
        }

        $csvContent = ConvertTo-KoxoCsvContent -Users $payload.users
        $fileHash = Get-KoxoSha256Hex -Text $csvContent
        $tempDirectory = if ($DryRun) { $configuration.WorkingDirectory } else { Split-Path -Parent $configuration.CsvTargetPath }
        if (-not (Test-Path -LiteralPath $tempDirectory)) {
            New-Item -ItemType Directory -Path $tempDirectory -Force | Out-Null
        }

        $tempPath = Join-Path $tempDirectory ("koxo-users-{0}.csv.tmp" -f ([guid]::NewGuid().ToString('N')))
        Write-KoxoTextFile -Path $tempPath -Content $csvContent -EncodingName $configuration.CsvEncoding
        Test-KoxoCsvFile -Path $tempPath -EncodingName $configuration.CsvEncoding | Out-Null

        $backupPath = $null
        if (-not $DryRun) {
            $replacement = Invoke-KoxoSafeReplacement -TempPath $tempPath -TargetPath $configuration.CsvTargetPath -BackupDirectory $configuration.BackupDirectory -RetentionCount $configuration.BackupRetentionCount
            $backupPath = $replacement.BackupPath
            Write-KoxoSyncState -StatePath $configuration.StatePath -Payload $payload -Hash $fileHash
        }

        $koxoLaunch = $null
        if (-not $DryRun -and $LaunchKoxo) {
            $koxoLaunch = Invoke-KoxoProcess `
                -Configuration $configuration `
                -ExecutablePath $KoxoExecutablePath `
                -WorkingDirectory $KoxoWorkingDirectory `
                -Arguments $KoxoSyncArgument `
                -ExpectedUserCount @($payload.users).Count
        }
        elseif ($LaunchKoxo) {
            $koxoLaunch = [pscustomobject]@{
                Requested = $true
                Status = 'skipped_dry_run'
                ExecutablePath = $KoxoExecutablePath
                WorkingDirectory = $KoxoWorkingDirectory
                Arguments = $KoxoSyncArgument
                ExitCode = $null
                TimedOut = $false
                DurationSeconds = 0
            }
        }
        else {
            $koxoLaunch = [pscustomobject]@{
                Requested = $false
                Status = 'not_requested'
                ExecutablePath = $KoxoExecutablePath
                WorkingDirectory = $KoxoWorkingDirectory
                Arguments = $KoxoSyncArgument
                ExitCode = $null
                TimedOut = $false
                DurationSeconds = 0
            }
        }

        $applicationLogs = Get-KoxoRecentApplicationLogs -GlobPattern $configuration.KoxoLogGlob -Tail 20
        $result = [pscustomobject]@{
            Status = if ($DryRun) { 'dry_run' } elseif ($LaunchKoxo) { 'synchronized_and_launched' } else { 'synchronized' }
            UserCount = [int]$payload.userCount
            PrimaryGroup = $PrimaryGroup
            CsvEncoding = $configuration.CsvEncoding
            Hash = $fileHash
            TempPath = $tempPath
            TargetPath = $configuration.CsvTargetPath
            BackupPath = $backupPath
            LogPath = $configuration.LogPath
            KoxoLaunch = $koxoLaunch
            ApplicationLogs = $applicationLogs
        }

        Write-KoxoSyncLog -Configuration $configuration -Level 'info' -Message 'KoXo sync completed.' -Data @{
            status = $result.Status
            user_count = $result.UserCount
            hash = $result.Hash
            backup_path = $backupPath
            koxo_launch_status = $koxoLaunch.Status
            koxo_exit_code = $koxoLaunch.ExitCode
            koxo_timed_out = $koxoLaunch.TimedOut
        }

        $result
    }
    catch {
        if ($configuration) {
            Write-KoxoSyncLog -Configuration $configuration -Level 'error' -Message 'KoXo sync failed.' -Data @{
                exception = $_.Exception.Message
            }
        }
        throw
    }
    finally {
        if ($lock) {
            Release-KoxoFileLock -LockHandle $lock
        }
    }
}

function Invoke-KoxoSyncProfiles {
    [CmdletBinding()]
    param(
        # Un profil par entree : @{ PrimaryGroup = ...; CsvTargetPath = ...;
        # KoxoSyncArgument = ... }.
        [Parameter(Mandatory = $true)]
        [object[]]$Profiles,

        [string]$WorkingDirectory = (Join-Path $PSScriptRoot 'work'),

        [hashtable]$Overrides = @{},

        [switch]$DryRun,

        [switch]$LaunchKoxo,

        [string]$KoxoExecutablePath = 'C:\Program Files\KoXo Dev\KoXoAdm\KoXoAdm.exe',

        [string]$KoxoWorkingDirectory = 'C:\Program Files\KoXo Dev\KoXoAdm',

        $PayloadObject
    )

    if ($Profiles.Count -eq 0) {
        throw 'At least one KoXo profile must be declared.'
    }

    foreach ($profil in $Profiles) {
        foreach ($cle in @('PrimaryGroup', 'CsvTargetPath', 'KoxoSyncArgument')) {
            if (-not $profil.ContainsKey($cle) -or [string]::IsNullOrWhiteSpace([string]$profil[$cle])) {
                throw ("KoXo profile entry is missing {0}." -f $cle)
            }
        }
    }

    # L'export n'est appele QU'UNE FOIS, et le meme objet sert tous les profils.
    # Ce n'est pas une optimisation : l'API ne detient les mots de passe en clair
    # que le temps d'un export, et les consomme en les publiant. Un second appel
    # rendrait un payload sans colonne 14, et le profil servi en second
    # n'appliquerait aucun mot de passe.
    $configurationAmorce = Get-KoxoSyncConfiguration `
        -CsvTargetPath ([string]$Profiles[0].CsvTargetPath) `
        -WorkingDirectory $WorkingDirectory `
        -Overrides $Overrides

    if ($null -eq $PayloadObject) {
        $payload = Invoke-KoxoApiRequest -Configuration $configurationAmorce
    }
    else {
        $payload = $PayloadObject
    }

    $validation = Test-KoxoExportPayload -Payload $payload
    if (-not $validation.IsValid) {
        Write-KoxoSyncLog -Configuration $configurationAmorce -Level 'error' -Message 'KoXo payload validation failed.' -Data @{
            code = 'KOXO_EXPORT_VALIDATION_FAILED'
            errors = $validation.Errors
        }
        throw (New-Object System.InvalidOperationException('KOXO_EXPORT_VALIDATION_FAILED'))
    }

    $routing = Test-KoxoProfileRouting `
        -Payload $payload `
        -PrimaryGroups @($Profiles | ForEach-Object { [string]$_.PrimaryGroup })
    Write-KoxoSyncLog -Configuration $configurationAmorce -Level 'info' -Message 'KoXo profile routing verified.' -Data @{
        claimed_groups = $routing.ClaimedGroups
        payload_groups = $routing.PayloadGroups
    }

    # KOXO_OTHER_CSV_PATHS est neutralise ici, et il le faut : ce controle relit
    # les CSV VOISINS SUR DISQUE, or ils sont periment tant que leur profil n'est
    # pas passe. Une identite qui vient de changer de branche figurerait donc
    # dans le nouveau fichier et encore dans l'ancien, et serait signalee comme
    # un conflit alors qu'elle n'en est pas un. Test-KoxoProfileRouting ci-dessus
    # verifie la meme propriete sur la SOURCE, ou elle est exacte : un export
    # decoupe par groupe primaire produit des sous-ensembles disjoints.
    $surcharges = $Overrides.Clone()
    $surcharges['KOXO_OTHER_CSV_PATHS'] = ''

    $resultats = New-Object System.Collections.Generic.List[object]
    foreach ($profil in $Profiles) {
        $resultats.Add((Invoke-KoxoSync `
            -CsvTargetPath ([string]$profil.CsvTargetPath) `
            -WorkingDirectory $WorkingDirectory `
            -Overrides $surcharges `
            -DryRun:$DryRun `
            -LaunchKoxo:$LaunchKoxo `
            -KoxoExecutablePath $KoxoExecutablePath `
            -KoxoWorkingDirectory $KoxoWorkingDirectory `
            -KoxoSyncArgument ([string]$profil.KoxoSyncArgument) `
            -PrimaryGroup ([string]$profil.PrimaryGroup) `
            -PayloadObject $payload))
    }

    # ToArray() et non @(...) : sur PowerShell 5.1, envelopper une
    # List[object] d'objets composes leve « Les types des arguments ne
    # correspondent pas ». Ne pas « simplifier » en @($resultats).
    $resultats.ToArray()
}

function Invoke-KoxoApiRequest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Configuration
    )

    Add-Type -AssemblyName System.Net.Http | Out-Null
    $handler = New-Object System.Net.Http.HttpClientHandler
    $client = New-Object System.Net.Http.HttpClient($handler)
    $client.Timeout = [TimeSpan]::FromSeconds($Configuration.SyncTimeoutSeconds)

    try {
        $request = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Get, $Configuration.ApiUrl)
        $request.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $Configuration.ApiToken)
        $request.Headers.Accept.ParseAdd('application/json')
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw ("KoXo API returned HTTP {0}." -f [int]$response.StatusCode)
        }

        if ([string]::IsNullOrWhiteSpace($content)) {
            throw 'KoXo API returned an empty body.'
        }

        $content | ConvertFrom-Json
    }
    finally {
        if ($request) {
            $request.Dispose()
        }
        $client.Dispose()
        $handler.Dispose()
    }
}

function Test-KoxoExportPayload {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Payload
    )

    $errors = @()
    $expectedRootFields = @('schemaVersion', 'generatedAt', 'userCount', 'users')
    $expectedUserFields = @(
        'civilite',
        'nom',
        'prenom',
        'dateNaissance',
        'identifiantUnique',
        'groupeSecondaire',
        'email',
        'groupePrimaire'
    )

    # motDePasse est ACCEPTE mais FACULTATIF, et volontairement hors de la liste
    # ci-dessus, qui sert aussi de liste des champs OBLIGATOIRES : l'API ne
    # detient le mot de passe en clair qu'a l'instant ou le client le saisit,
    # elle ne peut donc pas le publier a chaque export. Publie, il alimente la
    # colonne 14 que KoXo applique a l'annuaire quand ForcePasswords vaut 1 ;
    # absent, KoXo conserve le mot de passe qu'il connait deja.
    $optionalUserFields = @('motDePasse')

    $rootNames = @(Get-KoxoPropertyNames -InputObject $Payload)
    foreach ($name in $rootNames) {
        if ($name -notin $expectedRootFields) {
            $errors += [pscustomobject]@{ Scope = 'payload'; Field = $name; Message = 'Unexpected root field.' }
        }
    }

    foreach ($name in $expectedRootFields) {
        if ($name -notin $rootNames) {
            $errors += [pscustomobject]@{ Scope = 'payload'; Field = $name; Message = 'Missing root field.' }
        }
    }

    # Version 2 : chaque utilisateur porte groupePrimaire. Le refus est
    # volontairement symetrique — un export de version 1 n'aiguille personne, et
    # ce script ne saurait pas dans quel CSV ranger les identites. Mieux vaut
    # echouer fermé qu'ecrire un fichier au petit bonheur.
    if ((Get-KoxoPropertyValue -InputObject $Payload -Name 'schemaVersion') -ne 2) {
        $errors += [pscustomobject]@{ Scope = 'payload'; Field = 'schemaVersion'; Message = 'schemaVersion must be 2.' }
    }

    $generatedAt = Get-KoxoPropertyValue -InputObject $Payload -Name 'generatedAt'
    $parsedGeneratedAt = [datetimeoffset]::MinValue
    if (-not [datetimeoffset]::TryParse([string]$generatedAt, [ref]$parsedGeneratedAt)) {
        $errors += [pscustomobject]@{ Scope = 'payload'; Field = 'generatedAt'; Message = 'generatedAt must be ISO 8601.' }
    }

    $users = @(Get-KoxoPropertyValue -InputObject $Payload -Name 'users')
    $userCount = [int](Get-KoxoPropertyValue -InputObject $Payload -Name 'userCount')
    if ($users.Count -ne $userCount) {
        $errors += [pscustomobject]@{ Scope = 'payload'; Field = 'userCount'; Message = 'userCount must match the number of users.' }
    }

    for ($index = 0; $index -lt $users.Count; $index++) {
        $user = $users[$index]
        $names = @(Get-KoxoPropertyNames -InputObject $user)
        foreach ($name in $names) {
            if ($name -notin $expectedUserFields -and $name -notin $optionalUserFields) {
                $errors += [pscustomobject]@{ Scope = 'user'; Index = $index; Field = $name; Message = 'Unexpected user field.' }
            }
        }

        foreach ($name in $expectedUserFields) {
            if ($name -notin $names) {
                $errors += [pscustomobject]@{ Scope = 'user'; Index = $index; Field = $name; Message = 'Missing user field.' }
                continue
            }

            $value = [string](Get-KoxoPropertyValue -InputObject $user -Name $name)
            if ([string]::IsNullOrWhiteSpace($value)) {
                $errors += [pscustomobject]@{ Scope = 'user'; Index = $index; Field = $name; Message = 'Field is required.' }
            }
        }

        $title = [string](Get-KoxoPropertyValue -InputObject $user -Name 'civilite')
        if ($title -and $title -notin @('Mme', 'M.')) {
            $errors += [pscustomobject]@{ Scope = 'user'; Index = $index; Field = 'civilite'; Message = 'civilite must be Mme or M..' }
        }

        $birthDate = [string](Get-KoxoPropertyValue -InputObject $user -Name 'dateNaissance')
        if ($birthDate -and $birthDate -notmatch '^\d{4}-\d{2}-\d{2}$') {
            $errors += [pscustomobject]@{ Scope = 'user'; Index = $index; Field = 'dateNaissance'; Message = 'dateNaissance must use YYYY-MM-DD.' }
        }

        $identifier = [string](Get-KoxoPropertyValue -InputObject $user -Name 'identifiantUnique')
        if ($identifier -and $identifier -notmatch '^CLI-\d{6}$') {
            $errors += [pscustomobject]@{ Scope = 'user'; Index = $index; Field = 'identifiantUnique'; Message = 'identifiantUnique must match CLI-000000.' }
        }

        $email = [string](Get-KoxoPropertyValue -InputObject $user -Name 'email')
        if ($email -and $email -notmatch '^[^\s@]+@[^\s@]+\.[^\s@]+$') {
            $errors += [pscustomobject]@{ Scope = 'user'; Index = $index; Field = 'email'; Message = 'email format is invalid.' }
        }
    }

    [pscustomobject]@{
        IsValid = ($errors.Count -eq 0)
        Errors = @($errors)
    }
}

function Get-KoxoPayloadPrimaryGroups {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Payload
    )

    $groupes = New-Object System.Collections.Generic.List[string]
    foreach ($user in @(Get-KoxoPropertyValue -InputObject $Payload -Name 'users')) {
        $valeur = [string](Get-KoxoPropertyValue -InputObject $user -Name 'groupePrimaire')
        if (-not [string]::IsNullOrWhiteSpace($valeur) -and -not $groupes.Contains($valeur.Trim())) {
            $groupes.Add($valeur.Trim())
        }
    }

    @($groupes)
}

function Select-KoxoPayloadByPrimaryGroup {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Payload,

        [Parameter(Mandatory = $true)]
        [string]$PrimaryGroup
    )

    # Comparaison insensible a la CASSE mais pas aux ACCENTS : « clients demo »
    # doit passer, « CLIENTS DEMO » sans accent non. C'est exactement la
    # distinction utile ici, la graphie accentuee devant correspondre au bit pres
    # a celle saisie dans l'IHM KoXo sous peine de no-op silencieux.
    $retenus = @(
        @(Get-KoxoPropertyValue -InputObject $Payload -Name 'users') |
            Where-Object {
                [string](Get-KoxoPropertyValue -InputObject $_ -Name 'groupePrimaire') -eq $PrimaryGroup
            }
    )

    [pscustomobject]@{
        schemaVersion = (Get-KoxoPropertyValue -InputObject $Payload -Name 'schemaVersion')
        generatedAt = (Get-KoxoPropertyValue -InputObject $Payload -Name 'generatedAt')
        userCount = $retenus.Count
        users = $retenus
    }
}

function Test-KoxoProfileRouting {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Payload,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$PrimaryGroups
    )

    # Une identite absente de TOUS les CSV n'est pas « ignoree » : elle devient
    # orpheline pour le profil qui la portait, donc desactivee. Un groupe
    # primaire publie par l'API mais reclame par aucun profil doit donc arreter
    # la synchronisation, pas la laisser passer en silence.
    $revendiques = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::Ordinal)
    foreach ($groupe in $PrimaryGroups) {
        if ([string]::IsNullOrWhiteSpace($groupe)) {
            throw 'Each KoXo profile must declare a non-empty primary group.'
        }

        if (-not $revendiques.Add($groupe.Trim())) {
            throw ("Primary group {0} is claimed by more than one KoXo profile." -f $groupe.Trim())
        }
    }

    $orphelins = @(
        Get-KoxoPayloadPrimaryGroups -Payload $Payload |
            Where-Object { -not $revendiques.Contains($_) }
    )
    if ($orphelins.Count -gt 0) {
        throw (
            "No KoXo profile claims primary group(s): {0}. Declared profiles: {1}." -f
            ($orphelins -join ', '),
            (@($revendiques) -join ', ')
        )
    }

    [pscustomobject]@{
        ClaimedGroups = @($revendiques)
        PayloadGroups = @(Get-KoxoPayloadPrimaryGroups -Payload $Payload)
    }
}

function ConvertTo-KoxoCsvContent {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Users
    )

    # 14 colonnes et non 13 : le profil KoXo lit le mot de passe dans
    # « Field 14 » (<Password>Field 14</Password>). Un fichier a 13 colonnes
    # ferait lire un mot de passe vide, et un fichier a nombre de colonnes
    # VARIABLE decale les champs — c'est ce qui a fait appliquer l'identite et
    # le mot de passe de Jean DUPONT sur le compte de Zachary le 2026-08-06,
    # KoXo rapprochant les lignes par l'IdentifiantUnique de la colonne 5.
    # La largeur doit donc etre constante, ligne d'en-tete comprise.
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('Civilite;Nom;Prenom;DateNaissance;IdentifiantUnique;GroupeSecondaire;Email;Telephone;TelephoneMobile;Fax;PageWeb;ChampLibre;Fonction;MotDePasse')
    foreach ($user in $Users) {
        $fields = @(
            [string](Get-KoxoPropertyValue -InputObject $user -Name 'civilite'),
            [string](Get-KoxoPropertyValue -InputObject $user -Name 'nom'),
            [string](Get-KoxoPropertyValue -InputObject $user -Name 'prenom'),
            [string](Get-KoxoPropertyValue -InputObject $user -Name 'dateNaissance'),
            [string](Get-KoxoPropertyValue -InputObject $user -Name 'identifiantUnique'),
            [string](Get-KoxoPropertyValue -InputObject $user -Name 'groupeSecondaire'),
            [string](Get-KoxoPropertyValue -InputObject $user -Name 'email'),
            '',
            '',
            '',
            '',
            '',
            '',
            [string](Get-KoxoPropertyValue -InputObject $user -Name 'motDePasse')
        )

        $escaped = foreach ($field in $fields) {
            Escape-KoxoCsvField -Value $field
        }
        $lines.Add(($escaped -join ';'))
    }

    ([string]::Join("`r`n", $lines) + "`r`n")
}

function Test-KoxoCsvFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [string]$EncodingName = 'utf8bom'
    )

    Add-Type -AssemblyName Microsoft.VisualBasic | Out-Null
    $encoding = Get-KoxoEncoding -Name $EncodingName
    $parser = New-Object Microsoft.VisualBasic.FileIO.TextFieldParser($Path, $encoding)
    $parser.SetDelimiters(';')
    $parser.HasFieldsEnclosedInQuotes = $true

    # Toute ligne doit avoir exactement 14 champs, en-tete comprise. Une largeur
    # variable n'est pas un detail cosmetique : KoXo rapproche les lignes par
    # l'IdentifiantUnique de la colonne 5, donc un champ manquant decale cette
    # colonne et fait ecrire l'identite ET le mot de passe d'un client sur le
    # compte d'un autre. C'est arrive le 2026-08-06 sur un CSV assemble a la
    # main. Ce controle est la derniere barriere avant l'annuaire.
    $expectedColumnCount = 14
    $lineNumber = 0
    try {
        while (-not $parser.EndOfData) {
            $lineNumber++
            $row = $parser.ReadFields()
            if ($row.Count -ne $expectedColumnCount) {
                throw ("CSV row {0} must contain exactly {1} columns. Found {2}." -f $lineNumber, $expectedColumnCount, $row.Count)
            }
        }
    }
    finally {
        $parser.Close()
    }

    $true
}

function Invoke-KoxoSafeReplacement {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$TempPath,

        [Parameter(Mandatory = $true)]
        [string]$TargetPath,

        [Parameter(Mandatory = $true)]
        [string]$BackupDirectory,

        [int]$RetentionCount = 10
    )

    if (-not (Test-Path -LiteralPath $BackupDirectory)) {
        New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
    }

    $backupPath = Join-Path $BackupDirectory (
        '{0}.{1}.bak' -f
        ([System.IO.Path]::GetFileName($TargetPath)),
        (Get-Date -Format 'yyyyMMddHHmmss')
    )

    if (Test-Path -LiteralPath $TargetPath) {
        [System.IO.File]::Replace($TempPath, $TargetPath, $backupPath)
    }
    else {
        Move-Item -LiteralPath $TempPath -Destination $TargetPath -Force
        $backupPath = $null
    }

    $backups = @(Get-ChildItem -LiteralPath $BackupDirectory -Filter (([System.IO.Path]::GetFileName($TargetPath)) + '.*.bak') -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending
    )
    if ($backups.Count -gt $RetentionCount) {
        $backups | Select-Object -Skip $RetentionCount | Remove-Item -Force
    }

    [pscustomobject]@{
        TargetPath = $TargetPath
        BackupPath = $backupPath
    }
}

function Acquire-KoxoFileLock {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$LockPath
    )

    $directory = Split-Path -Parent $LockPath
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    try {
        $stream = New-Object System.IO.FileStream(
            $LockPath,
            [System.IO.FileMode]::OpenOrCreate,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None
        )
        [pscustomobject]@{
            Path = $LockPath
            Stream = $stream
        }
    }
    catch {
        throw 'Another KoXo sync process already holds the lock.'
    }
}

function Release-KoxoFileLock {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $LockHandle
    )

    if ($LockHandle.Stream) {
        $LockHandle.Stream.Dispose()
    }
}

function Get-KoxoRecentApplicationLogs {
    [CmdletBinding()]
    param(
        [string]$GlobPattern,

        [int]$Tail = 20
    )

    if ([string]::IsNullOrWhiteSpace($GlobPattern)) {
        return @()
    }

    $items = @(Get-ChildItem -Path $GlobPattern -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending)
    if ($items.Count -eq 0) {
        return @()
    }

    Get-Content -LiteralPath $items[0].FullName -Tail $Tail -ErrorAction SilentlyContinue
}

function Get-KoxoLatestExternalLog {
    [CmdletBinding()]
    param(
        [string]$GlobPattern,

        [datetime]$NotBeforeUtc = [datetime]::MinValue
    )

    if ([string]::IsNullOrWhiteSpace($GlobPattern)) {
        return $null
    }

    $items = @(
        Get-ChildItem -Path $GlobPattern -File -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Name -notlike 'koxo-sync-*' -and
                $_.Name -ne 'koxo-sync.state.json' -and
                $_.Name -ne 'koxo-sync.lock' -and
                $_.LastWriteTimeUtc -ge $NotBeforeUtc
            } |
            Sort-Object LastWriteTimeUtc -Descending
    )
    if ($items.Count -eq 0) {
        return $null
    }

    $items[0]
}

function Get-KoxoProcessedUserCount {
    [CmdletBinding()]
    param(
        [string[]]$Lines = @()
    )

    # Motifs volontairement SANS accent : le journal KoXo est relu sans encodage
    # impose et « forcé » peut arriver mutile. Ce qui precede l'accent suffit.
    #
    # Le login n'est pas toujours extractible ; une ligne « Ajout/Modification
    # de ... » atteste malgre tout d'un traitement. On compte donc des CLES :
    # le login quand on sait le lire — ce qui dedoublonne les mentions
    # multiples d'une meme identite — sinon le rang de la ligne.
    $patterns = @(
        'Ajout/Modification de .*\(([^()]+)\)',   # synchro d'une identite existante
        'Ajout/Modification de',                  # meme evenement, login illisible
        '\}\s*Ajout\s+([^\s]+)\s*$',              # {Ajout d'un utilisateur} Ajout <login>
        'Mot de passe forc.*"([^"]+)"'            # mot de passe applique a <login>
    )

    $keys = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $line = [string]$Lines[$i]
        foreach ($pattern in $patterns) {
            $m = [regex]::Match($line, $pattern)
            if (-not $m.Success) { continue }

            if ($m.Groups.Count -gt 1 -and $m.Groups[1].Success -and $m.Groups[1].Value.Trim()) {
                [void]$keys.Add($m.Groups[1].Value.Trim())
            }
            else {
                [void]$keys.Add("ligne:$i")
            }

            break
        }
    }

    $keys.Count
}

function Test-KoxoLogOutcome {
    [CmdletBinding()]
    param(
        [string]$GlobPattern,

        [datetime]$NotBeforeUtc = [datetime]::MinValue,

        [int]$Tail = 200,

        # Nombre d'identites que le CSV publie. A zero, le controle est inactif.
        [int]$ExpectedUserCount = 0
    )

    $logFile = Get-KoxoLatestExternalLog -GlobPattern $GlobPattern -NotBeforeUtc $NotBeforeUtc
    if ($null -eq $logFile) {
        return [pscustomobject]@{
            HasRecentLog = $false
            IsSuccessful = $false
            LogPath = $null
            AcceptedMarker = $false
            CompletionMarker = $false
            BlockingError = $false
            ProcessedUserCount = 0
            NoUserProcessed = $false
            TailLines = @()
        }
    }

    $tailLines = @(
        Get-Content -LiteralPath $logFile.FullName -Tail $Tail -ErrorAction SilentlyContinue
    )
    $joined = ($tailLines -join "`n").ToLowerInvariant()
    $acceptedMarker =
        $joined.Contains('paramètre accepté') -or
        $joined.Contains('param?tre accept?') -or
        $joined.Contains('param??tre accept??') -or
        $joined.Contains('parametre accepte')
    $completionMarker =
        $joined.Contains('fin de l''opération') -or
        $joined.Contains('fin de l''op?ration') -or
        $joined.Contains('fin de l''op??ration') -or
        $joined.Contains('fin de l''operation')
    $blockingError =
        $joined.Contains('erreur bloquante') -or
        $joined.Contains('échec') -or
        $joined.Contains('?chec') -or
        $joined.Contains('??chec') -or
        $joined.Contains('echec fatal') -or
        $joined.Contains('fatal error')

    # Garde-fou « zero traite ». Un profil qui vise un groupe primaire
    # inexistant sort en trois lignes : parametre accepte, fin de l'operation.
    # Les deux marqueurs de succes sont donc presents alors que KoXo n'a
    # touche personne — mesure en reel le 2026-08-06. On ne compare pas le
    # compte exact (un deplacement d'identite ne journalise aucun utilisateur
    # au premier passage, et ce passage est legitime) : seul le zero absolu,
    # alors que le CSV publie des identites, est traite comme un echec.
    $processedUserCount = Get-KoxoProcessedUserCount -Lines $tailLines
    $noUserProcessed = ($ExpectedUserCount -gt 0 -and $processedUserCount -eq 0)

    [pscustomobject]@{
        IsSuccessful = ($acceptedMarker -and $completionMarker -and -not $blockingError -and -not $noUserProcessed)
        HasRecentLog = $true
        LogPath = $logFile.FullName
        AcceptedMarker = $acceptedMarker
        CompletionMarker = $completionMarker
        BlockingError = $blockingError
        ProcessedUserCount = $processedUserCount
        NoUserProcessed = $noUserProcessed
        TailLines = $tailLines
    }
}

function Test-KoxoIdentifierOwnership {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$Identifiers,

        [string[]]$OtherCsvPaths = @(),

        [string]$EncodingName = 'utf8bom'
    )

    # Un IdentifiantUnique ne doit appartenir qu'a UN seul profil. Present dans
    # deux CSV, il est revendique par deux moteurs de reconciliation : la
    # derniere synchro executee reprend l'identite, et le retrait du premier
    # fichier la fait passer pour orpheline — donc desactivee, puisque
    # DisableOrphanedAccounts est actif. Constate en reel le 2026-08-06.
    $conflicts = New-Object System.Collections.Generic.List[string]
    $publies = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
    foreach ($id in $Identifiers) {
        if (-not [string]::IsNullOrWhiteSpace($id)) { [void]$publies.Add($id.Trim()) }
    }

    foreach ($path in $OtherCsvPaths) {
        if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path)) {
            continue
        }

        $encoding = Get-KoxoEncoding -Name $EncodingName
        $lignes = [System.IO.File]::ReadAllLines($path, $encoding)
        for ($i = 1; $i -lt $lignes.Length; $i++) {
            $champs = $lignes[$i] -split ';', -1
            if ($champs.Count -lt 5) { continue }
            $autre = $champs[4].Trim()
            if ($autre -and $publies.Contains($autre)) {
                $conflicts.Add(("{0} (aussi dans {1})" -f $autre, (Split-Path -Leaf $path)))
            }
        }
    }

    if ($conflicts.Count -gt 0) {
        throw ("Identifiers must belong to a single CSV. Conflicts: {0}." -f ($conflicts -join ', '))
    }

    [pscustomobject]@{
        PublishedCount = $publies.Count
        CheckedFiles = @($OtherCsvPaths | Where-Object { $_ -and (Test-Path -LiteralPath $_) })
    }
}

function Invoke-KoxoProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Configuration,

        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [string]$Arguments,

        [int]$ExpectedUserCount = 0
    )

    $resolvedExecutablePath = [System.IO.Path]::GetFullPath($ExecutablePath)
    $resolvedWorkingDirectory = [System.IO.Path]::GetFullPath($WorkingDirectory)

    if (-not (Test-Path -LiteralPath $resolvedExecutablePath -PathType Leaf)) {
        throw ("KoXo executable not found: {0}." -f $resolvedExecutablePath)
    }

    if (-not (Test-Path -LiteralPath $resolvedWorkingDirectory -PathType Container)) {
        throw ("KoXo working directory not found: {0}." -f $resolvedWorkingDirectory)
    }

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $resolvedExecutablePath
    $startInfo.Arguments = $Arguments
    $startInfo.WorkingDirectory = $resolvedWorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true

    Write-KoxoSyncLog -Configuration $Configuration -Level 'info' -Message 'Launching KoXo process.' -Data @{
        executable_path = $resolvedExecutablePath
        working_directory = $resolvedWorkingDirectory
        arguments = $Arguments
    }

    $startedAt = Get-Date
    $startedAtUtc = $startedAt.ToUniversalTime()
    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw 'Failed to start KoXo process.'
    }

    $timeoutMilliseconds = [math]::Max(5000, ($Configuration.SyncTimeoutSeconds * 1000))
    $timedOut = $false

    # KoXoAdm.exe peut terminer son travail puis ne jamais rendre la main.
    # Le depassement de delai n'est donc pas une preuve d'echec : on tue le
    # processus, puis on tranche sur le journal KoXo comme pour un code de
    # sortie non nul.
    if (-not $process.WaitForExit($timeoutMilliseconds)) {
        $timedOut = $true
        try {
            if (-not $process.HasExited) {
                $process.Kill()
                $process.WaitForExit()
            }
        }
        catch {
        }
    }

    $durationSeconds = [math]::Round(((Get-Date) - $startedAt).TotalSeconds, 2)
    $process.Refresh()
    $logOutcome = Test-KoxoLogOutcome `
        -GlobPattern $Configuration.KoxoLogGlob `
        -NotBeforeUtc $startedAtUtc.AddSeconds(-5) `
        -ExpectedUserCount $ExpectedUserCount

    $exitCode = $null
    try {
        $exitCode = $process.ExitCode
    }
    catch {
    }

    # Controle avant tout autre : un code de sortie nul et les deux marqueurs de
    # succes ne prouvent RIEN si KoXo n'a touche personne. C'est exactement ce
    # que produit un profil visant un groupe primaire inexistant.
    if ($logOutcome.NoUserProcessed) {
        Write-KoxoSyncLog -Configuration $Configuration -Level 'error' -Message 'KoXo reported no processed identity while the CSV published some.' -Data @{
            executable_path = $resolvedExecutablePath
            arguments = $Arguments
            expected_user_count = $ExpectedUserCount
            processed_user_count = $logOutcome.ProcessedUserCount
            koxo_log_path = $logOutcome.LogPath
        }

        throw ("KoXo processed no identity while the CSV published {0}. Check that the profile's primary group exists." -f $ExpectedUserCount)
    }

    if ($timedOut) {
        if (-not $logOutcome.IsSuccessful) {
            Write-KoxoSyncLog -Configuration $Configuration -Level 'error' -Message 'KoXo process timed out without any proof of completion in the KoXo log.' -Data @{
                executable_path = $resolvedExecutablePath
                arguments = $Arguments
                timeout_seconds = $Configuration.SyncTimeoutSeconds
                duration_seconds = $durationSeconds
                koxo_log_path = $logOutcome.LogPath
                koxo_log_has_recent = $logOutcome.HasRecentLog
                koxo_log_accepted_marker = $logOutcome.AcceptedMarker
                koxo_log_completion_marker = $logOutcome.CompletionMarker
                koxo_log_blocking_error = $logOutcome.BlockingError
            }

            throw ("KoXo process timed out after {0} seconds." -f $Configuration.SyncTimeoutSeconds)
        }
    }
    elseif ($exitCode -ne 0 -and -not $logOutcome.IsSuccessful) {
        throw ("KoXo process failed with exit code {0}." -f $exitCode)
    }

    $completionLevel = if ($timedOut) { 'warning' } else { 'info' }
    $completionMessage = if ($timedOut) {
        'KoXo process timed out but the recent KoXo log proves the operation completed.'
    }
    else {
        'KoXo process completed.'
    }

    Write-KoxoSyncLog -Configuration $Configuration -Level $completionLevel -Message $completionMessage -Data @{
        executable_path = $resolvedExecutablePath
        arguments = $Arguments
        exit_code = $exitCode
        timed_out = $timedOut
        timeout_seconds = $Configuration.SyncTimeoutSeconds
        duration_seconds = $durationSeconds
        koxo_log_path = $logOutcome.LogPath
        koxo_log_success = $logOutcome.IsSuccessful
        koxo_log_has_recent = $logOutcome.HasRecentLog
        koxo_log_accepted_marker = $logOutcome.AcceptedMarker
        koxo_log_completion_marker = $logOutcome.CompletionMarker
        koxo_log_blocking_error = $logOutcome.BlockingError
    }

    [pscustomobject]@{
        Requested = $true
        Status = if ($timedOut) { 'completed_after_timeout' } elseif ($exitCode -eq 0) { 'completed' } else { 'completed_with_nonzero_exit' }
        ExecutablePath = $resolvedExecutablePath
        WorkingDirectory = $resolvedWorkingDirectory
        Arguments = $Arguments
        ExitCode = $exitCode
        TimedOut = $timedOut
        DurationSeconds = $durationSeconds
        LogPath = $logOutcome.LogPath
        LogSuccessful = $logOutcome.IsSuccessful
        LogAcceptedMarker = $logOutcome.AcceptedMarker
        LogCompletionMarker = $logOutcome.CompletionMarker
        LogBlockingError = $logOutcome.BlockingError
    }
}

function Write-KoxoSyncLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Configuration,

        [Parameter(Mandatory = $true)]
        [string]$Level,

        [Parameter(Mandatory = $true)]
        [string]$Message,

        [hashtable]$Data = @{}
    )

    $entry = [ordered]@{
        timestamp_utc = (Get-Date).ToUniversalTime().ToString('o')
        level = $Level
        message = $Message
    }

    # Depuis que la colonne 14 transporte un mot de passe en clair, filtrer le
    # seul mot « token » ne suffit plus : le journal survit au CSV, il est
    # archive et relu longtemps apres.
    foreach ($key in $Data.Keys) {
        if ($key -match '(?i)token|password|motdepasse|secret') {
            continue
        }
        $entry[$key] = $Data[$key]
    }

    $json = ($entry | ConvertTo-Json -Depth 10 -Compress)
    Add-Content -LiteralPath $Configuration.LogPath -Value $json -Encoding UTF8
}

function Read-KoxoSyncState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$StatePath
    )

    if (-not (Test-Path -LiteralPath $StatePath)) {
        return $null
    }

    Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
}

function Write-KoxoSyncState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$StatePath,

        [Parameter(Mandatory = $true)]
        $Payload,

        [Parameter(Mandatory = $true)]
        [string]$Hash
    )

    $state = [ordered]@{
        lastUserCount = [int](Get-KoxoPropertyValue -InputObject $Payload -Name 'userCount')
        lastHash = $Hash
        updatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    }

    Write-KoxoTextFile -Path $StatePath -Content (($state | ConvertTo-Json -Depth 5)) -EncodingName 'utf8'
}

function Test-KoxoGuardRails {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Configuration,

        [Parameter(Mandatory = $true)]
        $Payload,

        $State
    )

    $userCount = [int](Get-KoxoPropertyValue -InputObject $Payload -Name 'userCount')
    if ($userCount -lt $Configuration.MinUserCount) {
        throw ("userCount {0} is below KOXO_MIN_USER_COUNT ({1})." -f $userCount, $Configuration.MinUserCount)
    }

    $baseline = 0
    if ($State -and (Get-KoxoPropertyNames -InputObject $State) -contains 'lastUserCount') {
        $baseline = [int]$State.lastUserCount
    }

    $dropPercent = 0
    $bypassed = $false
    if ($baseline -gt 0 -and $userCount -lt $baseline) {
        $dropPercent = [math]::Round((($baseline - $userCount) / [double]$baseline) * 100, 2)
        if ($dropPercent -gt $Configuration.MaxUserDropPercent) {
            if (-not $Configuration.AllowUserDrop) {
                throw ("userCount drop {0}% (from {1} to {2}) exceeds KOXO_MAX_USER_DROP_PERCENT ({3}). Set KOXO_ALLOW_USER_DROP=true to proceed deliberately." -f $dropPercent, $baseline, $userCount, $Configuration.MaxUserDropPercent)
            }

            $bypassed = $true
        }
    }

    # Rendu à l'appelant pour être journalisé à CHAQUE passage, y compris quand
    # le contrôle passe : un garde-fou muet ne se distingue pas d'un garde-fou
    # absent le jour où l'on cherche à comprendre une désactivation en masse.
    [pscustomobject]@{
        UserCount = $userCount
        BaselineUserCount = $baseline
        DropPercent = $dropPercent
        MaxDropPercent = $Configuration.MaxUserDropPercent
        Bypassed = $bypassed
    }
}

function Escape-KoxoCsvField {
    [CmdletBinding()]
    param(
        [AllowNull()]
        [string]$Value
    )

    $text = if ($null -eq $Value) { '' } else { $Value }
    $escaped = $text -replace '"', '""'
    if ($escaped.IndexOfAny(@([char]';', [char]'"', [char]"`r", [char]"`n")) -ge 0) {
        return '"' + $escaped + '"'
    }

    $escaped
}

function Get-KoxoSha256Hex {
    [CmdletBinding(DefaultParameterSetName = 'Text')]
    param(
        [Parameter(Mandatory = $true, ParameterSetName = 'Text')]
        [string]$Text,

        [Parameter(Mandatory = $true, ParameterSetName = 'Path')]
        [string]$Path
    )

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        if ($PSCmdlet.ParameterSetName -eq 'Path') {
            $bytes = [System.IO.File]::ReadAllBytes($Path)
        }
        else {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        }

        $hashBytes = $sha.ComputeHash($bytes)
        -join ($hashBytes | ForEach-Object { $_.ToString('x2') })
    }
    finally {
        $sha.Dispose()
    }
}

function Write-KoxoTextFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Content,

        [Parameter(Mandatory = $true)]
        [string]$EncodingName
    )

    $directory = Split-Path -Parent $Path
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $encoding = Get-KoxoEncoding -Name $EncodingName
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)

    # `ascii` et `latin1` remplacent silencieusement par « ? » tout caractere
    # hors de leur jeu : une majuscule accentuee perdue ici ne serait plus
    # rattrapable en aval. On relit donc immediatement avec le meme encodage et
    # on refuse d'aller plus loin en cas de perte.
    $roundTrip = [System.IO.File]::ReadAllText($Path, $encoding)
    if ($roundTrip -cne $Content) {
        $lost = Get-KoxoLostCharacters -Source $Content -RoundTrip $roundTrip
        throw (
            "L'encodage {0} ne peut pas representer le contenu ecrit dans {1}{2}." -f
            $EncodingName,
            $Path,
            $lost
        )
    }
}

function Get-KoxoLostCharacters {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Source,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$RoundTrip
    )

    if ($Source.Length -ne $RoundTrip.Length) {
        return ''
    }

    $lost = New-Object System.Collections.Generic.List[string]
    for ($index = 0; $index -lt $Source.Length; $index++) {
        if ($Source[$index] -cne $RoundTrip[$index] -and -not $lost.Contains([string]$Source[$index])) {
            $lost.Add([string]$Source[$index])
        }
    }

    if ($lost.Count -eq 0) {
        return ''
    }

    ' (caracteres perdus : ' + ($lost -join ' ') + ')'
}

function Get-KoxoEncoding {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    switch ($Name.ToLowerInvariant()) {
        'utf8' { return (New-Object System.Text.UTF8Encoding($false)) }
        'utf8bom' { return (New-Object System.Text.UTF8Encoding($true)) }
        'unicode' { return [System.Text.Encoding]::Unicode }
        'ascii' { return [System.Text.Encoding]::ASCII }
        'latin1' { return [System.Text.Encoding]::GetEncoding('iso-8859-1') }
        default { throw ("Unsupported KOXO_CSV_ENCODING value: {0}." -f $Name) }
    }
}

function Get-KoxoSetting {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [string]$DefaultValue,

        [switch]$Required,

        [hashtable]$Overrides = @{}
    )

    if ($Overrides.ContainsKey($Name)) {
        $value = $Overrides[$Name]
    }
    else {
        $value = [Environment]::GetEnvironmentVariable($Name)
    }

    if ($null -eq $value -or [string]::IsNullOrWhiteSpace([string]$value)) {
        $value = $DefaultValue
    }

    if ($Required -and [string]::IsNullOrWhiteSpace([string]$value)) {
        throw ("Missing required setting: {0}." -f $Name)
    }

    [string]$value
}

function Test-KoxoBooleanSetting {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [hashtable]$Overrides = @{}
    )

    $value = Get-KoxoSetting -Name $Name -DefaultValue 'false' -Overrides $Overrides
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $false
    }

    switch ($value.Trim().ToLowerInvariant()) {
        '1' { return $true }
        'true' { return $true }
        'yes' { return $true }
        'on' { return $true }
        '0' { return $false }
        'false' { return $false }
        'no' { return $false }
        'off' { return $false }
        default { throw ("Invalid boolean setting for {0}: {1}." -f $Name, $value) }
    }
}

function Test-KoxoApiUrl {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ApiUrl,

        [switch]$AllowInsecureHttp
    )

    $uri = [System.Uri]$ApiUrl
    if ($uri.Scheme -ne 'https' -and $uri.Host -notin @('localhost', '127.0.0.1') -and -not $AllowInsecureHttp) {
        throw 'KOXO_API_URL must use HTTPS outside local execution.'
    }
}

function Get-KoxoPropertyNames {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $InputObject
    )

    if ($InputObject -is [System.Collections.IDictionary]) {
        return @($InputObject.Keys)
    }

    @($InputObject.PSObject.Properties | ForEach-Object { $_.Name })
}

function Get-KoxoPropertyValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $InputObject,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($InputObject -is [System.Collections.IDictionary]) {
        return $InputObject[$Name]
    }

    $property = $InputObject.PSObject.Properties[$Name]
    if ($property) {
        return $property.Value
    }

    $null
}

Export-ModuleMember -Function `
    Acquire-KoxoFileLock, `
    ConvertTo-KoxoCsvContent, `
    Escape-KoxoCsvField, `
    Get-KoxoEncoding, `
    Get-KoxoLostCharacters, `
    Get-KoxoRecentApplicationLogs, `
    Get-KoxoLatestExternalLog, `
    Get-KoxoPropertyValue, `
    Get-KoxoSetting, `
    Get-KoxoSha256Hex, `
    Get-KoxoSyncConfiguration, `
    Invoke-KoxoApiRequest, `
    Invoke-KoxoProcess, `
    Invoke-KoxoSafeReplacement, `
    Invoke-KoxoSync, `
    Invoke-KoxoSyncProfiles, `
    Get-KoxoPayloadPrimaryGroups, `
    Select-KoxoPayloadByPrimaryGroup, `
    Test-KoxoProfileRouting, `
    Read-KoxoSyncState, `
    Release-KoxoFileLock, `
    Test-KoxoCsvFile, `
    Test-KoxoBooleanSetting, `
    Test-KoxoApiUrl, `
    Test-KoxoLogOutcome, `
    Test-KoxoIdentifierOwnership, `
    Get-KoxoProcessedUserCount, `
    Test-KoxoExportPayload, `
    Test-KoxoGuardRails, `
    Write-KoxoSyncState, `
    Write-KoxoSyncLog, `
    Write-KoxoTextFile

