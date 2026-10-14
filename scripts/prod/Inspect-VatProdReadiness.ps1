[CmdletBinding()]
param(
    [string] $ProdSecretsFile = (Join-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) 'kermaria-client-platform.local.env.ps1')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Inspection SQL strictement en lecture seule. Ne jamais executer le fichier
# de secrets : seules les affectations litterales SQL_* sont lues dans l'AST.
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

$sql = @(
    "SELECT 'identity',DATABASE(),@@hostname,@@version;",
    "SELECT 'migration',migration_id,applied_at FROM schema_migrations WHERE migration_id IN ('101_signup_auto_approval_state','102_vat_transition') ORDER BY migration_id;",
    "SELECT 'legal',content_key,version_label,SHA2(body_markdown,256) FROM managed_content_entries WHERE content_key IN ('legal:cgv','legal:mentions-legales') ORDER BY content_key;",
    "SELECT 'invoice',COUNT(*),COALESCE(SUM(total_amount_cents),0),COALESCE(SUM(CASE WHEN pdf_hash IS NOT NULL THEN 1 ELSE 0 END),0) FROM bpce_invoices WHERE issue_date >= '2026-10-01';",
    "SELECT 'invoice_archive',COUNT(*),COALESCE(SUM(CASE WHEN pdf_hash IS NOT NULL AND (pdf_content IS NULL OR LENGTH(pdf_content)=0) THEN 1 ELSE 0 END),0) FROM bpce_invoices;",
    "SELECT 'invoice_piece',bpce_invoice_id,COALESCE(fiscal_number,''),status,COALESCE(pdf_hash,''),COALESCE(LENGTH(pdf_content),0) FROM bpce_invoices ORDER BY issue_date;",
    "SELECT 'invoice_missing_pdf',bpce_invoice_id,COALESCE(fiscal_number,''),status,pdf_hash FROM bpce_invoices WHERE pdf_hash IS NOT NULL AND (pdf_content IS NULL OR LENGTH(pdf_content)=0) ORDER BY issue_date;",
    "SELECT 'subscriptions',COUNT(*) FROM billing_v2_subscriptions WHERE status='active';"
    ,"SELECT 'stripe_live_test_link',subscription.id,subscription.status,subscription.customer_id,customer.external_reference,customer.country,subscription.created_at FROM billing_v2_subscriptions subscription JOIN customers customer ON customer.id=subscription.customer_id WHERE subscription.id='aa4bc2b5-b9bc-450b-aba2-b07b3cdfcc56';"
    ,"SELECT 'stripe_live_test_event',id,total_amount_cents,financial_status,settlement_status FROM billing_v2_billing_events WHERE id='b6847dba-c247-4e9e-91d2-a331b14bf1ab';"
) -join "`n"
$remote = 'read -r MYSQL_PWD
MYSQL_PWD=${MYSQL_PWD%$(printf "\r")}
export MYSQL_PWD
mariadb -h 192.168.100.206 -u "$1" --batch --raw -N --database=kermaria'
$previousErrorAction = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    $output = (@($values.SQL_PASSWORD, $sql) -join "`n") |
        ssh -o BatchMode=yes kermaria-srv-06 "bash -c '$remote' _ $($values.SQL_USERNAME)" 2>&1
    $exitCode = $LASTEXITCODE
}
finally { $ErrorActionPreference = $previousErrorAction }
if ($exitCode -ne 0) { throw "PROD_VAT_INSPECTION_FAILED exit=$exitCode" }
$identity = @($output | Where-Object { "$_" -like 'identity*' })
if ($identity.Count -ne 1 -or "$($identity[0])" -notmatch '^identity\tkermaria\tKERMARIA-SRV-06\t') {
    throw 'PROD_SQL_RUNTIME_IDENTITY_MISMATCH'
}
foreach ($line in $output) {
    if ("$line" -match '^(identity|migration|legal|invoice|invoice_archive|invoice_piece|invoice_missing_pdf|subscriptions|stripe_live_test_link|stripe_live_test_event)\t') {
        Write-Output "$line"
    }
}
