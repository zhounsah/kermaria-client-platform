[CmdletBinding()]
param(
    [string]$OutputDirectory = $(if ($env:KERMARIA_BACKUP_DIR) { $env:KERMARIA_BACKUP_DIR } else { Join-Path $env:USERPROFILE "Backups\Kermaria" }),
    [string]$DumpPrefix = "kermaria_mariadb",
    [string]$MySqlDumpPath = "mysqldump"
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
    # MariaDB 12.x emet systematiquement un avertissement TLS sur stderr : le
    # dump etait donc coupe des la premiere ligne et laissait un fichier de
    # 0 octet. Seul le code de sortie fait foi.
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

$repoRoot = Split-Path -Parent $PSScriptRoot
$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if ($resolvedOutputDirectory.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Warning "Stockez de preference les dumps hors du depot Git."
}

if (-not (Get-Command $MySqlDumpPath -ErrorAction SilentlyContinue)) {
    throw "Client mysqldump introuvable: $MySqlDumpPath."
}

$sqlHost = Get-RequiredValue -Name "SQL_HOST"
$sqlPort = Get-RequiredValue -Name "SQL_PORT"
$sqlDatabase = Get-RequiredValue -Name "SQL_DATABASE"
$sqlUsername = Get-RequiredValue -Name "SQL_USERNAME"
$sqlPassword = Get-RequiredValue -Name "SQL_PASSWORD" -Secret

New-Item -ItemType Directory -Force -Path $resolvedOutputDirectory | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$dumpPath = Join-Path $resolvedOutputDirectory ("{0}_{1}.sql" -f $DumpPrefix, $timestamp)
$arguments = @(
    "--host=$sqlHost",
    "--port=$sqlPort",
    "--user=$sqlUsername",
    "--single-transaction",
    "--routines",
    "--triggers",
    "--default-character-set=utf8mb4",
    "--result-file=$dumpPath",
    $sqlDatabase
)

$previousMysqlPwd = [Environment]::GetEnvironmentVariable("MYSQL_PWD")

try {
    $env:MYSQL_PWD = $sqlPassword
    Invoke-NativeCommand `
        -FilePath $MySqlDumpPath `
        -Arguments $arguments `
        -FailureMessage "mysqldump a echoue"
}
finally {
    if ([string]::IsNullOrWhiteSpace($previousMysqlPwd)) {
        Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
    }
    else {
        $env:MYSQL_PWD = $previousMysqlPwd
    }
}

$dumpFile = Get-Item -LiteralPath $dumpPath
if ($dumpFile.Length -le 0) {
    throw "Le dump genere est vide: $dumpPath."
}

# Un dump interrompu n'est pas vide pour autant : seul le marqueur de fin
# ecrit par mysqldump prouve que la sauvegarde est complete.
$lastLine = Get-Content -LiteralPath $dumpPath -Tail 1
if ($lastLine -notmatch "^-- Dump completed") {
    throw "Le dump genere est incomplet (marqueur de fin absent): $dumpPath."
}

$hash = Get-FileHash -LiteralPath $dumpPath -Algorithm SHA256

Write-Output "Backup MariaDB cree: $dumpPath"
Write-Output "Taille octets: $($dumpFile.Length)"
Write-Output "SHA256: $($hash.Hash)"
