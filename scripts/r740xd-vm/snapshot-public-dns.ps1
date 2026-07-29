[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [string]$Resolver = "1.1.1.1"
)

$ErrorActionPreference = "Stop"
$zone = "zacharyhounsa.ovh"
$queries = @(
    @{ Name = $zone; Type = "A" },
    @{ Name = $zone; Type = "AAAA" },
    @{ Name = $zone; Type = "NS" },
    @{ Name = $zone; Type = "SOA" },
    @{ Name = $zone; Type = "MX" },
    @{ Name = $zone; Type = "TXT" },
    @{ Name = $zone; Type = "CAA" },
    @{ Name = "_dmarc.$zone"; Type = "TXT" },
    @{ Name = "default._domainkey.$zone"; Type = "TXT" },
    @{ Name = "ovh._domainkey.$zone"; Type = "TXT" }
)

$applicationNames = @(
    "www.$zone",
    "dashboard.$zone",
    "administration.$zone",
    "clients.$zone",
    "portfolio.$zone",
    "tests-mail.$zone"
)
foreach ($name in $applicationNames) {
    $queries += @{ Name = $name; Type = "A" }
    $queries += @{ Name = $name; Type = "AAAA" }
}

$results = foreach ($query in $queries) {
    try {
        $records = @(Resolve-DnsName `
            -Name $query.Name `
            -Type $query.Type `
            -Server $Resolver `
            -DnsOnly `
            -ErrorAction Stop)
        $matchingRecords = @($records | Where-Object {
            ([string]$_.Type) -in @($query.Type, "CNAME")
        })
        if ($matchingRecords.Count -eq 0) {
            [pscustomobject]@{
                QueryName = $query.Name
                QueryType = $query.Type
                Status = "NO_RECORD"
                Records = @()
            }
            continue
        }

        [pscustomobject]@{
            QueryName = $query.Name
            QueryType = $query.Type
            Status = "FOUND"
            Records = @($matchingRecords | ForEach-Object {
                [ordered]@{
                    Name = $_.Name
                    Type = [string]$_.Type
                    TTL = $_.TTL
                    IPAddress = $_.IPAddress
                    NameHost = $_.NameHost
                    NameExchange = $_.NameExchange
                    Preference = $_.Preference
                    Strings = @($_.Strings)
                    PrimaryServer = $_.PrimaryServer
                    NameAdministrator = $_.NameAdministrator
                    SerialNumber = $_.SerialNumber
                }
            })
        }
    }
    catch {
        $status = if (
            $_.FullyQualifiedErrorId -match "DNS_ERROR_RCODE_NAME_ERROR" -or
            $_.Exception.Message -match "n.existe pas|does not exist|NXDOMAIN"
        ) {
            "NXDOMAIN"
        }
        elseif (
            $_.FullyQualifiedErrorId -match "DNS_INFO_NO_RECORDS|DNS_ERROR_RECORD_DOES_NOT_EXIST"
        ) {
            "NO_RECORD"
        }
        elseif ($_.FullyQualifiedErrorId -match "CannotConvertArgumentNoMessage") {
            "UNSUPPORTED_CLIENT"
        }
        else {
            "QUERY_ERROR"
        }
        [pscustomobject]@{
            QueryName = $query.Name
            QueryType = $query.Type
            Status = $status
            Records = @()
        }
    }
}

$snapshot = [ordered]@{
    CapturedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    Resolver = $Resolver
    Zone = $zone
    Results = @($results)
}

$fullOutputPath = [IO.Path]::GetFullPath($OutputPath)
$parent = [IO.Path]::GetDirectoryName($fullOutputPath)
if (-not [IO.Directory]::Exists($parent)) {
    [IO.Directory]::CreateDirectory($parent) | Out-Null
}
$snapshot | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $fullOutputPath -Encoding UTF8

Write-Host "DNS snapshot written: $fullOutputPath"
Write-Host "Queries: $($queries.Count)"
Write-Host "Found: $(@($results | Where-Object Status -eq 'FOUND').Count)"
Write-Host "NXDOMAIN: $(@($results | Where-Object Status -eq 'NXDOMAIN').Count)"
Write-Host "No record: $(@($results | Where-Object Status -eq 'NO_RECORD').Count)"
Write-Host "Unsupported client type: $(@($results | Where-Object Status -eq 'UNSUPPORTED_CLIENT').Count)"
Write-Host "Errors: $(@($results | Where-Object Status -eq 'QUERY_ERROR').Count)"
