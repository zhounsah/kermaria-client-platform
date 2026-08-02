[CmdletBinding()]
param(
    [string]$Prefix = 'http://+:8041/internal/koxo/sync/',
    [string]$SyncScriptPath = (Join-Path $PSScriptRoot 'Sync-KoXoClients.ps1'),
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

    Add-Content -LiteralPath $path -Value (($payload | ConvertTo-Json -Compress))
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

            $correlationId = if ($payload -and $payload.correlationId) {
                [string]$payload.correlationId
            } else {
                [guid]::NewGuid().ToString('D')
            }

            $stdoutPath = Join-Path $LogDirectory ('koxo-sync-child-{0}.stdout.log' -f $correlationId)
            $stderrPath = Join-Path $LogDirectory ('koxo-sync-child-{0}.stderr.log' -f $correlationId)

            $syncCommand = @(
                '&',
                ('"{0}"' -f $resolvedSyncScriptPath),
                '-CsvTargetPath',
                ('"{0}"' -f $resolvedCsvTargetPath),
                '-WorkingDirectory',
                ('"{0}"' -f $resolvedWorkingDirectory),
                '-LaunchKoxo',
                '-KoxoExecutablePath',
                ('"{0}"' -f $resolvedKoxoExecutablePath),
                '-KoxoWorkingDirectory',
                ('"{0}"' -f $resolvedKoxoWorkingDirectory),
                '-KoxoSyncArgument',
                ('"{0}"' -f $KoxoSyncArgument)
            ) -join ' '

            $process = Start-Process -FilePath 'powershell.exe' `
                -ArgumentList @(
                    '-NoProfile',
                    '-NonInteractive',
                    '-ExecutionPolicy', 'Bypass',
                    '-Command', $syncCommand
                ) `
                -RedirectStandardOutput $stdoutPath `
                -RedirectStandardError $stderrPath `
                -WorkingDirectory $resolvedKoxoWorkingDirectory `
                -WindowStyle Hidden `
                -PassThru

            Write-WebhookLog -Level 'info' -Message 'KoXo webhook sync queued.' -Data @{
                correlation_id = $correlationId
                trigger = if ($payload -and $payload.trigger) { [string]$payload.trigger } else { $null }
                portal_user_id = if ($payload -and $payload.portalUserId) { [string]$payload.portalUserId } else { $null }
                customer_reference = if ($payload -and $payload.customerReference) { [string]$payload.customerReference } else { $null }
                process_id = $process.Id
                stdout_path = $stdoutPath
                stderr_path = $stderrPath
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
