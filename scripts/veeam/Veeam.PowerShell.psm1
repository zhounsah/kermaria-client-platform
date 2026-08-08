Set-StrictMode -Version Latest

function Get-KermariaVeeamPowerShellSnapshot {
    [CmdletBinding()]
    param()

    if (-not (Get-Command Get-VBRJob -ErrorAction SilentlyContinue)) {
        Import-Module Veeam.Backup.PowerShell -ErrorAction Stop
    }

    $jobs = @(Get-VBRJob)
    $sessions = @(Get-VBRBackupSession)
    $items = New-Object System.Collections.Generic.List[object]

    foreach ($job in $jobs) {
        $jobId = Get-KermariaVeeamProperty -InputObject $job -Names @('Id')
        if (-not $jobId) {
            continue
        }

        $jobSessions = @(
            $sessions |
                Where-Object {
                    $sessionJobId = Get-KermariaVeeamProperty -InputObject $_ -Names @('JobId', 'jobId')
                    ($sessionJobId -as [string]) -eq ($jobId -as [string])
                } |
                Sort-Object {
                    Get-KermariaVeeamDate -InputObject $_ -Names @('EndTimeUTC', 'EndTime', 'CreationTimeUTC', 'CreationTime')
                } -Descending
        )
        $latest = $jobSessions | Select-Object -First 1
        if (-not $latest) {
            continue
        }

        $sessionId = Get-KermariaVeeamProperty -InputObject $latest -Names @('Id')
        $startedAt = Get-KermariaVeeamDate -InputObject $latest -Names @('CreationTimeUTC', 'CreationTime', 'StartTimeUTC', 'StartTime')
        $finishedAt = Get-KermariaVeeamDate -InputObject $latest -Names @('EndTimeUTC', 'EndTime', 'StopTimeUTC', 'StopTime')
        $result = Get-KermariaVeeamProperty -InputObject $latest -Names @('Result', 'Info')
        $durationSeconds = $null
        if ($startedAt -and $finishedAt) {
            $durationSeconds = [int][Math]::Max(1, ($finishedAt - $startedAt).TotalSeconds)
        }
        $externalSessionId = if ($sessionId) { [string]$sessionId } else { '{0}:{1:o}' -f $jobId, $startedAt }
        $startedAtIso = if ($startedAt) { $startedAt.ToString('o') } else { $null }
        $finishedAtIso = if ($finishedAt) { $finishedAt.ToString('o') } else { $null }

        $items.Add([pscustomobject]@{
            provider = 'veeam'
            externalJobId = [string]$jobId
            externalSessionId = $externalSessionId
            startedAt = $startedAtIso
            finishedAt = $finishedAtIso
            result = Convert-KermariaVeeamResult -Value $result
            protectedBytes = Get-KermariaVeeamLong -InputObject $latest -Names @('BackupStats.DataSize', 'Progress.TransferedSize', 'Progress.ProcessedSize')
            durationSeconds = $durationSeconds
            retentionDays = Get-KermariaVeeamRetentionDays -Job $job
            nextRunAt = Get-KermariaVeeamNextRun -Job $job
            publicMessage = Convert-KermariaVeeamPublicMessage -Value $result
        })
    }

    return $items
}

function Get-KermariaVeeamProperty {
    param(
        [Parameter(Mandatory)] [object] $InputObject,
        [Parameter(Mandatory)] [string[]] $Names
    )

    foreach ($name in $Names) {
        $current = $InputObject
        foreach ($part in $name.Split('.')) {
            if ($null -eq $current) {
                break
            }

            $property = $current.PSObject.Properties[$part]
            if ($null -eq $property) {
                $current = $null
                break
            }

            $current = $property.Value
        }

        if ($null -ne $current -and "$current".Trim().Length -gt 0) {
            return $current
        }
    }

    return $null
}

function Get-KermariaVeeamDate {
    param(
        [Parameter(Mandatory)] [object] $InputObject,
        [Parameter(Mandatory)] [string[]] $Names
    )

    $value = Get-KermariaVeeamProperty -InputObject $InputObject -Names $Names
    if ($value -is [datetime]) {
        return $value.ToUniversalTime()
    }

    if ($value) {
        try {
            return ([datetime]$value).ToUniversalTime()
        } catch {
            return $null
        }
    }

    return $null
}

function Get-KermariaVeeamLong {
    param(
        [Parameter(Mandatory)] [object] $InputObject,
        [Parameter(Mandatory)] [string[]] $Names
    )

    $value = Get-KermariaVeeamProperty -InputObject $InputObject -Names $Names
    if ($null -eq $value) {
        return $null
    }

    try {
        return [long]$value
    } catch {
        return $null
    }
}

function Get-KermariaVeeamRetentionDays {
    param([Parameter(Mandatory)] [object] $Job)

    $value = Get-KermariaVeeamProperty -InputObject $Job -Names @(
        'Options.BackupStorageOptions.RetainCycles',
        'BackupStorageOptions.RetainCycles'
    )
    if ($null -eq $value) {
        return $null
    }

    try {
        return [int]$value
    } catch {
        return $null
    }
}

function Get-KermariaVeeamNextRun {
    param([Parameter(Mandatory)] [object] $Job)

    $value = Get-KermariaVeeamProperty -InputObject $Job -Names @(
        'ScheduleOptions.NextRun',
        'NextRun'
    )
    if ($value -is [datetime]) {
        return $value.ToUniversalTime().ToString('o')
    }

    return $null
}

function Convert-KermariaVeeamResult {
    param([object] $Value)

    switch -Regex ("$Value") {
        '^Success$|^Succeeded$' { return 'success' }
        '^Warning$' { return 'warning' }
        '^Failed$|^Error$' { return 'failed' }
        '^Running$|^Working$' { return 'running' }
        default { return 'unknown' }
    }
}

function Convert-KermariaVeeamPublicMessage {
    param([object] $Value)

    switch (Convert-KermariaVeeamResult -Value $Value) {
        'warning' { 'Sauvegarde terminee avec avertissement.' }
        'failed' { 'Sauvegarde en echec. Contactez le support si besoin.' }
        default { $null }
    }
}

Export-ModuleMember -Function Get-KermariaVeeamPowerShellSnapshot
