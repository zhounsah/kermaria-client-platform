<#
.SYNOPSIS
    Preflight LECTURE SEULE de la chaine de provisioning du stockage KoXo.

.DESCRIPTION
    Ne modifie RIEN : ni base, ni annuaire, ni fiche KoXo, ni quota. Aucun appel
    n'est emis vers la route de reconciliation, aucun KoXoAdm.exe n'est lance.

    Le script suit la chaine reelle et s'arrete au premier maillon qu'il ne peut
    pas prouver :

        portal_users.id
          -> portal_users.koxo_unique_identifier (CLI-NNNNNN)
          -> attribut AD employeeNumber
          -> objet d'annuaire unique (objectGUID / objectSID / sAMAccountName)
          -> customer_ad_links (exactement un lien)
          -> fiche KoXo Data\Users\<PRIMAIRE>\<SECONDAIRE>\<sAMAccountName>.xml
          -> quota actuellement enregistre

.PARAMETER AdServer
    Controleur de domaine interroge. RDC-07 appartient a HOME.BZH, alors que
    les identites KoXo vivent dans le domaine ENFANT clients.home.bzh : sans
    borne explicite, la recherche part dans le mauvais domaine et ne trouve
    jamais l'utilisateur. La valeur par defaut est celle qui a rendu
    CLI-000001 lors de la verification manuelle. Le FQDN du domaine
    (clients.home.bzh) convient aussi, au prix d'un controleur choisi par DNS.

.PARAMETER AdSearchBase
    Racine de recherche, en DN. Volontairement la racine du domaine enfant et
    non AD_CLIENTS_OU_DN : cette variable d'environnement porte encore un DN du
    domaine PARENT (voir AGENTS.md), la reprendre reintroduirait exactement la
    panne corrigee ici.

.NOTES
    A LANCER DEPUIS RDC-07, PAS DEPUIS UNE SESSION WinRM. Une requete LDAP
    emise depuis une session WinRM echoue par double saut : l'identite n'est pas
    deleguee au controleur de domaine.

    Le compte MariaDB utilise doit etre en LECTURE SEULE. Le compte applicatif
    kermaria_api suffit largement ; ne pas fournir kermaria_migrator.

.EXAMPLE
    .\Test-KoxoStorageReadiness.ps1 -PortalUserId '0f1e...' -SqlUsername kermaria_api

.EXAMPLE
    .\Test-KoxoStorageReadiness.ps1 -PortalUserId '0f1e...' `
        -AdServer 'clients.home.bzh' -AdSearchBase 'OU=KoXoAdm,DC=clients,DC=home,DC=bzh'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PortalUserId,

    [string]$SqlHost = $env:SQL_HOST,
    [int]$SqlPort = $(if ($env:SQL_PORT) { [int]$env:SQL_PORT } else { 3306 }),
    [string]$SqlDatabase = $env:SQL_DATABASE,
    [string]$SqlUsername = $env:SQL_USERNAME,
    [string]$SqlPassword = $env:SQL_PASSWORD,
    [string]$MysqlClientPath = 'mysql.exe',

    # Bornage LDAP explicite. Aucune valeur par defaut n'est prise dans
    # l'environnement : AD_DOMAIN vaut home.bzh et AD_CLIENTS_OU_DN porte un DN
    # du domaine parent, donc les reprendre ramenerait la recherche dans le
    # mauvais domaine. La convention C# (AdRuntimeConfiguration.BuildLdapPath)
    # reste respectee : "LDAP://<serveur ou domaine>/<DN>".
    [string]$AdServer = 'KERMARIA-SRV-21.clients.home.bzh',
    [string]$AdSearchBase = 'DC=clients,DC=home,DC=bzh',

    # Racine des donnees KoXo. Renseignee uniquement si ce poste voit le disque
    # de SRV-21 ; sinon la derniere etape est declaree non verifiee, jamais
    # supposee bonne.
    [string]$KoxoDataRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'KoxoStorage.Common.psm1') -Force -DisableNameChecking

$script:Findings = New-Object System.Collections.Generic.List[object]

function Add-Finding {
    param([string]$Step, [string]$Status, [string]$Detail)

    $script:Findings.Add([pscustomobject]@{
        Step = $Step
        Status = $Status
        Detail = $Detail
    })
}

function Invoke-ReadOnlyQuery {
    param([string]$Sql)

    if ([string]::IsNullOrWhiteSpace($SqlHost) -or
        [string]::IsNullOrWhiteSpace($SqlDatabase) -or
        [string]::IsNullOrWhiteSpace($SqlUsername)) {
        throw 'SQL_HOST, SQL_DATABASE and SQL_USERNAME are required.'
    }

    # Windows PowerShell 5.1 transforme chaque ligne stderr d'un executable
    # natif en ErrorRecord : le client MariaDB 12.x emet un avertissement a
    # chaque appel, ce qui couperait le script en pleine execution avec
    # $ErrorActionPreference = 'Stop'. On neutralise localement et on ne juge
    # que sur $LASTEXITCODE.
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $mysqlExitCode = $null
    try {
        $output = & $MysqlClientPath `
            "--host=$SqlHost" `
            "--port=$SqlPort" `
            "--user=$SqlUsername" `
            "--password=$SqlPassword" `
            '--batch' '--raw' '--skip-column-names' `
            $SqlDatabase `
            '-e' $Sql 2>&1
        # $LASTEXITCODE est global et reflete la DERNIERE commande native
        # executee : le releve doit suivre immediatement l'invocation. Le lire
        # apres le finally laisserait le restaurateur de preference, ou tout
        # autre code intercale, ecraser la valeur du client MariaDB.
        $mysqlExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previous
    }

    if ($mysqlExitCode -ne 0) {
        throw ("MariaDB query failed with exit code {0}." -f $mysqlExitCode)
    }

    # PowerShell reenumere ce que rend une fonction : sans l'operateur virgule,
    # une requete a une seule ligne rendrait un scalaire et $rows.Count
    # echouerait sous StrictMode. On rend donc toujours le tableau lui-meme,
    # comme objet unique, pour 0, 1 ou N lignes.
    $rows = @($output | Where-Object { $_ -is [string] -and $_ -notmatch '^(Warning|mysql:)' })
    return ,$rows
}

Write-Host 'Preflight KoXo Storage - LECTURE SEULE, aucune modification.' -ForegroundColor Cyan
Write-Host ''

# ---------------------------------------------------------------------------
# 1. portal_users -> identifiant unique + etat du client.
# ---------------------------------------------------------------------------
$escapedPortalUserId = $PortalUserId -replace "'", "''"
$userRows = Invoke-ReadOnlyQuery -Sql @"
SELECT pu.id, pu.customer_id, pu.koxo_unique_identifier,
       c.external_reference, c.is_demo, c.koxo_group_reference
FROM portal_users pu
INNER JOIN customers c ON c.id = pu.customer_id
WHERE pu.id = '$escapedPortalUserId'
LIMIT 1;
"@

if ($userRows.Count -eq 0) {
    Add-Finding -Step '1. portal_users' -Status 'BLOQUANT' -Detail 'Aucun utilisateur portail pour cet identifiant.'
    $script:Findings | Format-Table -AutoSize
    exit 1
}

$userFields = $userRows[0] -split "`t"
$customerId = $userFields[1]
$employeeNumber = $userFields[2]
$customerReference = $userFields[3]
$isDemo = ($userFields[4] -eq '1')
$koxoGroupReference = if ($userFields[5] -eq 'NULL') { $null } else { $userFields[5] }

Add-Finding -Step '1. portal_users' -Status 'OK' -Detail ("client {0}, reference {1}, demo={2}" -f $customerId, $customerReference, $isDemo)

if (-not ($employeeNumber -match '^CLI-\d{6}$')) {
    Add-Finding -Step '2. koxo_unique_identifier' -Status 'BLOQUANT' -Detail ("Forme inattendue : '{0}'. Attendu CLI-NNNNNN." -f $employeeNumber)
    $script:Findings | Format-Table -AutoSize
    exit 1
}

Add-Finding -Step '2. koxo_unique_identifier' -Status 'OK' -Detail $employeeNumber

# ---------------------------------------------------------------------------
# 2. customer_ad_links : exactement un lien, et il appartient a cet utilisateur.
# ---------------------------------------------------------------------------
# La reference client n'existe pas sur customer_ad_links (migration 007) : elle
# vit sur customers.external_reference. La selectionner directement rendait
# ERROR 1054 (42S22) et faisait echouer tout le preflight.
$linkRows = Invoke-ReadOnlyQuery -Sql @"
SELECT
    cal.object_guid,
    cal.object_sid,
    cal.sam_account_name,
    cal.customer_id,
    c.external_reference AS customer_reference
FROM customer_ad_links cal
INNER JOIN customers c
    ON c.id = cal.customer_id
WHERE cal.portal_user_id = '$escapedPortalUserId'
  AND cal.object_type = 'user';
"@

if ($linkRows.Count -eq 0) {
    Add-Finding -Step '3. customer_ad_links' -Status 'BLOQUANT' -Detail "Aucun lien annuaire. L'identite n'est pas materialisee ; le quota ne la creera pas."
    $script:Findings | Format-Table -AutoSize
    exit 1
}

if ($linkRows.Count -gt 1) {
    Add-Finding -Step '3. customer_ad_links' -Status 'BLOQUANT' -Detail ("{0} liens pour un seul utilisateur : ambigu, aucun arbitrage implicite." -f $linkRows.Count)
    $script:Findings | Format-Table -AutoSize
    exit 1
}

$linkFields = $linkRows[0] -split "`t"
$linkGuid = $linkFields[0]
$linkSid = $linkFields[1]
$samAccountName = $linkFields[2]

if ($linkFields[3] -ne $customerId) {
    Add-Finding -Step '3. customer_ad_links' -Status 'BLOQUANT' -Detail 'Le lien appartient a un autre client.'
    $script:Findings | Format-Table -AutoSize
    exit 1
}

Add-Finding -Step '3. customer_ad_links' -Status 'OK' -Detail ("GUID {0}, sAMAccountName {1}" -f $linkGuid, $samAccountName)

# ---------------------------------------------------------------------------
# 3. Annuaire : resolution par employeeNumber, jamais par nom.
# ---------------------------------------------------------------------------
# Un DirectorySearcher sans SearchRoot interroge le domaine COURANT. Lance
# depuis RDC-07, membre de HOME.BZH, il cherchait donc dans home.bzh alors que
# les identites KoXo vivent dans l'enfant clients.home.bzh : zero resultat, et
# un « aucun objet ne porte cet employeeNumber » trompeur. La racine est
# desormais posee explicitement.
$adObjects = New-Object System.Collections.Generic.List[object]
$ldapFailed = $false
$directoryRoot = $null
$searcher = $null
$results = $null
try {
    $directoryRoot = [System.DirectoryServices.DirectoryEntry]::new(
        "LDAP://$AdServer/$AdSearchBase")
    $searcher = [System.DirectoryServices.DirectorySearcher]::new($directoryRoot)
    # Resolution par employeeNumber UNIQUEMENT : le nom est translittere par
    # KoXo et le sAMAccountName derive a la creation, donc aucun repli par SAM,
    # nom, UPN ou DN n'est admissible.
    $searcher.Filter = "(&(objectClass=user)(employeeNumber=$employeeNumber))"
    $searcher.SearchScope = 'Subtree'
    # 2 suffit a distinguer « un seul » de « plusieurs ».
    $searcher.SizeLimit = 2
    $searcher.PageSize = 2
    [void]$searcher.PropertiesToLoad.Add('objectGUID')
    [void]$searcher.PropertiesToLoad.Add('objectSid')
    [void]$searcher.PropertiesToLoad.Add('sAMAccountName')
    [void]$searcher.PropertiesToLoad.Add('distinguishedName')

    $results = $searcher.FindAll()
    # Les valeurs sont extraites tant que la collection vit : apres Dispose,
    # les handles sous-jacents ne sont plus garantis.
    foreach ($result in $results) {
        $adObjects.Add([pscustomobject]@{
            Guid = ([guid]$result.Properties['objectguid'][0]).ToString('D')
            Sid = (New-Object System.Security.Principal.SecurityIdentifier(
                $result.Properties['objectsid'][0], 0)).Value
            Sam = [string]$result.Properties['samaccountname'][0]
            Dn = [string]$result.Properties['distinguishedname'][0]
        })
    }
}
catch {
    # Fail-closed : une recherche impossible n'est jamais un « aucun objet ».
    $ldapFailed = $true
    Add-Finding -Step '4. annuaire' -Status 'NON VERIFIE' -Detail ("Recherche LDAP impossible sur LDAP://{0}/{1} : {2}. Relancer HORS session WinRM." -f $AdServer, $AdSearchBase, $_.Exception.Message)
}
finally {
    if ($null -ne $results) { $results.Dispose() }
    if ($null -ne $searcher) { $searcher.Dispose() }
    if ($null -ne $directoryRoot) { $directoryRoot.Dispose() }
}

if ($adObjects.Count -eq 1) {
    $adGuid = $adObjects[0].Guid
    $adSid = $adObjects[0].Sid
    $adSam = $adObjects[0].Sam
    $adDn = $adObjects[0].Dn

    $coherent = ($adGuid -eq $linkGuid) -and ($adSid -eq $linkSid) -and ($adSam -eq $samAccountName)
    Add-Finding -Step '4. annuaire' `
        -Status $(if ($coherent) { 'OK' } else { 'BLOQUANT' }) `
        -Detail ("DN {0} ; triplet GUID/SID/SAM {1}" -f $adDn, $(if ($coherent) { 'coherent avec le lien' } else { 'DIVERGENT du lien enregistre' }))
}
elseif ($adObjects.Count -gt 1) {
    Add-Finding -Step '4. annuaire' -Status 'BLOQUANT' -Detail ("{0} objets portent cet employeeNumber : aucune designation possible." -f $adObjects.Count)
}
elseif (-not $ldapFailed) {
    Add-Finding -Step '4. annuaire' -Status 'BLOQUANT' -Detail 'Aucun objet ne porte cet employeeNumber.'
}

# ---------------------------------------------------------------------------
# 4. Emplacement de la fiche KoXo et quota actuel.
# ---------------------------------------------------------------------------
$primaryGroup = if ($isDemo) { 'CLIENTS D' + [char]0x00C9 + 'MO' } else { 'CLIENTS' }
$secondaryGroup = if ($isDemo) {
    'DEMO-' + $(if ($koxoGroupReference) { $koxoGroupReference } else { 'CLI-DEMO' })
} else {
    if ($koxoGroupReference) { $koxoGroupReference } else { $customerReference }
}

Add-Finding -Step '5. topologie' -Status 'OK' -Detail ("primaire '{0}', secondaire '{1}'" -f $primaryGroup, $secondaryGroup)

if ([string]::IsNullOrWhiteSpace($KoxoDataRoot)) {
    Add-Finding -Step '6. fiche KoXo' -Status 'NON VERIFIE' -Detail ("Relancer avec -KoxoDataRoot depuis un poste voyant le disque de SRV-21. Chemin attendu : Data\Users\{0}\{1}\{2}.xml" -f $primaryGroup, $secondaryGroup, $samAccountName)
}
else {
    $sheetPath = Resolve-KoxoStorageTargetPath `
        -UsersRoot (Join-Path $KoxoDataRoot 'Users') `
        -TargetKind 'user' `
        -PrimaryGroup $primaryGroup `
        -SecondaryGroup $secondaryGroup `
        -UserId $samAccountName

    $state = Read-KoxoStorageQuotaState -Path $sheetPath
    if (-not $state.Exists) {
        Add-Finding -Step '6. fiche KoXo' -Status 'BLOQUANT' -Detail ("Fiche absente : {0}" -f $sheetPath)
    }
    elseif ($state.Ambiguous) {
        Add-Finding -Step '6. fiche KoXo' -Status 'BLOQUANT' -Detail ("Fiche illisible : {0}" -f $state.AmbiguityReason)
    }
    else {
        Add-Finding -Step '6. fiche KoXo' -Status 'OK' -Detail ("{0} : EnableFolderQuota={1}, FolderQuota={2} MiB" -f $sheetPath, $state.Enabled, $state.QuotaMib)
    }

    # Egalement lu, parce qu'un stockage partage vise cette fiche-la.
    $sharedPath = Resolve-KoxoStorageTargetPath `
        -UsersRoot (Join-Path $KoxoDataRoot 'Users') `
        -TargetKind 'secondary_group' `
        -PrimaryGroup $primaryGroup `
        -SecondaryGroup $secondaryGroup

    $sharedState = Read-KoxoStorageQuotaState -Path $sharedPath
    Add-Finding -Step '7. fiche partagee' `
        -Status $(if ($sharedState.Exists -and -not $sharedState.Ambiguous) { 'OK' } else { 'BLOQUANT' }) `
        -Detail ("{0} : existe={1}, FolderQuota={2} MiB" -f $sharedPath, $sharedState.Exists, $sharedState.QuotaMib)
}

Write-Host ''
$script:Findings | Format-Table -AutoSize
Write-Host ''

$blocking = @($script:Findings | Where-Object { $_.Status -eq 'BLOQUANT' })
if ($blocking.Count -gt 0) {
    Write-Host ("Preflight NON CONCLUANT : {0} maillon(s) bloquant(s)." -f $blocking.Count) -ForegroundColor Red
    exit 1
}

$unverified = @($script:Findings | Where-Object { $_.Status -eq 'NON VERIFIE' })
if ($unverified.Count -gt 0) {
    Write-Host 'Preflight PARTIEL : aucun maillon bloquant, mais tout n a pas pu etre prouve depuis ce poste.' -ForegroundColor Yellow
    exit 2
}

Write-Host 'Preflight CONCLUANT : la chaine est prete. Aucune modification effectuee.' -ForegroundColor Green
exit 0
