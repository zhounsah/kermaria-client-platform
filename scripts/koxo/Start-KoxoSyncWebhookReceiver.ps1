[CmdletBinding()]
param(
    [string]$Prefix = 'http://+:8041/internal/koxo/sync/',
    [string]$SyncScriptPath = (Join-Path $PSScriptRoot 'Sync-KoXoClients.ps1'),
    [string]$WebhookSyncLauncherPath = (Join-Path $PSScriptRoot 'Invoke-KoxoSyncFromWebhook.ps1'),
    [string]$CsvTargetPath = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients.csv',
    [string]$WorkingDirectory = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\work',
    [string]$KoxoExecutablePath = 'C:\Program Files\KoXo Dev\KoXoAdm\KoXoAdm.exe',
    [string]$KoxoWorkingDirectory = 'C:\Program Files\KoXo Dev\KoXoAdm',
    [string]$KoxoSyncArgument = '/Synchro=CLIENTS.xml',
    [string]$Token = '',
    [string]$LogDirectory = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\Logs\webhook',
    # Operation CIBLEE de quota, distincte de la synchronisation globale.
    # Elle partage l'hote, le port et le mecanisme d'authentification, mais
    # jamais la semantique : elle ne declenche aucune synchronisation CSV.
    [string]$StoragePath = '/internal/koxo/storage/reconcile/',
    [string]$StorageToken = '',
    [string]$KoxoDataRoot = 'C:\Program Files\KoXo Dev\KoXoAdm\Data',
    # Instance ISOLEE (DEV) : chemin absolu de son fichier de definition JSON.
    # Il fixe le prefixe d'ecoute, le jeton (lu dans un fichier, jamais sur la
    # ligne de commande), les journaux, et il est transmis au lanceur de
    # synchronisation. La route de stockage y est fermee sauf mention
    # contraire. Absent, le recepteur reste celui de la production, inchange.
    [string]$InstanceConfigPath = '',
    # Receveur distinct : meme port, uniquement le prefixe de stockage.
    [switch]$StorageOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'KoxoStorage.Common.psm1') -Force -DisableNameChecking

$storageRouteEnabled = $true
$instanceArguments = ''
$instance = $null
if ($StorageOnly -and [string]::IsNullOrWhiteSpace($InstanceConfigPath)) {
    throw 'StorageOnly requires an isolated instance definition.'
}
if (-not [string]::IsNullOrWhiteSpace($InstanceConfigPath)) {
    Import-Module (Join-Path $PSScriptRoot 'KoxoSync.Common.psm1') -Force -DisableNameChecking
    $instance = Get-KoxoInstanceDefinition -InstanceConfigPath $InstanceConfigPath -StorageOnly:$StorageOnly
    if ($null -eq $instance.Receiver) {
        throw ("KoXo instance {0} declares no receiver section." -f $instance.InstanceName)
    }

    if (-not $instance.Receiver.TokenPath -or -not (Test-Path -LiteralPath $instance.Receiver.TokenPath -PathType Leaf)) {
        throw ("KoXo instance {0}: receiver token file not found." -f $instance.InstanceName)
    }

    $Prefix = $instance.Receiver.Prefix
    $Token = [System.IO.File]::ReadAllText($instance.Receiver.TokenPath).Trim()
    $LogDirectory = $instance.Receiver.LogDirectory
    $storageRouteEnabled = $instance.Receiver.StorageRouteEnabled
    # Une instance isolee n'a jamais de jeton de stockage herite du processus.
    $StorageToken = $Token
    $instanceArguments = ' -InstanceConfigPath "{0}"' -f [System.IO.Path]::GetFullPath($InstanceConfigPath)
}

if (-not (Test-Path -LiteralPath $LogDirectory)) {
    New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
}

$resolvedToken = if ([string]::IsNullOrWhiteSpace($Token)) {
    $env:KOXO_SYNC_WEBHOOK_TOKEN
} else {
    $Token
}

if ([string]::IsNullOrWhiteSpace($resolvedToken)) {
    throw 'KOXO_SYNC_WEBHOOK_TOKEN is required.'
}

# Jeton dedie facultatif. Pose, il devient le SEUL jeton accepte sur la route
# de stockage : un secret qui fuiterait cote facturation ne pourrait alors pas
# declencher la synchronisation globale, dont un passage errone desactive des
# comptes. Absent, la route de stockage retombe sur le jeton existant.
$resolvedStorageToken = if ([string]::IsNullOrWhiteSpace($StorageToken)) {
    $env:KOXO_STORAGE_WEBHOOK_TOKEN
} else {
    $StorageToken
}

if ([string]::IsNullOrWhiteSpace($resolvedStorageToken)) {
    $resolvedStorageToken = $resolvedToken
}

$syncPath = ([System.Uri]($Prefix -replace '://\+', '://localhost')).AbsolutePath.TrimEnd('/')
$normalizedStoragePath = '/' + $StoragePath.Trim('/')
$storagePrefix = ($Prefix -replace '(?<=://[^/]+)/.*$', '') + $normalizedStoragePath + '/'
# Meme repertoire de travail que la synchronisation globale, volontairement :
# c'est ce qui fait tomber les deux chemins sur le MEME verrou, et KoXoAdm.exe
# ne supporte pas deux instances concurrentes.
$storageConfiguration = $null
if ($storageRouteEnabled) {
    $storageOverrides = @{}
    if ($null -ne $instance) {
        $WorkingDirectory = $instance.WorkingDirectory
        $storageOverrides = @{
            KOXO_EXECUTABLE_PATH = $instance.KoxoExecutablePath
            KOXO_WORKING_DIRECTORY = $instance.KoxoWorkingDirectory
            KOXO_KOXO_LOG_GLOB = $instance.KoxoLogGlob
            KOXO_SYNC_TIMEOUT_SECONDS = [string]$instance.SyncTimeoutSeconds
            KOXO_LOG_DIRECTORY = $instance.LogDirectory
        }
        if ($null -ne $instance.Receiver.Storage) {
            $settings = $instance.Receiver.Storage
            $storageOverrides.KOXO_STORAGE_DATA_ROOT = $settings.DataRoot
            $storageOverrides.KOXO_STORAGE_FSRM_ENABLED = ([string]$settings.FsrmEnabled).ToLowerInvariant()
            $storageOverrides.KOXO_STORAGE_FSRM_SERVER = $settings.FsrmServer
            $storageOverrides.KOXO_STORAGE_FSRM_USER_PATH_TEMPLATE = $settings.FsrmUserPathTemplate
            $storageOverrides.KOXO_STORAGE_FSRM_GROUP_PATH_TEMPLATE = $settings.FsrmGroupPathTemplate
        }
    }
    $storageConfiguration = Get-KoxoStorageConfiguration `
        -DataRoot $KoxoDataRoot `
        -WorkingDirectory $WorkingDirectory -Overrides $storageOverrides
    if ($null -ne $instance) {
        $storageConfiguration | Add-Member -NotePropertyName AdmLockTimeoutSeconds -NotePropertyValue $instance.AdmLockTimeoutSeconds -Force
    }
}

$resolvedSyncScriptPath = [System.IO.Path]::GetFullPath($SyncScriptPath)
$resolvedWebhookSyncLauncherPath = [System.IO.Path]::GetFullPath($WebhookSyncLauncherPath)
$resolvedCsvTargetPath = [System.IO.Path]::GetFullPath($CsvTargetPath)
$resolvedWorkingDirectory = [System.IO.Path]::GetFullPath($WorkingDirectory)
$resolvedKoxoExecutablePath = [System.IO.Path]::GetFullPath($KoxoExecutablePath)
$resolvedKoxoWorkingDirectory = [System.IO.Path]::GetFullPath($KoxoWorkingDirectory)
$listener = [System.Net.HttpListener]::new()
$qualityProofPath = '/internal/koxo/qualities/proof'
$qualityProofEnabled = -not $StorageOnly -and $null -ne $instance -and
    @($instance.Profiles | Where-Object {$_.PrimaryGroup -eq 'CLIENTS DEV'}).Count -eq 1
if ($qualityProofEnabled) {
    Import-Module (Join-Path $PSScriptRoot 'KoxoQualities.Common.psm1') -Force -DisableNameChecking
    $listener.Prefixes.Add(($Prefix -replace '(?<=://[^/]+)/.*$', '') + $qualityProofPath + '/')
}
if (-not $StorageOnly) {
    $listener.Prefixes.Add($Prefix)
}
if ($storageRouteEnabled) {
    $listener.Prefixes.Add($storagePrefix)
}
$listener.Start()

function Write-WebhookLog {
    param(
        [string]$Level,
        [string]$Message,
        [hashtable]$Data = @{}
    )

    $path = Join-Path $LogDirectory ('koxo-webhook-{0}.log' -f (Get-Date -Format 'yyyyMMdd'))
    $payload = [ordered]@{
        timestamp = (Get-Date).ToString('O')
        level = $Level
        message = $Message
    }

    foreach ($key in $Data.Keys) {
        $payload[$key] = $Data[$key]
    }

    # `-Encoding UTF8` explicite, comme `Write-KoxoSyncLog` dans le module :
    # sans lui, Add-Content ecrit dans la page de codes ANSI du systeme alors
    # que le fichier est relu en UTF-8. Les accents des messages d'exception y
    # devenaient illisibles, et surtout `Get-Content -Tail`, qui cherche les
    # fins de ligne a rebours avec l'encodage demande, se desalignait et rendait
    # une ligne tronquee en plein milieu d'un horodatage.
    Add-Content -LiteralPath $path -Value (($payload | ConvertTo-Json -Compress)) -Encoding UTF8
}

function Get-WebhookPayloadValue {
    param($Payload, [string]$Name)

    # Sous Set-StrictMode, `$payload.trigger` leve quand la propriete est
    # absente. Passer par PSObject.Properties rend un champ manquant
    # indistinguable d'un champ vide, ce qui est le comportement attendu ici :
    # ces trois champs ne servent qu'a la journalisation.
    if ($null -eq $Payload) {
        return $null
    }

    $property = $Payload.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return $null
    }

    [string]$property.Value
}

function ConvertTo-SafeFileNameFragment {
    param([string]$Value)

    # L'identifiant de correlation vient de l'appelant et nomme un fichier :
    # tout ce qui n'est pas alphanumerique, tiret ou soulignement est neutralise.
    ($Value -replace '[^A-Za-z0-9._-]', '_')
}

function Read-BearerToken {
    param([System.Net.HttpListenerRequest]$Request)

    $authorization = $Request.Headers['Authorization']
    if ([string]::IsNullOrWhiteSpace($authorization) -or -not $authorization.StartsWith('Bearer ')) {
        return $null
    }

    $tokenValue = $authorization.Substring('Bearer '.Length).Trim()
    if ([string]::IsNullOrWhiteSpace($tokenValue)) {
        return $null
    }

    return $tokenValue
}

function Write-JsonResponse {
    param(
        [System.Net.HttpListenerResponse]$Response,
        [int]$StatusCode,
        [hashtable]$Body
    )

    $buffer = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Compress -Depth 6))
    $Response.StatusCode = $StatusCode
    $Response.ContentType = 'application/json; charset=utf-8'
    $Response.ContentLength64 = $buffer.Length
    $Response.OutputStream.Write($buffer, 0, $buffer.Length)
    $Response.OutputStream.Close()
}

Write-WebhookLog -Level 'info' -Message 'KoXo webhook receiver started.' -Data @{
    prefix = $(if ($StorageOnly) { $storagePrefix } else { $Prefix })
    sync_script_path = $resolvedSyncScriptPath
    instance_config_path = $InstanceConfigPath
    storage_route_enabled = $storageRouteEnabled
    storage_only = [bool]$StorageOnly
}

try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $request = $context.Request
        $response = $context.Response

        try {
            if (-not [string]::Equals($request.HttpMethod, 'POST', [System.StringComparison]::OrdinalIgnoreCase)) {
                Write-JsonResponse -Response $response -StatusCode 405 -Body @{
                    code = 'METHOD_NOT_ALLOWED'
                    message = 'Use POST.'
                }
                continue
            }

            # Routage AVANT toute action : la route de stockage ne doit jamais
            # tomber dans la branche de synchronisation globale, dont un
            # passage errone desactive des comptes.
            $requestPath = $request.Url.AbsolutePath.TrimEnd('/')
            $isStorageRequest = $storageRouteEnabled -and [string]::Equals(
                $requestPath,
                $normalizedStoragePath,
                [System.StringComparison]::OrdinalIgnoreCase)
            $isSyncRequest = -not $StorageOnly -and [string]::Equals(
                $requestPath,
                $syncPath,
                [System.StringComparison]::OrdinalIgnoreCase)

            $isQualityProofRequest = $qualityProofEnabled -and [string]::Equals(
                $requestPath, $qualityProofPath, [System.StringComparison]::OrdinalIgnoreCase)
            if (-not $isStorageRequest -and -not $isSyncRequest -and -not $isQualityProofRequest) {
                Write-JsonResponse -Response $response -StatusCode 404 -Body @{
                    code = 'NOT_FOUND'
                    message = 'Unknown operation.'
                }
                continue
            }

            $expectedToken = if ($isStorageRequest) { $resolvedStorageToken } else { $resolvedToken }
            $providedToken = Read-BearerToken -Request $request
            if (-not [string]::Equals($providedToken, $expectedToken, [System.StringComparison]::Ordinal)) {
                Write-WebhookLog -Level 'warning' -Message 'KoXo webhook unauthorized.' -Data @{
                    remote = $request.RemoteEndPoint.ToString()
                }
                Write-JsonResponse -Response $response -StatusCode 401 -Body @{
                    code = 'UNAUTHORIZED'
                    message = 'A valid bearer token is required.'
                }
                continue
            }

            $reader = [System.IO.StreamReader]::new($request.InputStream, $request.ContentEncoding)
            try {
                if ($isQualityProofRequest) {
                    $characters = New-Object char[] 131073
                    $count = $reader.ReadBlock($characters, 0, $characters.Length)
                    if ($count -gt 131072) {
                        Write-JsonResponse -Response $response -StatusCode 413 -Body @{code='PROOF_TOO_LARGE'}
                        continue
                    }
                    $body = [string]::new($characters, 0, $count)
                } else { $body = $reader.ReadToEnd() }
            } finally { $reader.Dispose() }
            if ($isQualityProofRequest) {
                # Route distincte : un ancien recepteur repond 404, jamais une
                # synchronisation accidentelle pour une demande de preuve.
                $proofLock = $null
                try {
                    $proofRequest = $body | ConvertFrom-Json
                    if ($proofRequest.customerId -notmatch '^[0-9a-fA-F-]{36}$' -or
                        [long]$proofRequest.revision -le 0 -or $proofRequest.desiredSha256 -notmatch '^[A-F0-9]{64}$' -or
                        @($proofRequest.targets).Count -lt 1 -or @($proofRequest.targets).Count -gt 128) { throw 'Invalid proof contract' }
                    $proofLock = Enter-KoxoAdmLock -TimeoutSeconds 2 -Holder 'qualities-proof'
                    if (Get-Process KoXoAdm -ErrorAction SilentlyContinue) { throw 'KoXo is active' }
                    $profile = @($instance.Profiles | Where-Object {$_.PrimaryGroup -eq 'CLIENTS DEV'})[0]
                    Test-KoxoCsvFile -Path $profile.CsvTargetPath -EncodingName utf8bom | Out-Null
                    $proofs = @($proofRequest.targets | ForEach-Object {
                        $proof = Get-KoxoDevQualityProof -DataRoot (Join-Path $instance.KoxoWorkingDirectory 'Data') `
                            -CsvPath $profile.CsvTargetPath -SecondaryGroup $_.secondaryGroup `
                            -UniqueId $_.uniqueId -ExpectedGroups @($_.groups)
                        @{portalUserId=$_.portalUserId;uniqueId=$proof.UniqueId;userId=$proof.UserId;
                            csvVerified=$proof.CsvVerified;koxoVerified=$proof.KoxoVerified}
                    })
                    Write-JsonResponse -Response $response -StatusCode 200 -Body @{
                        protocolVersion=1;customerId=$proofRequest.customerId;revision=$proofRequest.revision;
                        desiredSha256=$proofRequest.desiredSha256;targets=$proofs
                    }
                } catch {
                    Write-JsonResponse -Response $response -StatusCode 409 -Body @{code='KOXO_QUALITY_PROOF_UNAVAILABLE'}
                } finally {
                    if ($null -ne $proofLock) { Exit-KoxoAdmLock -LockHandle $proofLock }
                }
                continue
            }
            if ($isStorageRequest) {
                # Traitement SYNCHRONE : l'appelant a besoin du constat, pas
                # d'un accuse de prise en compte. Le verrou partage serialise
                # de toute facon les invocations de KoXoAdm.exe.
                try {
                    $storageRequest = Read-KoxoStorageRequest -Body $body
                    if ($null -ne $instance -and
                        $storageRequest.PrimaryGroup -notin @($instance.Profiles | ForEach-Object { $_.PrimaryGroup })) {
                        throw 'The storage target is outside the configured instance profiles.'
                    }
                }
                catch {
                    Write-WebhookLog -Level 'warning' -Message 'KoXo storage request rejected.' -Data @{
                        reason = $_.Exception.Message
                    }
                    Write-JsonResponse -Response $response -StatusCode 400 -Body @{
                        code = 'INVALID_REQUEST'
                        message = 'The storage reconcile contract was not respected.'
                    }
                    continue
                }

                # Un echec de reconciliation est une issue normale, pas une
                # panne du service : il est rendu avec la cible concernee pour
                # que l'appelant sache exactement ce qui n'a pas ete applique.
                try {
                    $storageResult = Invoke-KoxoStorageReconcile `
                        -Configuration $storageConfiguration `
                        -TargetKind $storageRequest.TargetKind `
                        -PrimaryGroup $storageRequest.PrimaryGroup `
                        -SecondaryGroup $storageRequest.SecondaryGroup `
                        -UserId $storageRequest.UserId `
                        -DesiredQuotaMib $storageRequest.DesiredQuotaMib `
                        -CorrelationId $storageRequest.CorrelationId `
                        -TargetKey $storageRequest.TargetKey `
                        -SubscriptionItemId $storageRequest.SubscriptionItemId
                }
                catch {
                    Write-WebhookLog -Level 'error' -Message 'KoXo storage reconcile failed.' -Data @{
                        correlation_id = $storageRequest.CorrelationId
                        target_key = $storageRequest.TargetKey
                        exception = $_.Exception.Message
                    }
                    $storageResult = New-KoxoStorageResult `
                        -Status 'failed' `
                        -ReasonCode 'BILLING_V2_KOXO_STORAGE_REPAIR_FAILED' `
                        -Verification 'none' `
                        -TargetKey $storageRequest.TargetKey `
                        -CorrelationId $storageRequest.CorrelationId
                }

                Write-JsonResponse -Response $response -StatusCode 200 -Body @{
                    status = $storageResult.status
                    reasonCode = $storageResult.reasonCode
                    verification = $storageResult.verification
                    targetKey = $storageResult.targetKey
                    correlationId = $storageResult.correlationId
                }
                continue
            }

            $payload = if ([string]::IsNullOrWhiteSpace($body)) {
                $null
            } else {
                $body | ConvertFrom-Json
            }

            # Tout ce qui est tire de la charge est resolu AVANT le lancement :
            # une lecture qui leve apres `Start-Process` rendrait un echec a
            # l'appelant pour une synchronisation reellement demarree, et
            # l'appelant pourrait rejouer contre le verrou.
            $correlationId = Get-WebhookPayloadValue -Payload $payload -Name 'correlationId'
            if ([string]::IsNullOrWhiteSpace($correlationId)) {
                $correlationId = [guid]::NewGuid().ToString('D')
            }

            $trigger = Get-WebhookPayloadValue -Payload $payload -Name 'trigger'
            $publishCsvOnly = $qualityProofEnabled -and $trigger -eq 'qualities_changed'
            # L'actualisation du CSV ne lance pas KoXo. Les autres synchronisations
            # restent interdites lorsqu'une console interactive est ouverte.
            if ($qualityProofEnabled -and -not $publishCsvOnly -and @(Get-Process KoXoAdm -ErrorAction SilentlyContinue | Where-Object {$_.SessionId -ne 0}).Count -gt 0) {
                Write-JsonResponse -Response $response -StatusCode 409 -Body @{code='KOXO_INTERACTIVE_CONSOLE_ACTIVE'}
                continue
            }
            $portalUserId = Get-WebhookPayloadValue -Payload $payload -Name 'portalUserId'
            $customerReference = Get-WebhookPayloadValue -Payload $payload -Name 'customerReference'

            $fileFragment = ConvertTo-SafeFileNameFragment -Value $correlationId
            $stdoutPath = Join-Path $LogDirectory ('koxo-sync-child-{0}.stdout.log' -f $fileFragment)
            $stderrPath = Join-Path $LogDirectory ('koxo-sync-child-{0}.stderr.log' -f $fileFragment)

            $syncArguments = (
                '-NoProfile -NonInteractive -ExecutionPolicy Bypass ' +
                '-File "{0}" -SyncScriptPath "{1}" -CsvTargetPath "{2}" ' +
                '-WorkingDirectory "{3}" -KoxoExecutablePath "{4}" ' +
                '-KoxoWorkingDirectory "{5}" -KoxoSyncArgument "{6}"{7}'
            ) -f (
                $resolvedWebhookSyncLauncherPath,
                $resolvedSyncScriptPath,
                $resolvedCsvTargetPath,
                $resolvedWorkingDirectory,
                $resolvedKoxoExecutablePath,
                $resolvedKoxoWorkingDirectory,
                $KoxoSyncArgument,
                $instanceArguments
            )

            if ($publishCsvOnly) { $syncArguments += ' -PublishCsvOnly' }
            $process = Start-Process -FilePath 'powershell.exe' `
                -ArgumentList $syncArguments `
                -RedirectStandardOutput $stdoutPath `
                -RedirectStandardError $stderrPath `
                -WorkingDirectory $resolvedKoxoWorkingDirectory `
                -WindowStyle Hidden `
                -PassThru

            # La synchronisation tourne deja : un incident de journalisation ne
            # doit pas la faire remonter comme un echec a l'appelant.
            try {
                Write-WebhookLog -Level 'info' -Message 'KoXo webhook sync queued.' -Data @{
                    correlation_id = $correlationId
                    trigger = $trigger
                    portal_user_id = $portalUserId
                    customer_reference = $customerReference
                    process_id = $process.Id
                    stdout_path = $stdoutPath
                    stderr_path = $stderrPath
                }
            }
            catch {
            }

            Write-JsonResponse -Response $response -StatusCode 202 -Body @{
                status = 'queued'
                correlation_id = $correlationId
                process_id = $process.Id
            }
        }
        catch {
            Write-WebhookLog -Level 'error' -Message 'KoXo webhook request failed.' -Data @{
                exception = $_.Exception.Message
            }
            Write-JsonResponse -Response $response -StatusCode 500 -Body @{
                code = 'INTERNAL_ERROR'
                message = 'Webhook processing failed.'
            }
        }
    }
}
finally {
    if ($listener.IsListening) {
        $listener.Stop()
    }

    $listener.Close()
    Write-WebhookLog -Level 'info' -Message 'KoXo webhook receiver stopped.'
}
