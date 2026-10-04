Set-StrictMode -Version Latest

function Get-KoxoDevQualityProof {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$DataRoot,
        [Parameter(Mandatory=$true)][string]$CsvPath,
        [Parameter(Mandatory=$true)][string]$SecondaryGroup,
        [Parameter(Mandatory=$true)][string]$UniqueId,
        [Parameter(Mandatory=$true)][AllowEmptyCollection()][string[]]$ExpectedGroups
    )
    # Chemins racine fournis exclusivement par la configuration du recepteur.
    # Ce premier protocole est borne a DEV, pas reutilisable en PROD par defaut.
    if ($SecondaryGroup -notmatch '^DEV-CLI-[A-Z0-9]+$' -or $UniqueId -notmatch '^CLI-D[0-9]{6}$') {
        throw 'KOXO_QUALITY_TARGET_OUTSIDE_DEV'
    }
    foreach ($group in $ExpectedGroups) {
        if ([string]::IsNullOrWhiteSpace($group) -or $group -cne $group.Trim() -or
            $group -notmatch '\A[\p{L}\p{N}_. -]{1,256}\z') { throw 'KOXO_QUALITY_GROUP_INVALID' }
    }
    $expected = @($ExpectedGroups | ForEach-Object {$_.ToUpperInvariant()} | Sort-Object -Unique)
    $result = [ordered]@{UniqueId=$UniqueId;UserId=$null;CsvVerified=$false;KoxoVerified=$false}
    $rows = @(Import-Csv -LiteralPath $CsvPath -Delimiter ';' -Encoding UTF8)
    $matches = @($rows | Where-Object {$_.IdentifiantUnique -ceq $UniqueId})
    if ($matches.Count -ne 1 -or $matches[0].GroupeSecondaire -cne $SecondaryGroup) { return [pscustomobject]$result }
    $row = $matches[0]
    # Index 14 est volontaire : le libelle accentue est controle par le schema
    # CSV et l'ordre constant ; aucun champ mot de passe n'est retourne.
    $columns = @($row.PSObject.Properties)
    if ($columns.Count -ne 15 -or $columns[14].Name -cne ('Qualit'+[char]233+'sSuppl'+[char]233+'mentaires')) {
        return [pscustomobject]$result
    }
    $csvGroups = @(([string]$columns[14].Value -split ',') | Where-Object {$_ -ne ''} |
        ForEach-Object {$_.ToUpperInvariant()} | Sort-Object -Unique)
    $result.CsvVerified = (($csvGroups -join ',') -ceq ($expected -join ','))
    $folder = Join-Path (Join-Path (Join-Path $DataRoot 'Users') 'CLIENTS DEV') $SecondaryGroup
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) { return [pscustomobject]$result }
    $users = @()
    foreach ($file in Get-ChildItem -LiteralPath $folder -Filter '*.xml' -File) {
        $settings = New-Object System.Xml.XmlReaderSettings
        $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver = $null
        $reader = [System.Xml.XmlReader]::Create($file.FullName, $settings)
        try {
            $xml = New-Object System.Xml.XmlDocument
            $xml.XmlResolver = $null
            $xml.Load($reader)
            if ($xml.SelectSingleNode('/User/UniqueID').InnerText -ceq $UniqueId) { $users += ,$xml }
        } finally { $reader.Dispose() }
    }
    if ($users.Count -ne 1) { return [pscustomobject]$result }
    $result.UserId = $users[0].SelectSingleNode('/User/UserId').InnerText
    $qualityNodes = @($users[0].SelectNodes('//AdditionalQuality'))
    foreach ($quality in $qualityNodes) {
        $name = $quality.SelectSingleNode('SAMAccountName')
        if ($null -eq $name -or [string]::IsNullOrWhiteSpace($name.InnerText)) { return [pscustomobject]$result }
    }
    $groups = @($qualityNodes | ForEach-Object {$_.SelectSingleNode('SAMAccountName').InnerText.ToUpperInvariant()} | Sort-Object -Unique)
    $result.KoxoVerified = (($groups -join ',') -ceq ($expected -join ','))
    [pscustomobject]$result
}

Export-ModuleMember -Function Get-KoxoDevQualityProof
