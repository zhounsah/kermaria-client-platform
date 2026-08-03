[CmdletBinding()]
param(
    [string]$SyncScriptPath = (Join-Path $PSScriptRoot 'Sync-KoXoClients.ps1'),
    [string]$CsvTargetPath = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients.csv',
    [string]$WorkingDirectory = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\work',
    [string]$KoxoExecutablePath = 'C:\Program Files\KoXo Dev\KoXoAdm\KoXoAdm.exe',
    [string]$KoxoWorkingDirectory = 'C:\Program Files\KoXo Dev\KoXoAdm',
    [string]$KoxoSyncArgument = '/Synchro=CLIENTS.xml'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedSyncScriptPath = [System.IO.Path]::GetFullPath($SyncScriptPath)
$resolvedCsvTargetPath = [System.IO.Path]::GetFullPath($CsvTargetPath)
$resolvedWorkingDirectory = [System.IO.Path]::GetFullPath($WorkingDirectory)
$resolvedKoxoExecutablePath = [System.IO.Path]::GetFullPath($KoxoExecutablePath)
$resolvedKoxoWorkingDirectory = [System.IO.Path]::GetFullPath($KoxoWorkingDirectory)

# Receiver children may inherit a stale environment. Refresh the process scope
# from machine-level KoXo settings before invoking the real sync script.
foreach ($name in @(
    'KOXO_API_URL',
    'KOXO_API_TOKEN',
    'KOXO_ALLOW_INSECURE_HTTP',
    'KOXO_CSV_ENCODING',
    'KOXO_MIN_USER_COUNT',
    'KOXO_MAX_USER_DROP_PERCENT',
    'KOXO_SYNC_TIMEOUT_SECONDS',
    'KOXO_LOG_DIRECTORY',
    'KOXO_KOXO_LOG_GLOB',
    'KOXO_BACKUP_RETENTION_COUNT'
)) {
    $value = [Environment]::GetEnvironmentVariable($name, 'Machine')
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        [Environment]::SetEnvironmentVariable($name, $value, 'Process')
    }
}

& $resolvedSyncScriptPath `
    -CsvTargetPath $resolvedCsvTargetPath `
    -WorkingDirectory $resolvedWorkingDirectory `
    -LaunchKoxo `
    -KoxoExecutablePath $resolvedKoxoExecutablePath `
    -KoxoWorkingDirectory $resolvedKoxoWorkingDirectory `
    -KoxoSyncArgument $KoxoSyncArgument
