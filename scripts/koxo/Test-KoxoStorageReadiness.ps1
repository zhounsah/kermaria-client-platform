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

.NOTES
    A LANCER DEPUIS RDC-07, PAS DEPUIS UNE SESSION WinRM. Une requete LDAP
    emise depuis une session WinRM echoue par double saut : l'identite n'est pas
    deleguee au controleur de domaine.

    Le compte MariaDB utilise doit etre en LECTURE SEULE. Le compte applicatif
    kermaria_api suffit largement ; ne pas fournir kermaria_migrator.

.EXAMPLE
    .\Test-KoxoStorageReadiness.ps1 -PortalUserId '0f1e...' -SqlUsername kermaria_api
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
    try {
        $output = & $MysqlClientPath `
            "--host=$SqlHost" `
            "--port=$SqlPort" `
            "--user=$SqlUsername" `
            "--password=$SqlPassword" `
            '--batch' '--raw' '--skip-column-names' `
            $SqlDatabase `
            '-e' $Sql 2>&1
    }
    finally {
        $ErrorActionPreference = $previous
    }

    if ($LASTEXITCODE -ne 0) {
        throw ("MariaDB query failed with exit code {0}." -f $LASTEXITCODE)
    }

    @($output | Where-Object { $_ -is [string] -and $_ -notmatch '^(Warning|mysql:)' })
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
$linkRows = Invoke-ReadOnlyQuery -Sql @"
SELECT object_guid, object_sid, sam_account_name, customer_id, customer_reference
FROM customer_ad_links
WHERE portal_user_id = '$escapedPortalUserId';
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
try {
    $searcher = New-Object System.DirectoryServices.DirectorySearcher
    $searcher.Filter = "(&(objectClass=user)(employeeNumber=$employeeNumber))"
    [void]$searcher.PropertiesToLoad.Add('objectGUID')
    [void]$searcher.PropertiesToLoad.Add('objectSid')
    [void]$searcher.PropertiesToLoad.Add('sAMAccountName')
    [void]$searcher.PropertiesToLoad.Add('distinguishedName')
    $found = @($searcher.FindAll())
}
catch {
    Add-Finding -Step '4. annuaire' -Status 'NON VERIFIE' -Detail ("Recherche LDAP impossible : {0}. Relancer HORS session WinRM." -f $_.Exception.Message)
    $found = @()
}

if ($found.Count -eq 1) {
    $adGuid = ([guid]$found[0].Properties['objectguid'][0]).ToString('D')
    $adSid = (New-Object System.Security.Principal.SecurityIdentifier($found[0].Properties['objectsid'][0], 0)).Value
    $adSam = [string]$found[0].Properties['samaccountname'][0]
    $adDn = [string]$found[0].Properties['distinguishedname'][0]

    $coherent = ($adGuid -eq $linkGuid) -and ($adSid -eq $linkSid) -and ($adSam -eq $samAccountName)
    Add-Finding -Step '4. annuaire' `
        -Status $(if ($coherent) { 'OK' } else { 'BLOQUANT' }) `
        -Detail ("DN {0} ; triplet GUID/SID/SAM {1}" -f $adDn, $(if ($coherent) { 'coherent avec le lien' } else { 'DIVERGENT du lien enregistre' }))
}
elseif ($found.Count -gt 1) {
    Add-Finding -Step '4. annuaire' -Status 'BLOQUANT' -Detail ("{0} objets portent cet employeeNumber : aucune designation possible." -f $found.Count)
}
elseif ($script:Findings[-1].Step -ne '4. annuaire') {
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
