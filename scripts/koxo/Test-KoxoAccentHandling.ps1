<#
.SYNOPSIS
    Compare le nom envoye a KoXo et le nom reellement ecrit dans l'annuaire.

.DESCRIPTION
    Deux causes distinctes font perdre les majuscules accentuees, et elles
    appellent des corrections opposees :

    - corruption d'encodage : le CSV est relu en ANSI par KoXo, « LAUMAILLÉ »
      devient « LAUMAILLÃ‰ ». Correction = KOXO_CSV_ENCODING (cote depot).
    - translitteration : KoXo remplace « LAUMAILLÉ » par « LAUMAILLE ». Le
      caractere n'est pas corrompu, il est normalise. Correction = reglage KoXo
      ou reprise de l'attribut apres synchronisation (hors depot).

    Ce script lit le CSV effectivement consomme par KoXo, retrouve chaque
    identite par son attribut employeeNumber (seule cle fiable), puis classe
    l'ecart. Il n'ecrit rien : ni dans le CSV, ni dans l'annuaire.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\koxo\Test-KoxoAccentHandling.ps1 `
      -CsvPath "C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients.csv"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CsvPath,

    # Doit correspondre a KOXO_CSV_ENCODING sur la machine de synchronisation.
    [ValidateSet('utf8', 'utf8bom', 'unicode', 'ascii', 'latin1')]
    [string]$EncodingName = 'utf8bom',

    # Racine LDAP de recherche. Par defaut, le domaine de la machine courante.
    [string]$SearchRoot,

    # N'interroge pas l'annuaire : se limite a l'inspection du CSV.
    [switch]$SkipDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'KoxoSync.Common.psm1') -Force

function Remove-Diacritics {
    param([string]$Value)

    $decomposed = $Value.Normalize([Text.NormalizationForm]::FormD)
    $builder = New-Object System.Text.StringBuilder
    foreach ($character in $decomposed.ToCharArray()) {
        $category = [Globalization.CharUnicodeInfo]::GetUnicodeCategory($character)
        if ($category -ne [Globalization.UnicodeCategory]::NonSpacingMark) {
            [void]$builder.Append($character)
        }
    }

    $builder.ToString().Normalize([Text.NormalizationForm]::FormC)
}

function ConvertTo-AnsiMisread {
    # Simule ce que voit un lecteur ANSI face a des octets UTF-8 : c'est la
    # signature exacte de « LAUMAILLÉ » -> « LAUMAILLÃ‰ ».
    param([string]$Value)

    [Text.Encoding]::GetEncoding(1252).GetString([Text.Encoding]::UTF8.GetBytes($Value))
}

function Get-CsvRow {
    param([string]$Path, [string]$Encoding)

    Add-Type -AssemblyName Microsoft.VisualBasic | Out-Null
    $parser = New-Object Microsoft.VisualBasic.FileIO.TextFieldParser(
        $Path,
        (Get-KoxoEncoding -Name $Encoding)
    )
    $parser.SetDelimiters(';')
    $parser.HasFieldsEnclosedInQuotes = $true

    try {
        $isHeader = $true
        while (-not $parser.EndOfData) {
            $fields = $parser.ReadFields()
            if ($isHeader) {
                $isHeader = $false
                continue
            }

            [pscustomobject]@{
                Nom = $fields[1]
                Prenom = $fields[2]
                IdentifiantUnique = $fields[4]
            }
        }
    }
    finally {
        $parser.Close()
    }
}

function Get-DirectorySurname {
    param([string]$EmployeeNumber, [string]$Root)

    $searcher = New-Object DirectoryServices.DirectorySearcher
    if (-not [string]::IsNullOrWhiteSpace($Root)) {
        $searcher.SearchRoot = New-Object DirectoryServices.DirectoryEntry($Root)
    }

    $escaped = $EmployeeNumber -replace '([\\*()\0])', '\$1'
    $searcher.Filter = "(&(objectClass=user)(objectCategory=person)(employeeNumber=$escaped))"
    [void]$searcher.PropertiesToLoad.Add('sn')
    [void]$searcher.PropertiesToLoad.Add('displayname')
    [void]$searcher.PropertiesToLoad.Add('samaccountname')

    try {
        # FindAll et non FindOne : un employeeNumber en double casse la seule
        # cle de rattachement fiable, et le provisioning refuse deja de lier
        # dans ce cas. Le signaler plutot que d'en elire un au hasard.
        $all = @($searcher.FindAll())
        if ($all.Count -eq 0) {
            return $null
        }

        $properties = $all[0].Properties
        [pscustomobject]@{
            Surname = [string]$properties['sn'][0]
            DisplayName = [string]$properties['displayname'][0]
            SamAccountName = [string]$properties['samaccountname'][0]
            MatchCount = $all.Count
        }
    }
    finally {
        $searcher.Dispose()
    }
}

function Get-Classification {
    param([string]$Sent, $Directory)

    if ($null -eq $Directory) {
        return 'absent_annuaire'
    }

    if ($Directory.MatchCount -gt 1) {
        return 'plusieurs_identites'
    }

    $actual = $Directory.Surname
    if ($actual -ceq $Sent) {
        return 'identique'
    }

    $misread = ConvertTo-AnsiMisread -Value $Sent
    if ($actual -ceq $misread) {
        return 'corrompu_encodage'
    }

    # Les deux causes se cumulent : KoXo relit d'abord en ANSI (« É » -> « Ã‰ »),
    # puis sa table de translitteration retire l'accent du « Ã ». Reste
    # « A‰ » — un « A » nu suivi d'un caractere non alphabetique intact.
    if ($actual -ceq (Remove-Diacritics -Value $misread)) {
        return 'corrompu_puis_translittere'
    }

    if ($actual -ceq (Remove-Diacritics -Value $Sent)) {
        return 'translittere'
    }

    if ($actual -eq $Sent) {
        return 'casse_differente'
    }

    'autre'
}

$resolvedCsvPath = [IO.Path]::GetFullPath($CsvPath)
if (-not (Test-Path -LiteralPath $resolvedCsvPath -PathType Leaf)) {
    throw ("CSV introuvable : {0}." -f $resolvedCsvPath)
}

$rows = @(Get-CsvRow -Path $resolvedCsvPath -Encoding $EncodingName)
$report = foreach ($row in $rows) {
    $directory = $null
    if (-not $SkipDirectory) {
        $directory = Get-DirectorySurname -EmployeeNumber $row.IdentifiantUnique -Root $SearchRoot
    }

    $actualSurname = ''
    $samAccountName = ''
    if ($null -ne $directory) {
        $actualSurname = $directory.Surname
        $samAccountName = $directory.SamAccountName
    }

    $classification = 'non_verifie'
    if (-not $SkipDirectory) {
        $classification = Get-Classification -Sent $row.Nom -Directory $directory
    }

    [pscustomobject]@{
        IdentifiantUnique = $row.IdentifiantUnique
        NomEnvoye = $row.Nom
        NomAnnuaire = $actualSurname
        SamAccountName = $samAccountName
        # Octets reellement ecrits dans le CSV, utile quand l'affichage console
        # ment sur l'encodage.
        OctetsNomEnvoye = (
            ([Text.Encoding]::UTF8.GetBytes($row.Nom) | ForEach-Object { $_.ToString('x2') }) -join ' '
        )
        Constat = $classification
    }
}

$report

if ($SkipDirectory) {
    Write-Host ''
    Write-Host 'Annuaire non interroge (-SkipDirectory) : aucun constat rendu.'
    return
}

$accented = @($report | Where-Object { $_.NomEnvoye -cne (Remove-Diacritics -Value $_.NomEnvoye) })
$byConstat = $report | Group-Object -Property Constat | Sort-Object -Property Count -Descending

Write-Host ''
Write-Host ('Lignes analysees : {0} (dont {1} avec accent).' -f $report.Count, $accented.Count)
foreach ($group in $byConstat) {
    Write-Host ('  {0} : {1}' -f $group.Name, $group.Count)
}

$resolved = @(
    $report | Where-Object { $_.Constat -notin @('absent_annuaire', 'plusieurs_identites', 'non_verifie') }
)
$accentedResolved = @(
    $resolved | Where-Object { $_.NomEnvoye -cne (Remove-Diacritics -Value $_.NomEnvoye) }
)

Write-Host ''
if ($resolved.Count -eq 0) {
    # Ne jamais conclure « intact » sur zero mesure : sans identite retrouvee,
    # le test ne dit rien. Cause la plus frequente : requete LDAP emise depuis
    # une session WinRM (double saut), a relancer en local ou depuis RDC-07.
    Write-Host 'Verdict : aucune identite retrouvee par employeeNumber, le test ne prouve rien.'
    Write-Host '          Verifier -SearchRoot, et ne pas lancer ce script au travers d''une'
    Write-Host '          session WinRM : la requete LDAP y echoue silencieusement.'
}
elseif (@($report | Where-Object { $_.Constat -eq 'corrompu_puis_translittere' }).Count -gt 0) {
    Write-Host 'Verdict : les deux causes se cumulent. Corriger d''abord KOXO_CSV_ENCODING'
    Write-Host '          (valeur attendue utf8bom), rejouer une synchronisation, puis relancer'
    Write-Host '          ce test pour savoir s''il reste une translitteration KoXo.'
}
elseif (@($report | Where-Object { $_.Constat -eq 'corrompu_encodage' }).Count -gt 0) {
    Write-Host 'Verdict : corruption d''encodage. Verifier KOXO_CSV_ENCODING sur cette machine'
    Write-Host '          (valeur attendue utf8bom) puis rejouer une synchronisation.'
}
elseif (@($report | Where-Object { $_.Constat -eq 'translittere' }).Count -gt 0) {
    Write-Host 'Verdict : translitteration par KoXo. L''encodage n''est pas en cause ; la'
    Write-Host '          correction se joue dans la configuration KoXo ou apres synchronisation.'
}
elseif ($accentedResolved.Count -eq 0) {
    Write-Host 'Verdict : aucun nom accentue retrouve dans l''annuaire, le test ne prouve rien.'
}
else {
    Write-Host 'Verdict : les noms accentues arrivent intacts dans l''annuaire.'
}
