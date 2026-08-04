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
    [string]$LogDirectory = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\Logs\webhook'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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

$resolvedSyncScriptPath = [System.IO.Path]::GetFullPath($SyncScriptPath)
$resolvedWebhookSyncLauncherPath = [System.IO.Path]::GetFullPath($WebhookSyncLauncherPath)
$resolvedCsvTargetPath = [System.IO.Path]::GetFullPath($CsvTargetPath)
$resolvedWorkingDirectory = [System.IO.Path]::GetFullPath($WorkingDirectory)
$resolvedKoxoExecutablePath = [System.IO.Path]::GetFullPath($KoxoExecutablePath)
$resolvedKoxoWorkingDirectory = [System.IO.Path]::GetFullPath($KoxoWorkingDirectory)
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add($Prefix)
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

    $buffer = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Compress))
    $Response.StatusCode = $StatusCode
    $Response.ContentType = 'application/json; charset=utf-8'
    $Response.ContentLength64 = $buffer.Length
    $Response.OutputStream.Write($buffer, 0, $buffer.Length)
    $Response.OutputStream.Close()
}

Write-WebhookLog -Level 'info' -Message 'KoXo webhook receiver started.' -Data @{
    prefix = $Prefix
    sync_script_path = $resolvedSyncScriptPath
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

            $providedToken = Read-BearerToken -Request $request
            if (-not [string]::Equals($providedToken, $resolvedToken, [System.StringComparison]::Ordinal)) {
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
            $body = $reader.ReadToEnd()
            $reader.Dispose()

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
            $portalUserId = Get-WebhookPayloadValue -Payload $payload -Name 'portalUserId'
            $customerReference = Get-WebhookPayloadValue -Payload $payload -Name 'customerReference'

            $fileFragment = ConvertTo-SafeFileNameFragment -Value $correlationId
            $stdoutPath = Join-Path $LogDirectory ('koxo-sync-child-{0}.stdout.log' -f $fileFragment)
            $stderrPath = Join-Path $LogDirectory ('koxo-sync-child-{0}.stderr.log' -f $fileFragment)

            $syncArguments = (
                '-NoProfile -NonInteractive -ExecutionPolicy Bypass ' +
                '-File "{0}" -SyncScriptPath "{1}" -CsvTargetPath "{2}" ' +
                '-WorkingDirectory "{3}" -KoxoExecutablePath "{4}" ' +
                '-KoxoWorkingDirectory "{5}" -KoxoSyncArgument "{6}"'
            ) -f (
                $resolvedWebhookSyncLauncherPath,
                $resolvedSyncScriptPath,
                $resolvedCsvTargetPath,
                $resolvedWorkingDirectory,
                $resolvedKoxoExecutablePath,
                $resolvedKoxoWorkingDirectory,
                $KoxoSyncArgument
            )

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
