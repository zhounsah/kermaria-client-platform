[CmdletBinding()]
param(
    [string]$TaskName = 'Kermaria-KoXoWebhookReceiver-8042',
    [string]$LauncherPath = (Join-Path $PSScriptRoot 'Start-KoxoSyncWebhookReceiver-8042.cmd'),
    [ValidateRange(1, 65535)]
    [int]$Port = 8042,
    [switch]$Execute,
    [switch]$RunNow
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedLauncherPath = [System.IO.Path]::GetFullPath($LauncherPath)
$workingDirectory = [System.IO.Path]::GetDirectoryName($resolvedLauncherPath)

$taskDefinition = [pscustomobject]@{
    TaskName = $TaskName
    LauncherPath = $resolvedLauncherPath
    Port = $Port
    WorkingDirectory = $workingDirectory

    PrincipalUserId = 'SYSTEM'
    RunLevel = 'Highest'
    Trigger = 'AtStartup'

    ExecutionTimeLimit = 'PT0S'
    RestartCount = 3
    RestartInterval = 'PT1M'
    MultipleInstances = 'IgnoreNew'
    StartWhenAvailable = $true

    Mode = if ($Execute) { 'execute' } else { 'simulate' }
    CommandLine = '"{0}" {1}' -f $resolvedLauncherPath, $Port
}

if (-not $Execute) {
    $taskDefinition
    return
}

if (-not (Test-Path -LiteralPath $resolvedLauncherPath -PathType Leaf)) {
    throw "KoXo webhook launcher not found: $resolvedLauncherPath"
}

$action = New-ScheduledTaskAction `
    -Execute $resolvedLauncherPath `
    -Argument ([string]$Port) `
    -WorkingDirectory $workingDirectory

$trigger = New-ScheduledTaskTrigger -AtStartup

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -MultipleInstances IgnoreNew `
    -StartWhenAvailable `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1)

$principal = New-ScheduledTaskPrincipal `
    -UserId 'SYSTEM' `
    -RunLevel Highest `
    -LogonType ServiceAccount

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Force |
Out-Null

if ($RunNow) {
    Start-ScheduledTask -TaskName $TaskName
}

$taskDefinition
