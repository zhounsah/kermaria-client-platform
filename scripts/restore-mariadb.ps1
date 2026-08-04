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

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    # Sous Windows PowerShell 5.1, chaque ligne ecrite sur stderr par un
    # executable natif devient un ErrorRecord ; avec $ErrorActionPreference a
    # "Stop" elle interrompt le processus en pleine execution. Le client
    # MariaDB 12.x emet systematiquement un avertissement TLS sur stderr : une
    # restauration aurait ete coupee en cours de route, laissant la base dans
    # un etat partiel. Seul le code de sortie fait foi.
    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & $FilePath @Arguments
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
    }

    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (code $LASTEXITCODE)."
    }
}

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

if (-not (Get-Command $MySqlPath -ErrorAction SilentlyContinue)) {
    throw "Client mysql introuvable: $MySqlPath."
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
    Invoke-NativeCommand `
        -FilePath $MySqlPath `
        -Arguments $arguments `
        -FailureMessage "mysql a echoue pendant la restauration"

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
        $migrationCount = Invoke-NativeCommand `
            -FilePath $MySqlPath `
            -Arguments $verifyArguments `
            -FailureMessage "Verification schema_migrations en echec"

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
