<#
.SYNOPSIS
    Enregistre la tache planifiee Windows « filet de securite » du cycle de vie
    des comptes de demonstration (V1.1 Lot 3), sur KERMARIA-SRV-13.

.DESCRIPTION
    Le service de fond de l'API interne (DemoAccountExpirationWorker) balaie deja
    les echeances au demarrage puis toutes les heures. Cette tache planifiee est
    un DOUBLON de securite : si le service etait arrete, elle rejoue le meme
    balayage (revocation des essais echus + purge) via l'argument CLI
    --run-demo-expiration, puis le processus quitte immediatement.

    Le balayage est inerte en persistance mock ; il n'agit qu'en base MariaDB.
    L'argument --run-demo-expiration ne demande aucune authentification (il ne
    passe pas par HTTP) et n'ouvre aucun port.

.PARAMETER DotnetPath
    Chemin de l'executable dotnet (defaut : dotnet dans le PATH).

.PARAMETER AppDll
    Chemin complet de Kermaria.ApiInternal.dll publie sur SRV-13.

.PARAMETER WorkingDirectory
    Repertoire de travail (racine de publication, ou se trouve appsettings +
    les secrets d'environnement). Defaut : dossier de AppDll.

.PARAMETER RunAsUser
    Compte de service exécutant la tache. DOIT etre le compte applicatif qui
    porte la chaine de connexion MariaDB et, le cas echeant, svc_api_portal_ad
    (retrait direct des groupes GG_DEMO_* en AD). Defaut : SYSTEM (a adapter).

.PARAMETER TimeOfDay
    Heure quotidienne du filet (defaut 03:15). Le service de fond couvre l'heure
    de la journee ; ce filet quotidien suffit largement.

.EXAMPLE
    .\Register-DemoExpirationTask.ps1 `
        -AppDll 'D:\apps\api-internal\Kermaria.ApiInternal.dll' `
        -RunAsUser 'CLIENTS\svc_api_portal'
#>

[CmdletBinding()]
param(
    [string] $DotnetPath = 'dotnet',
    [Parameter(Mandatory = $true)]
    [string] $AppDll,
    [string] $WorkingDirectory,
    [string] $RunAsUser = 'SYSTEM',
    [string] $TimeOfDay = '03:15',
    [string] $TaskName = 'Kermaria-Demo-Expiration'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $AppDll)) {
    throw "Introuvable : $AppDll"
}

if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
    $WorkingDirectory = Split-Path -Parent $AppDll
}

# Argument CLI gere dans Program.cs : construit l'hote, rejoue le balayage
# d'expiration (revocation + purge) puis quitte.
$action = New-ScheduledTaskAction `
    -Execute $DotnetPath `
    -Argument ('"{0}" --run-demo-expiration' -f $AppDll) `
    -WorkingDirectory $WorkingDirectory

# Filet quotidien (le service de fond assure la couverture horaire).
$trigger = New-ScheduledTaskTrigger -Daily -At $TimeOfDay

$settings = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -DontStopOnIdleEnd `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 30) `
    -MultipleInstances IgnoreNew

if ($RunAsUser -eq 'SYSTEM') {
    $principal = New-ScheduledTaskPrincipal `
        -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
}
else {
    # Compte de service gere (gMSA) ou compte dedie : le mot de passe n'est PAS
    # stocke ici (LogonType Password suppose une saisie hors script, ou gMSA).
    $principal = New-ScheduledTaskPrincipal `
        -UserId $RunAsUser -LogonType Password -RunLevel Highest
}

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Description 'V1.1 Lot 3 - filet de securite : revocation + purge des comptes de demonstration echus.' `
    -Force | Out-Null

Write-Host "Tache planifiee '$TaskName' enregistree (quotidien $TimeOfDay, compte $RunAsUser)."
Write-Host "Test manuel : Start-ScheduledTask -TaskName '$TaskName'"
