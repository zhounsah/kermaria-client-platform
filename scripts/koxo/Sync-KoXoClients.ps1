[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CsvTargetPath,

    [string]$WorkingDirectory = (Join-Path $PSScriptRoot 'work'),

    [switch]$DryRun,

    [switch]$LaunchKoxo,

    [string]$KoxoExecutablePath = 'C:\Program Files\KoXo Dev\KoXoAdm\KoXoAdm.exe',

    [string]$KoxoWorkingDirectory = 'C:\Program Files\KoXo Dev\KoXoAdm',

    [string]$KoxoSyncArgument = '/Synchro=CLIENTS.xml'
)

Set-StrictMode -Version Latest
$modulePath = Join-Path $PSScriptRoot 'KoxoSync.Common.psm1'
Import-Module $modulePath -Force

$result = Invoke-KoxoSync `
    -CsvTargetPath $CsvTargetPath `
    -WorkingDirectory $WorkingDirectory `
    -DryRun:$DryRun `
    -LaunchKoxo:$LaunchKoxo `
    -KoxoExecutablePath $KoxoExecutablePath `
    -KoxoWorkingDirectory $KoxoWorkingDirectory `
    -KoxoSyncArgument $KoxoSyncArgument
$result
