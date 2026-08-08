Set-StrictMode -Version Latest

function Get-KermariaVeeamRestSnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $BaseUrl,
        [Parameter(Mandatory)] [string] $Username,
        [Parameter(Mandatory)] [string] $Password,
        [string] $ApiVersion = '1.3-rev2'
    )

    $base = $BaseUrl.TrimEnd('/')
    $tokenResponse = Invoke-RestMethod `
        -Method Post `
        -Uri "$base/api/oauth2/token" `
        -ContentType 'application/x-www-form-urlencoded' `
        -Body @{
            grant_type = 'password'
            username = $Username
            password = $Password
        } `
        -ErrorAction Stop
    $token = $tokenResponse.access_token
    if (-not $token) {
        throw 'Veeam REST authentication did not return an access token.'
    }

    $headers = @{
        Authorization = "Bearer $token"
        'x-api-version' = $ApiVersion
    }
    $sessionsResponse = Invoke-RestMethod `
        -Method Get `
        -Uri "$base/api/v1/sessions" `
        -Headers $headers `
        -ErrorAction Stop
    $sessions = @($sessionsResponse.data)
    if ($sessions.Count -eq 0 -and $sessionsResponse -is [array]) {
        $sessions = @($sessionsResponse)
    }

    $items = New-Object System.Collections.Generic.List[object]
    foreach ($session in $sessions) {
        $jobId = Get-KermariaRestValue -InputObject $session -Names @(
            'jobId',
            'job.id',
            'links.job.id'
        )
        if (-not $jobId) {
            continue
        }

        $sessionId = Get-KermariaRestValue -InputObject $session -Names @('id')
        $startedAt = Get-KermariaRestDate -InputObject $session -Names @('creationTime', 'startTime', 'createdAt')
        $finishedAt = Get-KermariaRestDate -InputObject $session -Names @('endTime', 'stopTime', 'finishedAt')
        $result = Get-KermariaRestValue -InputObject $session -Names @('result.result', 'result', 'state')

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
            result = Convert-KermariaRestResult -Value $result
            protectedBytes = Get-KermariaRestLong -InputObject $session -Names @('progress.processedSize', 'statistics.processedSize', 'processedSize')
            durationSeconds = $durationSeconds
            retentionDays = $null
            nextRunAt = $null
            publicMessage = Convert-KermariaRestPublicMessage -Value $result
        })
    }

    return $items
}

function Get-KermariaRestValue {
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

function Get-KermariaRestDate {
    param(
        [Parameter(Mandatory)] [object] $InputObject,
        [Parameter(Mandatory)] [string[]] $Names
    )

    $value = Get-KermariaRestValue -InputObject $InputObject -Names $Names
    if (-not $value) {
        return $null
    }

    try {
        return ([datetime]$value).ToUniversalTime()
    } catch {
        return $null
    }
}

function Get-KermariaRestLong {
    param(
        [Parameter(Mandatory)] [object] $InputObject,
        [Parameter(Mandatory)] [string[]] $Names
    )

    $value = Get-KermariaRestValue -InputObject $InputObject -Names $Names
    if (-not $value) {
        return $null
    }

    try {
        return [long]$value
    } catch {
        return $null
    }
}

function Convert-KermariaRestResult {
    param([object] $Value)

    switch -Regex ("$Value") {
        '^Success$|^Succeeded$' { return 'success' }
        '^Warning$' { return 'warning' }
        '^Failed$|^Error$' { return 'failed' }
        '^Running$|^Working$' { return 'running' }
        default { return 'unknown' }
    }
}

function Convert-KermariaRestPublicMessage {
    param([object] $Value)

    switch (Convert-KermariaRestResult -Value $Value) {
        'warning' { 'Sauvegarde terminee avec avertissement.' }
        'failed' { 'Sauvegarde en echec. Contactez le support si besoin.' }
        default { $null }
    }
}

Export-ModuleMember -Function Get-KermariaVeeamRestSnapshot
