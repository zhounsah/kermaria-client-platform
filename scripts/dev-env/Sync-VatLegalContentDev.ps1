[CmdletBinding()]
param(
    [string] $DevSecretsFile = (Join-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) 'kermaria-client-platform.dev.env.ps1')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Ne pas executer le fichier de secrets : seules cinq affectations litterales
# DEV_SQL_* sont lues. La cible SQL est verifiee avant tout appel reseau.
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    $DevSecretsFile, [ref] $tokens, [ref] $parseErrors)
if ($parseErrors.Count -ne 0) { throw 'DEV_ENV_PARSE_ERROR' }
$values = @{}
$ast.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.AssignmentStatementAst] `
        -and $node.Left.Extent.Text -match '^\$env:DEV_SQL_(HOST|PORT|DATABASE|USERNAME|PASSWORD)$'
}, $true) | ForEach-Object {
    if ($_.Right.Expression -isnot [System.Management.Automation.Language.StringConstantExpressionAst]) {
        throw 'DEV_SQL_VALUE_NOT_LITERAL'
    }
    $values[$_.Left.Extent.Text.Substring(5)] = $_.Right.Expression.Value
}
if ($values.DEV_SQL_HOST -ne 'BASE-SQL-01.home.bzh' `
    -or $values.DEV_SQL_PORT -ne '3306' `
    -or $values.DEV_SQL_DATABASE -ne 'kermaria_dev' `
    -or $values.DEV_SQL_USERNAME -ne 'kermaria_dev' `
    -or [string]::IsNullOrWhiteSpace($values.DEV_SQL_PASSWORD)) {
    throw 'DEV_SQL_IDENTITY_MISMATCH'
}

function ConvertTo-SqlUtf8Hex([string] $value) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($value)
    return 'CONVERT(0x' + [BitConverter]::ToString($bytes).Replace('-', '') + ' USING utf8mb4)'
}

function Get-LegalSeed([string] $fileName) {
    $path = Join-Path (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'apps\api-internal\SeedContent') $fileName
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
    return @{ Version = $version; Body = ([string]::Join("`n", $body)).Trim() }
}

$items = @(
    @{ Key = 'legal:cgv'; File = 'cgv.md'; OldVersions = @('Version du : 03 août 2026', 'Version du : 13 octobre 2026') },
    @{ Key = 'legal:mentions-legales'; File = 'mentions-legales.md'; OldVersions = @('Dernière mise à jour : 07 juillet 2026', 'Dernière mise à jour : 13 octobre 2026') }
)
$sql = New-Object 'System.Collections.Generic.List[string]'
$sql.Add('START TRANSACTION;')
foreach ($item in $items) {
    $seed = Get-LegalSeed $item.File
    $key = ConvertTo-SqlUtf8Hex $item.Key
    $oldVersions = ($item.OldVersions | ForEach-Object { ConvertTo-SqlUtf8Hex $_ }) -join ','
    $newVersion = ConvertTo-SqlUtf8Hex $seed.Version
    $newBody = ConvertTo-SqlUtf8Hex $seed.Body
    $sql.Add("INSERT IGNORE INTO managed_content_legal_versions (id,content_key,version_label,body_markdown,content_sha256,recorded_at) VALUES (UUID(),$key,$newVersion,$newBody,SHA2($newBody,256),UTC_TIMESTAMP(6));")
    $sql.Add("UPDATE managed_content_entries SET body_markdown=$newBody,version_label=$newVersion,updated_at=UTC_TIMESTAMP(6) WHERE content_key=$key AND version_label IN ($oldVersions) AND SHA2(body_markdown,256) IN (SELECT content_sha256 FROM managed_content_legal_versions WHERE content_key=$key AND version_label IN ($oldVersions));")
    $sql.Add("SELECT 'updated',$key,ROW_COUNT();")
}
$sql.Add('COMMIT;')
foreach ($item in $items) {
    $key = ConvertTo-SqlUtf8Hex $item.Key
    $sql.Add("SELECT 'current',$key,version_label,SHA2(body_markdown,256) FROM managed_content_entries WHERE content_key=$key;")
}

$remote = 'read -r MYSQL_PWD
MYSQL_PWD=${MYSQL_PWD%$(printf "\r")}
export MYSQL_PWD
mariadb -h 192.168.100.206 -u "$1" -N --database=kermaria_dev'
$previousErrorAction = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    $output = (@($values.DEV_SQL_PASSWORD) + $sql.ToArray()) -join "`n" |
        ssh -o BatchMode=yes kermaria-srv-06 "bash -c '$remote' _ $($values.DEV_SQL_USERNAME)" 2>&1
    $exitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorAction
}
if ($exitCode -ne 0) { throw "DEV_LEGAL_SYNC_FAILED exit=$exitCode" }

$expected = @{}
foreach ($item in $items) {
    $seed = Get-LegalSeed $item.File
    $expected[$item.Key] = @{
        Version = $seed.Version
        Hash = ([BitConverter]::ToString(
            [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($seed.Body))
        ).Replace('-', '')).ToLowerInvariant()
    }
}
$verifiedCount = 0
foreach ($line in $output) {
    $parts = "$line" -split "`t"
    if ($parts.Count -eq 4 -and $parts[0] -eq 'current') {
        if (-not $expected.ContainsKey($parts[1]) `
            -or $parts[2] -ne $expected[$parts[1]].Version `
            -or $parts[3] -ne $expected[$parts[1]].Hash) {
            throw 'DEV_LEGAL_CONTENT_VERIFICATION_FAILED'
        }
        $verifiedCount++
        Write-Output "verified=$($parts[1]) version=$($parts[2]) sha256=$($parts[3])"
    }
}
if ($verifiedCount -ne $items.Count) { throw 'DEV_LEGAL_CONTENT_VERIFICATION_INCOMPLETE' }
