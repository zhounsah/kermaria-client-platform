<#
.SYNOPSIS
  Termine la migration 066 sur `kermaria_dev` (triggers + vue), DEV uniquement.

.DESCRIPTION
  SRV-06 a le binlog actif (log_bin=1) et log_bin_trust_function_creators=0 :
  MariaDB exige alors SUPER pour CREATE TRIGGER. Le compte migrateur DEV,
  volontairement limite a `kermaria_dev`.*, ne l'a pas ; la production avait
  ete migree par `kermaria_api` (definisseur des triggers PROD).

  Plutot que d'elargir les droits DEV ou de changer un parametre global du
  serveur de production, ce script cree les deux triggers et la vue de 066
  dans `kermaria_dev` via le compte d'administration disponible, en fixant
  DEFINER sur `kermaria_dev_migrator` : aucun objet DEV ne s'execute sous une
  identite capable d'atteindre `kermaria`. Il enregistre ensuite 066 dans
  `kermaria_dev.schema_migrations`.

  Le texte SQL est relu depuis le fichier de migration versionne ; seules la
  base cible et la clause DEFINER sont ajoutees. Aucune instruction ne vise
  `kermaria`.
#>
[CmdletBinding()]
param(
    [string] $AdminSecretsFile = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent | Split-Path -Parent) 'kermaria-client-platform.local.env.ps1'),
    [string] $SqlSshHost = 'kermaria-srv-06'
)

$ErrorActionPreference = 'Stop'
$migrationPath = Join-Path $PSScriptRoot '..\..\apps\api-internal\Migrations\MariaDb\066_billing_v2_componentized_pricing.sql'
$definer = "DEFINER = 'kermaria_dev_migrator'@'192.168.100.213'"

$blocks = (Get-Content $migrationPath -Raw) -split '(?m)^\s*--\s*statement-break\s*$' |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -match '(?m)^\s*CREATE (TRIGGER|OR REPLACE VIEW)' }
if ($blocks.Count -ne 3) {
    throw "066 : 3 blocs attendus (2 triggers + 1 vue), $($blocks.Count) trouves."
}

$statements = foreach ($block in $blocks) {
    $block -replace '(?m)^\s*CREATE TRIGGER ', "CREATE $definer TRIGGER " `
           -replace '(?m)^\s*CREATE OR REPLACE VIEW ', "CREATE OR REPLACE $definer VIEW "
}
$sql = @(
    'USE `kermaria_dev`;'
    # Chaque bloc de 066 est une instruction unique terminee par ';'.
    $statements
    "INSERT IGNORE INTO schema_migrations (migration_id, applied_at) VALUES ('066_billing_v2_componentized_pricing', UTC_TIMESTAMP(6));"
    "SELECT trigger_name, definer FROM information_schema.triggers WHERE trigger_schema = 'kermaria_dev';"
    "SELECT table_name, definer FROM information_schema.views WHERE table_schema = 'kermaria_dev';"
) -join "`n"

$ErrorActionPreference = 'Continue'
. $AdminSecretsFile *> $null
$remote = 'read -r MYSQL_PWD; MYSQL_PWD=${MYSQL_PWD%$(printf "\r")}; export MYSQL_PWD; mariadb -h 192.168.100.206 -u "$1" -N --database=kermaria_dev 2>&1'
(@($env:SQL_PASSWORD) + ($sql -split "`r?`n")) -join "`n" |
    ssh -o BatchMode=yes $SqlSshHost "bash -c '$remote' _ $($env:SQL_USERNAME)"
