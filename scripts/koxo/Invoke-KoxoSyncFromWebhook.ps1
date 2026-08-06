[CmdletBinding()]
param(
    # Conserve pour compatibilite : le recepteur le passe encore. Ce lanceur
    # n'appelle plus Sync-KoXoClients.ps1, qui ne sait piloter qu'un profil,
    # mais l'orchestrateur du module, qui les enchaine sur un export unique.
    [string]$SyncScriptPath = (Join-Path $PSScriptRoot 'Sync-KoXoClients.ps1'),
    [string]$CsvTargetPath = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients.csv',
    [string]$DemoCsvTargetPath = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients-demo.csv',
    [string]$WorkingDirectory = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\work',
    [string]$KoxoExecutablePath = 'C:\Program Files\KoXo Dev\KoXoAdm\KoXoAdm.exe',
    [string]$KoxoWorkingDirectory = 'C:\Program Files\KoXo Dev\KoXoAdm',
    [string]$KoxoSyncArgument = '/Synchro=CLIENTS.xml',
    [string]$DemoKoxoSyncArgument = '/Synchro=CLIENTS-DEMO.xml'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Ce fichier n'a pas de marque d'ordre d'octets, et PowerShell 5.1 relit alors
# un script en ANSI : un « E accent aigu » ecrit litteralement y arriverait
# mutile, et le profil viserait un groupe primaire inexistant — c'est-a-dire une
# synchronisation qui reussit sans rien faire. La sequence de code echappe a la
# question.
$primaryGroupClients = 'CLIENTS'
$primaryGroupDemo = 'CLIENTS D' + [char]0x00C9 + 'MO'

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
    'KOXO_ALLOW_USER_DROP',
    'KOXO_ALLOW_EMPTY_CSV',
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

# KOXO_OTHER_CSV_PATHS n'est volontairement PAS propagee : l'orchestrateur
# verifie l'exclusivite des identifiants sur l'export lui-meme, ou elle est
# exacte, plutot que sur des fichiers voisins encore periment en cours de
# passage.
[Environment]::SetEnvironmentVariable('KOXO_OTHER_CSV_PATHS', '', 'Process')

$modulePath = Join-Path $PSScriptRoot 'KoxoSync.Common.psm1'
Import-Module $modulePath -Force

# Un SEUL appel a l'API sert les deux profils. L'export consomme les mots de
# passe en attente : appeler deux fois priverait le second profil de sa
# colonne 14, donc laisserait ses comptes sur un mot de passe obsolete.
Invoke-KoxoSyncProfiles `
    -Profiles @(
        @{
            PrimaryGroup = $primaryGroupClients
            CsvTargetPath = [System.IO.Path]::GetFullPath($CsvTargetPath)
            KoxoSyncArgument = $KoxoSyncArgument
        },
        @{
            PrimaryGroup = $primaryGroupDemo
            CsvTargetPath = [System.IO.Path]::GetFullPath($DemoCsvTargetPath)
            KoxoSyncArgument = $DemoKoxoSyncArgument
        }
    ) `
    -WorkingDirectory $resolvedWorkingDirectory `
    -LaunchKoxo `
    -KoxoExecutablePath $resolvedKoxoExecutablePath `
    -KoxoWorkingDirectory $resolvedKoxoWorkingDirectory
