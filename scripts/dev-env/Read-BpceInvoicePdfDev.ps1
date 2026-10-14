[CmdletBinding(DefaultParameterSetName = 'List')]
param(
    [Parameter(ParameterSetName = 'Audit', Mandatory = $true)]
    [switch] $Audit,
    [Parameter(ParameterSetName = 'Export', Mandatory = $true)]
    [guid] $CommercialDocumentId,
    [Parameter(ParameterSetName = 'Export', Mandatory = $true)]
    [string] $OutputDirectory,
    [string] $DevSecretsFile = (Join-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) 'kermaria-client-platform.dev.env.ps1')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Lecture seule de la copie PDF deja archivee dans kermaria_dev. Le fichier
# d'environnement n'est jamais execute et aucun jeton BPCE n'est lu.
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

$sql = if ($PSCmdlet.ParameterSetName -eq 'Audit') {
    "SELECT invoice.fiscal_number, invoice.issue_date, invoice.status, invoice.total_amount_cents, COALESCE(customer.country,''), COALESCE(subscription_document.period_start,''), COALESCE(subscription_document.period_end,''), COALESCE(subscription_document.tax_amount_cents,''), COALESCE(event_row.period_start,''), COALESCE(event_row.period_end,''), COALESCE(event_row.settlement_status,''), document.subtotal_amount_cents, document.tax_amount_cents FROM bpce_invoices invoice INNER JOIN commercial_documents document ON document.id=invoice.commercial_document_id INNER JOIN customers customer ON customer.id=document.customer_id LEFT JOIN billing_v2_subscription_documents subscription_document ON subscription_document.commercial_document_id=document.id LEFT JOIN billing_v2_billing_events event_row ON event_row.id=subscription_document.billing_event_id WHERE invoice.issue_date >= '2026-10-01' ORDER BY invoice.issue_date, invoice.id;"
} elseif ($PSCmdlet.ParameterSetName -eq 'Export') {
    "SELECT id, bpce_invoice_id, COALESCE(fiscal_number,''), total_amount_cents, COALESCE(pdf_hash,''), COALESCE(HEX(pdf_content),'') FROM bpce_invoices WHERE commercial_document_id = '$CommercialDocumentId' LIMIT 1;"
} else {
    "SELECT id, bpce_invoice_id, COALESCE(fiscal_number,''), total_amount_cents, COALESCE(pdf_hash,''), COALESCE(LENGTH(pdf_content),0) FROM bpce_invoices ORDER BY issue_date DESC, id DESC LIMIT 25;"
}
$remote = 'read -r MYSQL_PWD
MYSQL_PWD=${MYSQL_PWD%$(printf "\r")}
export MYSQL_PWD
mariadb -h 192.168.100.206 -u "$1" --batch --raw -N --database=kermaria_dev'
$previousErrorAction = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    $output = (@($values.DEV_SQL_PASSWORD, $sql) -join "`n") |
        ssh -o BatchMode=yes kermaria-srv-06 "bash -c '$remote' _ $($values.DEV_SQL_USERNAME)" 2>&1
    $exitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorAction
}
if ($exitCode -ne 0) { throw "DEV_BPCE_READ_FAILED exit=$exitCode" }

$rows = @($output | Where-Object { "$_" -match '^[0-9a-fA-F-]{36}\t' })
if ($PSCmdlet.ParameterSetName -eq 'Audit') {
    foreach ($row in $output) {
        $parts = "$row" -split "`t"
        if ($parts.Count -ne 13 -or $parts[0] -notmatch '^FC-') { continue }
        Write-Output "fiscal_number=$($parts[0]) issue_date=$($parts[1]) status=$($parts[2]) total_cents=$($parts[3]) country=$($parts[4]) service_start=$($parts[5]) service_end=$($parts[6]) document_tax_cents=$($parts[7]) event_start=$($parts[8]) event_end=$($parts[9]) settlement=$($parts[10]) document_subtotal_cents=$($parts[11]) commercial_tax_cents=$($parts[12])"
    }
    return
}
if ($PSCmdlet.ParameterSetName -eq 'List') {
    foreach ($row in $rows) {
        $parts = "$row" -split "`t"
        if ($parts.Count -ne 6) { throw 'DEV_BPCE_LIST_INVALID' }
        Write-Output "document_id=$($parts[0]) bpce_id=$($parts[1]) fiscal_number=$($parts[2]) total_cents=$($parts[3]) pdf_hash=$($parts[4]) pdf_bytes=$($parts[5])"
    }
    return
}

if ($rows.Count -ne 1) { throw 'DEV_BPCE_DOCUMENT_NOT_FOUND' }
$fields = "$($rows[0])" -split "`t"
if ($fields.Count -ne 6 -or [string]::IsNullOrEmpty($fields[5])) {
    throw 'DEV_BPCE_PDF_NOT_CACHED'
}
if ($OutputDirectory -notmatch '^[A-Za-z]:\\') {
    throw 'DEV_BPCE_OUTPUT_MUST_BE_ABSOLUTE'
}
$resolvedRepo = [IO.Path]::GetFullPath((Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)).TrimEnd('\')
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
if ($resolvedOutput.StartsWith($resolvedRepo + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'DEV_BPCE_OUTPUT_INSIDE_REPOSITORY'
}
$bytes = [Convert]::FromHexString($fields[5])
if ($bytes.Length -lt 4 -or [Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne '%PDF') {
    throw 'DEV_BPCE_PDF_SIGNATURE_INVALID'
}
$hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
if ($hash -ne $fields[4].ToLowerInvariant()) { throw 'DEV_BPCE_PDF_HASH_MISMATCH' }
[IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
$path = Join-Path $resolvedOutput ("bpce-dev-$CommercialDocumentId.pdf")
$stream = [IO.File]::Open($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try { $stream.Write($bytes, 0, $bytes.Length) }
finally { $stream.Dispose() }
Write-Output "pdf=$path bytes=$($bytes.Length) sha256=$hash bpce_id=$($fields[1]) fiscal_number=$($fields[2]) total_cents=$($fields[3])"
