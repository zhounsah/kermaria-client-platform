[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DumpPath,
    [string]$TargetDatabase = $env:SQL_DATABASE,
    [string]$MySqlPath = "mysql",
    [switch]$VerifySchema
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RequiredValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [switch]$Secret
    )

    $currentValue = [Environment]::GetEnvironmentVariable($Name)
    if (-not [string]::IsNullOrWhiteSpace($currentValue)) {
        return $currentValue.Trim()
    }

    if ($Secret) {
        $secureValue = Read-Host -AsSecureString -Prompt "$Name (saisie locale uniquement)"
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
        try {
            return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
        }
        finally {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
        }
    }

    throw "Variable requise absente: $Name."
}

if ([string]::IsNullOrWhiteSpace($TargetDatabase)) {
    throw "TargetDatabase est requis."
}

$resolvedDumpPath = (Resolve-Path -LiteralPath $DumpPath).Path
$normalizedDumpPath = $resolvedDumpPath.Replace("\", "/")
$sqlHost = Get-RequiredValue -Name "SQL_HOST"
$sqlPort = Get-RequiredValue -Name "SQL_PORT"
$sqlUsername = Get-RequiredValue -Name "SQL_USERNAME"
$sqlPassword = Get-RequiredValue -Name "SQL_PASSWORD" -Secret
$previousMysqlPwd = [Environment]::GetEnvironmentVariable("MYSQL_PWD")

$arguments = @(
    "--host=$sqlHost",
    "--port=$sqlPort",
    "--user=$sqlUsername",
    "--database=$TargetDatabase",
    "--execute=source `"$normalizedDumpPath`""
)

try {
    $env:MYSQL_PWD = $sqlPassword
    & $MySqlPath @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "mysql a retourne le code $LASTEXITCODE pendant la restauration."
    }

    if ($VerifySchema) {
        $verifyArguments = @(
            "--host=$sqlHost",
            "--port=$sqlPort",
            "--user=$sqlUsername",
            "--database=$TargetDatabase",
            "--batch",
            "--raw",
            "--skip-column-names",
            "--execute=SELECT COUNT(*) FROM schema_migrations;"
        )
        $migrationCount = & $MySqlPath @verifyArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Verification schema_migrations en echec."
        }

        Write-Output "Migrations detectees apres restauration: $migrationCount"
    }
}
finally {
    if ([string]::IsNullOrWhiteSpace($previousMysqlPwd)) {
        Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
    }
    else {
        $env:MYSQL_PWD = $previousMysqlPwd
    }
}

Write-Output "Restauration MariaDB terminee depuis: $resolvedDumpPath"
Write-Output "Base cible: $TargetDatabase"
