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
    [string]$DemoKoxoSyncArgument = '/Synchro=CLIENTS-DEMO.xml',

    # Instance ISOLEE (DEV) : chemin absolu de son fichier de definition JSON.
    # Absent, ce lanceur reste celui de l'instance de production, inchange.
    [string]$InstanceConfigPath = '',

    # Rend le plan effectif (sans jeton) et s'arrete : aucun appel a l'API,
    # aucun CSV ecrit, aucun KoXoAdm lance.
    [switch]$PlanOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$modulePath = Join-Path $PSScriptRoot 'KoxoSync.Common.psm1'
Import-Module $modulePath -Force

# Production : les enfants du recepteur peuvent heriter d'un environnement
# perime, donc les variables Machine KOXO_* sont rechargees dans le processus,
# et KOXO_OTHER_CSV_PATHS neutralisee (l'orchestrateur verifie l'exclusivite
# des identifiants sur l'export lui-meme, ou elle est exacte).
#
# Instance isolee : c'est l'inverse. Toute variable KOXO_* est retiree du
# processus, et chaque reglage vient du fichier de definition par surcharge
# explicite. Une variable Machine de production ne peut donc ni fournir ni
# remplacer un parametre de l'instance.
$plan = Resolve-KoxoSyncLaunchPlan `
    -InstanceConfigPath $InstanceConfigPath `
    -CsvTargetPath $CsvTargetPath `
    -DemoCsvTargetPath $DemoCsvTargetPath `
    -WorkingDirectory $WorkingDirectory `
    -KoxoExecutablePath $KoxoExecutablePath `
    -KoxoWorkingDirectory $KoxoWorkingDirectory `
    -KoxoSyncArgument $KoxoSyncArgument `
    -DemoKoxoSyncArgument $DemoKoxoSyncArgument

foreach ($entry in $plan.ProcessEnvironment.GetEnumerator()) {
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
}

if ($PlanOnly) {
    ConvertTo-KoxoSafeLaunchPlan -Plan $plan | ConvertTo-Json -Depth 5
    return
}

# Un SEUL appel a l'API sert tous les profils de l'instance. L'export consomme
# les mots de passe en attente : appeler deux fois priverait le second profil de
# sa colonne 14, donc laisserait ses comptes sur un mot de passe obsolete.
Invoke-KoxoSyncProfiles `
    -Profiles $plan.Profiles `
    -WorkingDirectory $plan.WorkingDirectory `
    -Overrides $plan.Overrides `
    -LaunchKoxo `
    -KoxoExecutablePath $plan.KoxoExecutablePath `
    -KoxoWorkingDirectory $plan.KoxoWorkingDirectory
