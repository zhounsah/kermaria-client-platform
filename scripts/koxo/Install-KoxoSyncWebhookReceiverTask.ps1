[CmdletBinding()]
param(
    [string]$TaskName = 'Kermaria-KoXoWebhookReceiver',
    [string]$ReceiverScriptPath = (Join-Path $PSScriptRoot 'Start-KoxoSyncWebhookReceiver.ps1'),
    [string]$Prefix = 'http://+:8041/internal/koxo/sync/',
    [switch]$Execute,
    [switch]$RunNow
)

Set-StrictMode -Version Latest
$resolvedReceiverScriptPath = [System.IO.Path]::GetFullPath($ReceiverScriptPath)

$taskDefinition = [pscustomobject]@{
    TaskName = $TaskName
    ReceiverScriptPath = $resolvedReceiverScriptPath
    Prefix = $Prefix
    Mode = if ($Execute) { 'execute' } else { 'simulate' }
    CommandLine = 'powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "Start-Process powershell.exe -WindowStyle Hidden -ArgumentList ''-NoProfile'',''-NonInteractive'',''-ExecutionPolicy'',''Bypass'',''-Command'',''& ''''''{0}'''''' -Prefix ''''''{1}''''''''" -f (
        $resolvedReceiverScriptPath,
        $Prefix
    )
}

if (-not $Execute) {
    $taskDefinition
    return
}

$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument (
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "Start-Process powershell.exe -WindowStyle Hidden -ArgumentList ''-NoProfile'',''-NonInteractive'',''-ExecutionPolicy'',''Bypass'',''-Command'',''& ''''''{0}'''''' -Prefix ''''''{1}''''''''"' -f $resolvedReceiverScriptPath, $Prefix
)
$trigger = New-ScheduledTaskTrigger -AtStartup
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -MultipleInstances IgnoreNew -StartWhenAvailable
$principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -RunLevel Highest -LogonType ServiceAccount
Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null

if ($RunNow) {
    Start-ScheduledTask -TaskName $TaskName
}

$taskDefinition
