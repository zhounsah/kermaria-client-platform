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
        MaxUserDropPercent = [int](Get-KoxoSetting -Name 'KOXO_MAX_USER_DROP_PERCENT' -DefaultValue '100' -Overrides $Overrides)
        SyncTimeoutSeconds = [int](Get-KoxoSetting -Name 'KOXO_SYNC_TIMEOUT_SECONDS' -DefaultValue '90' -Overrides $Overrides)
        LogDirectory = $logDirectory
        KoxoLogGlob = Get-KoxoSetting -Name 'KOXO_KOXO_LOG_GLOB' -DefaultValue '' -Overrides $Overrides
        BackupRetentionCount = [int](Get-KoxoSetting -Name 'KOXO_BACKUP_RETENTION_COUNT' -DefaultValue '10' -Overrides $Overrides)
        CsvTargetPath = $targetPath
        WorkingDirectory = $workRoot
        BackupDirectory = Join-Path $targetDirectory 'backups'
        StatePath = Join-Path $logDirectory 'koxo-sync.state.json'
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

        $state = Read-KoxoSyncState -StatePath $configuration.StatePath
        Test-KoxoGuardRails -Configuration $configuration -Payload $payload -State $state

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
                -Arguments $KoxoSyncArgument
        }
        elseif ($LaunchKoxo) {
            $koxoLaunch = [pscustomobject]@{
                Requested = $true
                Status = 'skipped_dry_run'
                ExecutablePath = $KoxoExecutablePath
                WorkingDirectory = $KoxoWorkingDirectory
                Arguments = $KoxoSyncArgument
                ExitCode = $null
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
                DurationSeconds = 0
            }
        }

        $applicationLogs = Get-KoxoRecentApplicationLogs -GlobPattern $configuration.KoxoLogGlob -Tail 20
        $result = [pscustomobject]@{
            Status = if ($DryRun) { 'dry_run' } elseif ($LaunchKoxo) { 'synchronized_and_launched' } else { 'synchronized' }
            UserCount = [int]$payload.userCount
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
        'email'
    )

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

    if ((Get-KoxoPropertyValue -InputObject $Payload -Name 'schemaVersion') -ne 1) {
        $errors += [pscustomobject]@{ Scope = 'payload'; Field = 'schemaVersion'; Message = 'schemaVersion must be 1.' }
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
            if ($name -notin $expectedUserFields) {
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

function ConvertTo-KoxoCsvContent {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Users
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('Civilite;Nom;Prenom;DateNaissance;IdentifiantUnique;GroupeSecondaire;Email;Telephone;TelephoneMobile;Fax;PageWeb;ChampLibre;Fonction')
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
            ''
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

    try {
        while (-not $parser.EndOfData) {
            $row = $parser.ReadFields()
            if ($row.Count -ne 13) {
                throw ("CSV row must contain exactly 13 columns. Found {0}." -f $row.Count)
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

function Test-KoxoLogOutcome {
    [CmdletBinding()]
    param(
        [string]$GlobPattern,

        [datetime]$NotBeforeUtc = [datetime]::MinValue,

        [int]$Tail = 200
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

    [pscustomobject]@{
        IsSuccessful = ($acceptedMarker -and $completionMarker -and -not $blockingError)
        HasRecentLog = $true
        LogPath = $logFile.FullName
        AcceptedMarker = $acceptedMarker
        CompletionMarker = $completionMarker
        BlockingError = $blockingError
        TailLines = $tailLines
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
        [string]$Arguments
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
    if (-not $process.WaitForExit($timeoutMilliseconds)) {
        try {
            if (-not $process.HasExited) {
                $process.Kill()
                $process.WaitForExit()
            }
        }
        catch {
        }

        throw ("KoXo process timed out after {0} seconds." -f $Configuration.SyncTimeoutSeconds)
    }

    $durationSeconds = [math]::Round(((Get-Date) - $startedAt).TotalSeconds, 2)
    $process.Refresh()
    $logOutcome = Test-KoxoLogOutcome -GlobPattern $Configuration.KoxoLogGlob -NotBeforeUtc $startedAtUtc.AddSeconds(-5)

    if ($process.ExitCode -ne 0 -and -not $logOutcome.IsSuccessful) {
        throw ("KoXo process failed with exit code {0}." -f $process.ExitCode)
    }

    Write-KoxoSyncLog -Configuration $Configuration -Level 'info' -Message 'KoXo process completed.' -Data @{
        executable_path = $resolvedExecutablePath
        arguments = $Arguments
        exit_code = $process.ExitCode
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
        Status = if ($process.ExitCode -eq 0) { 'completed' } else { 'completed_with_nonzero_exit' }
        ExecutablePath = $resolvedExecutablePath
        WorkingDirectory = $resolvedWorkingDirectory
        Arguments = $Arguments
        ExitCode = $process.ExitCode
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

    foreach ($key in $Data.Keys) {
        if ($key -match 'token') {
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

    if ($State -and $State.lastUserCount -gt 0 -and $userCount -lt $State.lastUserCount) {
        $dropPercent = [math]::Round((($State.lastUserCount - $userCount) / [double]$State.lastUserCount) * 100, 2)
        if ($dropPercent -gt $Configuration.MaxUserDropPercent) {
            throw ("userCount drop {0}% exceeds KOXO_MAX_USER_DROP_PERCENT ({1})." -f $dropPercent, $Configuration.MaxUserDropPercent)
        }
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
    Get-KoxoSha256Hex, `
    Get-KoxoSyncConfiguration, `
    Invoke-KoxoApiRequest, `
    Invoke-KoxoProcess, `
    Invoke-KoxoSafeReplacement, `
    Invoke-KoxoSync, `
    Read-KoxoSyncState, `
    Release-KoxoFileLock, `
    Test-KoxoCsvFile, `
    Test-KoxoBooleanSetting, `
    Test-KoxoApiUrl, `
    Test-KoxoLogOutcome, `
    Test-KoxoExportPayload, `
    Test-KoxoGuardRails, `
    Write-KoxoSyncState, `
    Write-KoxoSyncLog, `
    Write-KoxoTextFile

