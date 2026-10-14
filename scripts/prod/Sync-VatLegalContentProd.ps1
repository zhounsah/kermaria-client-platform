[CmdletBinding()]
param(
    [switch] $Apply,
    [string] $ProdSecretsFile = (Join-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) 'kermaria-client-platform.local.env.ps1')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Par defaut : inspection uniquement. -Apply reste reserve a la fenetre de
# livraison approuvee. Aucune facture ou abonnement n'est modifie ici.
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    $ProdSecretsFile, [ref] $tokens, [ref] $parseErrors)
if ($parseErrors.Count -ne 0) { throw 'PROD_ENV_PARSE_ERROR' }
$values = @{}
$ast.FindAll({
    param($node)
    $node -is [Management.Automation.Language.AssignmentStatementAst] `
        -and $node.Left.Extent.Text -match '^\$env:SQL_(HOST|PORT|DATABASE|USERNAME|PASSWORD)$'
}, $true) | ForEach-Object {
    if ($_.Right.Expression -isnot [Management.Automation.Language.StringConstantExpressionAst]) {
        throw 'PROD_SQL_VALUE_NOT_LITERAL'
    }
    $values[$_.Left.Extent.Text.Substring(5)] = $_.Right.Expression.Value
}
if ($values.SQL_HOST -ne 'BASE-SQL-01.home.bzh' `
    -or $values.SQL_PORT -ne '3306' `
    -or $values.SQL_DATABASE -ne 'kermaria' `
    -or $values.SQL_USERNAME -ne 'kermaria_api' `
    -or [string]::IsNullOrWhiteSpace($values.SQL_PASSWORD)) {
    throw 'PROD_SQL_IDENTITY_MISMATCH'
}

function ConvertTo-SqlUtf8Hex([string] $text) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($text)
    return 'CONVERT(0x' + [BitConverter]::ToString($bytes).Replace('-', '') + ' USING utf8mb4)'
}

function Get-LegalSeed([string] $fileName) {
    $repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $path = Join-Path $repo "apps\api-internal\SeedContent\$fileName"
    $lines = ([IO.File]::ReadAllText($path, [Text.Encoding]::UTF8) -replace "`r`n", "`n") -split "`n"
    $version = $null
    $body = New-Object 'System.Collections.Generic.List[string]'
    foreach ($rawLine in $lines) {
        $line = $rawLine.Trim()
        if ($line.StartsWith('# ')) { continue }
        if ($null -eq $version -and ($line.StartsWith('**Version du :') `
                -or $line.StartsWith('**Dernière mise à jour :')) `
                -and $line.EndsWith('**')) {
            $version = $line.Trim('*').Trim()
            continue
        }
        $body.Add($rawLine)
    }
    if ([string]::IsNullOrWhiteSpace($version)) { throw "LEGAL_VERSION_MISSING:$fileName" }
    $content = ([string]::Join("`n", $body)).Trim()
    $hash = [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($content))
    ).ToLowerInvariant()
    return @{ Version = $version; Body = $content; Hash = $hash }
}

function Invoke-FixedProdSql([string] $sql) {
    $remote = 'read -r MYSQL_PWD
MYSQL_PWD=${MYSQL_PWD%$(printf "\r")}
export MYSQL_PWD
mariadb -h 192.168.100.206 -u "$1" --batch --raw -N --database=kermaria'
    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $result = (@($values.SQL_PASSWORD, $sql) -join "`n") |
            ssh -o BatchMode=yes kermaria-srv-06 "bash -c '$remote' _ $($values.SQL_USERNAME)" 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previousErrorAction }
    if ($exitCode -ne 0) { throw "PROD_LEGAL_SQL_FAILED exit=$exitCode" }
    return @($result | ForEach-Object { "$_" })
}

$entries = @(
    @{ Key='legal:cgv'; File='cgv.md'; OldVersion='Version du : 15 août 2026'; OldHash='d699a8db41bc4fa5a6fd5df16c6f541ac512107326ca5409fc5525361873b424' },
    @{ Key='legal:mentions-legales'; File='mentions-legales.md'; OldVersion='Dernière mise à jour : 07 juillet 2026'; OldHash='c2ab966ed1e824bfb1824c346cc90c34c8cda2deeadbd6ae7fa1d31607fee7b6' }
)
foreach ($entry in $entries) { $entry.Seed = Get-LegalSeed $entry.File }
$preflightSql = @(
    "SELECT 'identity',DATABASE(),@@hostname;",
    "SELECT 'migration',COUNT(*) FROM schema_migrations WHERE migration_id='102_vat_transition';",
    "SELECT 'current',content_key,version_label,SHA2(body_markdown,256) FROM managed_content_entries WHERE content_key IN ('legal:cgv','legal:mentions-legales') ORDER BY content_key;"
) -join "`n"
$before = Invoke-FixedProdSql $preflightSql
if (@($before | Where-Object { $_ -eq "identity`tkermaria`tKERMARIA-SRV-06" }).Count -ne 1) {
    throw 'PROD_SQL_RUNTIME_IDENTITY_MISMATCH'
}
$migration = @($before | Where-Object { $_ -like 'migration*' })
if ($migration.Count -ne 1) { throw 'PROD_MIGRATION_QUERY_INVALID' }
if ($Apply -and $migration[0] -ne "migration`t1") {
    throw 'PROD_VAT_MIGRATION_102_REQUIRED'
}
$current = @{}
foreach ($line in $before) {
    $parts = $line -split "`t"
    if ($parts.Count -eq 4 -and $parts[0] -eq 'current') {
        $current[$parts[1]] = @{ Version=$parts[2]; Hash=$parts[3] }
    }
}
if ($current.Count -ne 2) { throw 'PROD_LEGAL_CURRENT_INCOMPLETE' }
$allNew = $true
$allOld = $true
foreach ($entry in $entries) {
    $state = $current[$entry.Key]
    if ($null -eq $state) { throw 'PROD_LEGAL_KEY_MISSING' }
    if ($state.Version -ne $entry.Seed.Version -or $state.Hash -ne $entry.Seed.Hash) {
        $allNew = $false
    }
    if ($state.Version -ne $entry.OldVersion -or $state.Hash -ne $entry.OldHash) {
        $allOld = $false
    }
    Write-Output "current=$($entry.Key) version=$($state.Version) sha256=$($state.Hash) target_version=$($entry.Seed.Version) target_sha256=$($entry.Seed.Hash)"
}
Write-Output "migration_102=$($migration[0].Split("`t")[1]) mode=$(if($Apply){'apply'}else{'inspect'})"
if (-not $Apply) { return }
if ($allNew) { Write-Output 'PROD_LEGAL_ALREADY_CURRENT'; return }
if (-not $allOld) { throw 'PROD_LEGAL_UNEXPECTED_CURRENT_VERSION_OR_HASH' }

$keyCgv = ConvertTo-SqlUtf8Hex $entries[0].Key
$keyMentions = ConvertTo-SqlUtf8Hex $entries[1].Key
$validOld = @($entries | ForEach-Object {
    '(content_key=' + (ConvertTo-SqlUtf8Hex $_.Key) +
    ' AND version_label=' + (ConvertTo-SqlUtf8Hex $_.OldVersion) +
    " AND SHA2(body_markdown,256)='" + $_.OldHash + "')"
}) -join ' OR '
$newBodyCgv = ConvertTo-SqlUtf8Hex $entries[0].Seed.Body
$newBodyMentions = ConvertTo-SqlUtf8Hex $entries[1].Seed.Body
$newVersionCgv = ConvertTo-SqlUtf8Hex $entries[0].Seed.Version
$newVersionMentions = ConvertTo-SqlUtf8Hex $entries[1].Seed.Version
$mutations = @(
    'START TRANSACTION;',
    "INSERT IGNORE INTO managed_content_legal_versions (id,content_key,version_label,body_markdown,content_sha256,recorded_at) SELECT UUID(),content_key,version_label,body_markdown,SHA2(body_markdown,256),UTC_TIMESTAMP(6) FROM managed_content_entries WHERE $validOld;",
    "INSERT IGNORE INTO managed_content_legal_versions (id,content_key,version_label,body_markdown,content_sha256,recorded_at) VALUES (UUID(),$keyCgv,$newVersionCgv,$newBodyCgv,SHA2($newBodyCgv,256),UTC_TIMESTAMP(6)),(UUID(),$keyMentions,$newVersionMentions,$newBodyMentions,SHA2($newBodyMentions,256),UTC_TIMESTAMP(6));",
    "UPDATE managed_content_entries entry JOIN (SELECT COUNT(*) AS valid_count FROM managed_content_entries WHERE $validOld) guard ON guard.valid_count=2 SET entry.body_markdown=CASE WHEN entry.content_key=$keyCgv THEN $newBodyCgv ELSE $newBodyMentions END, entry.version_label=CASE WHEN entry.content_key=$keyCgv THEN $newVersionCgv ELSE $newVersionMentions END, entry.updated_at=UTC_TIMESTAMP(6) WHERE entry.content_key IN ($keyCgv,$keyMentions) AND ($validOld);",
    "SELECT 'updated',ROW_COUNT();",
    'COMMIT;',
    "SELECT 'current',content_key,version_label,SHA2(body_markdown,256) FROM managed_content_entries WHERE content_key IN ('legal:cgv','legal:mentions-legales') ORDER BY content_key;"
) -join "`n"
$after = Invoke-FixedProdSql $mutations
if (@($after | Where-Object { $_ -eq "updated`t2" }).Count -ne 1) {
    throw 'PROD_LEGAL_UPDATE_COUNT_INVALID'
}
foreach ($entry in $entries) {
    $expected = "current`t$($entry.Key)`t$($entry.Seed.Version)`t$($entry.Seed.Hash)"
    if (@($after | Where-Object { $_ -eq $expected }).Count -ne 1) {
        throw "PROD_LEGAL_POSTSTATE_INVALID:$($entry.Key)"
    }
    Write-Output "verified=$($entry.Key) version=$($entry.Seed.Version) sha256=$($entry.Seed.Hash)"
}
