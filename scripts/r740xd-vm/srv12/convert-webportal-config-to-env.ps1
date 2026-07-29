[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $InputPath)) {
    throw "Configuration JSON introuvable : $InputPath"
}

$configuration = Get-Content -LiteralPath $InputPath -Raw | ConvertFrom-Json
$lines = foreach ($property in $configuration.PSObject.Properties | Sort-Object Name) {
    $name = $property.Name
    $value = [string]$property.Value

    if ($name -notmatch '^[A-Z_][A-Z0-9_]*$') {
        throw "Nom de variable incompatible avec systemd : $name"
    }
    if ($value -match "[`r`n`0]") {
        throw "Valeur multilignes ou NUL refusee pour : $name"
    }

    if ($value -notmatch "'") {
        "$name='$value'"
    } else {
        $escaped = $value.Replace("\", "\\").Replace('"', '\"')
        "$name=`"$escaped`""
    }
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

[IO.File]::WriteAllLines(
    $OutputPath,
    $lines,
    [Text.UTF8Encoding]::new($false))

Write-Host "EnvironmentFile ecrit : $OutputPath ($($lines.Count) variables)"
