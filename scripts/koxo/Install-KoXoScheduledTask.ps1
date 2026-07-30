[CmdletBinding()]
param(
    [string]$TaskName = 'Kermaria-KoXoSync',
    [string]$ScriptPath = (Join-Path $PSScriptRoot 'Sync-KoXoClients.ps1'),
    [string]$CsvTargetPath = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients.csv',
    [string]$WorkingDirectory = (Join-Path $PSScriptRoot 'work'),
    [string]$KoxoExecutablePath = 'C:\Program Files\KoXo Dev\KoXoAdm\KoXoAdm.exe',
    [string]$KoxoWorkingDirectory = 'C:\Program Files\KoXo Dev\KoXoAdm',
    [string]$KoxoSyncArgument = '/Synchro=CLIENTS.xml',
    [string]$Interval = 'PT15M',
    [switch]$Execute,
    [switch]$DocumentOnly
)

Set-StrictMode -Version Latest

$taskDefinition = [pscustomobject]@{
    TaskName = $TaskName
    ScriptPath = [System.IO.Path]::GetFullPath($ScriptPath)
    CsvTargetPath = [System.IO.Path]::GetFullPath($CsvTargetPath)
    WorkingDirectory = [System.IO.Path]::GetFullPath($WorkingDirectory)
    KoxoExecutablePath = [System.IO.Path]::GetFullPath($KoxoExecutablePath)
    KoxoWorkingDirectory = [System.IO.Path]::GetFullPath($KoxoWorkingDirectory)
    KoxoSyncArgument = $KoxoSyncArgument
    Interval = $Interval
    Mode = if ($Execute) { 'execute' } elseif ($DocumentOnly) { 'document_only' } else { 'simulate' }
    CommandLine = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "{0}" -CsvTargetPath "{1}" -WorkingDirectory "{2}" -LaunchKoxo -KoxoExecutablePath "{3}" -KoxoWorkingDirectory "{4}" -KoxoSyncArgument "{5}"' -f (
        [System.IO.Path]::GetFullPath($ScriptPath),
        [System.IO.Path]::GetFullPath($CsvTargetPath),
        [System.IO.Path]::GetFullPath($WorkingDirectory),
        [System.IO.Path]::GetFullPath($KoxoExecutablePath),
        [System.IO.Path]::GetFullPath($KoxoWorkingDirectory),
        $KoxoSyncArgument
    )
}

if (-not $Execute) {
    $taskDefinition
    return
}

$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument ('-NoProfile -ExecutionPolicy Bypass -File "{0}" -CsvTargetPath "{1}" -WorkingDirectory "{2}" -LaunchKoxo -KoxoExecutablePath "{3}" -KoxoWorkingDirectory "{4}" -KoxoSyncArgument "{5}"' -f $taskDefinition.ScriptPath, $taskDefinition.CsvTargetPath, $taskDefinition.WorkingDirectory, $taskDefinition.KoxoExecutablePath, $taskDefinition.KoxoWorkingDirectory, $taskDefinition.KoxoSyncArgument)
$trigger = New-ScheduledTaskTrigger -Once -At ((Get-Date).Date.AddMinutes(5))
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -Compatibility Win8 -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Force
$taskDefinition
