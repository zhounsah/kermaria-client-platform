[CmdletBinding()]
param(
    [ValidateSet('auto', 'rest', 'powershell')]
    [string] $Mode = $env:VEEAM_COLLECTOR_MODE,
    [string] $PortalApiUrl = $env:VEEAM_PORTAL_API_URL,
    [string] $PortalServiceToken = $env:SERVICE_AUTH_TOKEN,
    [string] $RestBaseUrl = $env:VEEAM_REST_BASE_URL,
    [string] $RestUsername = $env:VEEAM_REST_USERNAME,
    [string] $RestPassword = $env:VEEAM_REST_PASSWORD,
    [string] $RestApiVersion = $(if ($env:VEEAM_REST_API_VERSION) { $env:VEEAM_REST_API_VERSION } else { '1.3-rev2' }),
    [switch] $WhatIfReport
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Mode) {
    $Mode = 'auto'
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $scriptRoot 'Veeam.PowerShell.psm1') -Force
Import-Module (Join-Path $scriptRoot 'Veeam.Rest.psm1') -Force

function Write-CollectorLog {
    param(
        [Parameter(Mandatory)] [string] $Message,
        [string] $Level = 'INFO'
    )

    $stamp = (Get-Date).ToUniversalTime().ToString('o')
    Write-Host ("{0} [{1}] {2}" -f $stamp, $Level, $Message)
}

function Test-RestConfigured {
    return (
        -not [string]::IsNullOrWhiteSpace($RestBaseUrl) -and
        -not [string]::IsNullOrWhiteSpace($RestUsername) -and
        -not [string]::IsNullOrWhiteSpace($RestPassword)
    )
}

function Get-CollectorSnapshot {
    if ($Mode -eq 'rest' -or ($Mode -eq 'auto' -and (Test-RestConfigured))) {
        Write-CollectorLog 'Collecte Veeam REST commencee.'
        return Get-KermariaVeeamRestSnapshot `
            -BaseUrl $RestBaseUrl `
            -Username $RestUsername `
            -Password $RestPassword `
            -ApiVersion $RestApiVersion
    }

    Write-CollectorLog 'Collecte Veeam PowerShell commencee.'
    return Get-KermariaVeeamPowerShellSnapshot
}

function Send-BackupReport {
    param([Parameter(Mandatory)] [object] $Report)

    if ($WhatIfReport) {
        $Report | ConvertTo-Json -Depth 8
        return
    }

    if ([string]::IsNullOrWhiteSpace($PortalApiUrl)) {
        throw 'VEEAM_PORTAL_API_URL is required.'
    }

    if ([string]::IsNullOrWhiteSpace($PortalServiceToken)) {
        throw 'SERVICE_AUTH_TOKEN is required.'
    }

    $uri = $PortalApiUrl.TrimEnd('/') + '/internal/backups/report'
    Invoke-RestMethod `
        -Method Post `
        -Uri $uri `
        -Headers @{ 'X-Service-Auth' = $PortalServiceToken } `
        -ContentType 'application/json' `
        -Body ($Report | ConvertTo-Json -Depth 8) `
        -ErrorAction Stop | Out-Null
}

Write-CollectorLog 'Collecte sauvegardes Veeam initialisee.'
$reports = @(Get-CollectorSnapshot | Where-Object {
    $_.provider -eq 'veeam' -and
    -not [string]::IsNullOrWhiteSpace($_.externalJobId) -and
    -not [string]::IsNullOrWhiteSpace($_.externalSessionId) -and
    -not [string]::IsNullOrWhiteSpace($_.startedAt)
})

$sent = 0
$failed = 0
foreach ($report in $reports) {
    try {
        Send-BackupReport -Report $report
        $sent += 1
    } catch {
        $failed += 1
        Write-CollectorLog `
            -Level 'ERROR' `
            -Message ("Rapport refuse pour un job Veeam mappe par identifiant stable. Erreur: {0}" -f $_.Exception.GetType().Name)
    }
}

Write-CollectorLog ("Collecte sauvegardes Veeam terminee. jobs={0} envoyes={1} echecs={2}" -f $reports.Count, $sent, $failed)
if ($failed -gt 0) {
    exit 1
}
