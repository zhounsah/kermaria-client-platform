[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$RootPath = "D:\Kermaria\KoXoExchange",
    [string]$CanonicalPath = "C:\ProgramData\Kermaria\koxo-exchange",
    [string]$ServiceAccount = "LocalSystem"
)

$ErrorActionPreference = "Stop"

$subDirectories = @(
    "incoming",
    "processing",
    "processed",
    "failed",
    "archive",
    "logs"
)

if ($PSCmdlet.ShouldProcess($RootPath, "Creer l'arborescence KoXo")) {
    if (-not (Test-Path -LiteralPath $RootPath)) {
        New-Item -ItemType Directory -Force -Path $RootPath | Out-Null
    }

    foreach ($name in $subDirectories) {
        $path = Join-Path $RootPath $name
        if (-not (Test-Path -LiteralPath $path)) {
            New-Item -ItemType Directory -Force -Path $path | Out-Null
        }
    }

    icacls $RootPath /inheritance:r /grant:r "*S-1-5-32-544:(OI)(CI)F" /grant:r "*S-1-5-18:(OI)(CI)F" | Out-Null
    if ($ServiceAccount -ne "LocalSystem") {
        icacls $RootPath /grant:r "${ServiceAccount}:(OI)(CI)M" | Out-Null
    }
}

if ($CanonicalPath -ne $RootPath) {
    $canonicalParent = Split-Path -Parent $CanonicalPath
    if ($PSCmdlet.ShouldProcess($CanonicalPath, "Creer le chemin canonique KoXo")) {
        if (-not (Test-Path -LiteralPath $canonicalParent)) {
            New-Item -ItemType Directory -Force -Path $canonicalParent | Out-Null
        }

        if (Test-Path -LiteralPath $CanonicalPath) {
            $existing = Get-Item -LiteralPath $CanonicalPath -Force
            $targets = @($existing.Target) | ForEach-Object {
                [IO.Path]::GetFullPath($_).TrimEnd("\")
            }
            $expected = [IO.Path]::GetFullPath($RootPath).TrimEnd("\")
            if ($existing.LinkType -ne "Junction" -or $expected -notin $targets) {
                throw "Le chemin canonique existe deja sans pointer vers $RootPath : $CanonicalPath"
            }
        } else {
            New-Item -ItemType Junction -Path $CanonicalPath -Target $RootPath | Out-Null
        }
    }
}

Write-Host "Arborescence KoXo preparee sous : $RootPath"
Write-Host "Chemin canonique : $CanonicalPath"
Write-Host "Sous-dossiers : $($subDirectories -join ', ')"
