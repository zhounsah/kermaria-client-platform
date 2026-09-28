<#
.SYNOPSIS
  Installe ou met a jour les binaires API-INTERNAL DEV, sans regenerer sa
  configuration a partir d'une source globale.

.DESCRIPTION
  Cette operation reste strictement additive vis-a-vis de KermariaApiInternal
  (PROD). Par defaut, elle valide puis preserve a l'octet pres la configuration
  DEV existante. Une mise a jour de secrets est exceptionnelle et explicite :
  -RefreshConfiguration -DevSecretsFile <fichier DEV_API_* parse sans execution>.

  Le fichier historique kermaria-client-platform.dev.env.ps1 n'est jamais une
  valeur par defaut et sera refuse s'il est passe : il contient des variables
  generiques et ne respecte pas le contrat DEV_API_*.

.EXAMPLE
  # Controle non modifiant de la configuration distante.
  .\scripts\dev-env\Install-ApiInternalDev.ps1 -ValidateOnly

.EXAMPLE
  # Mise a jour binaire : aucun JSON DEV n'est regenere ni ecrit.
  .\scripts\dev-env\Install-ApiInternalDev.ps1 -PublishDirectory $env:TEMP\api-internal-dev

.EXAMPLE
  # Rafraichissement explicite de secrets DEV, apres validation fail-closed.
  .\scripts\dev-env\Install-ApiInternalDev.ps1 -PublishDirectory $env:TEMP\api-internal-dev `
    -RefreshConfiguration -DevSecretsFile C:\secure\kermaria-client-platform.api-dev.secrets.ps1
#>
[CmdletBinding()]
param(
    [string] $PublishDirectory,
    [string] $ComputerName = 'KERMARIA-SRV-13.home.bzh',
    [string] $DevSecretsFile,
    [switch] $RefreshConfiguration,
    [switch] $ValidateOnly,
    [switch] $NoStart
)

$ErrorActionPreference = 'Stop'

if ($ValidateOnly -and $PublishDirectory) {
    throw '-ValidateOnly ne peut pas etre combine avec -PublishDirectory.'
}
if (-not $ValidateOnly) {
    if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
        throw '-PublishDirectory est obligatoire hors -ValidateOnly.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $PublishDirectory 'Kermaria.ApiInternal.exe') -PathType Leaf)) {
        throw "Kermaria.ApiInternal.exe absent de $PublishDirectory : publier avec -p:UseAppHost=true."
    }
}
if ($DevSecretsFile -and -not $RefreshConfiguration) {
    throw '-DevSecretsFile exige -RefreshConfiguration : aucune source ne doit etre lue par defaut.'
}

$configLibraryPath = Join-Path $PSScriptRoot 'DevApiConfig.ps1'
if (-not (Test-Path -LiteralPath $configLibraryPath -PathType Leaf)) {
    throw 'Bibliotheque DevApiConfig.ps1 introuvable.'
}
. $configLibraryPath

$secretOverrides = @{}
if ($RefreshConfiguration -and $DevSecretsFile) {
    $secretOverrides = Read-DevApiSecretOverrides -Path $DevSecretsFile
}
$configLibrarySource = Get-Content -LiteralPath $configLibraryPath -Raw

$session = New-PSSession -ComputerName $ComputerName
try {
    $configPath = 'C:\ProgramData\Kermaria-dev\api-internal.dev.config.json'
    $validation = Invoke-Command -Session $session -ArgumentList $configPath, $configLibrarySource, $secretOverrides -ScriptBlock {
        param($configPath, $configLibrarySource, $secretOverrides)
        . ([scriptblock]::Create($configLibrarySource))

        if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
            throw 'Configuration API DEV existante introuvable : aucun fallback de source n est autorise.'
        }
        try {
            $existing = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
        }
        catch {
            throw 'Configuration API DEV existante illisible : aucune modification n est autorisee.'
        }

        $plan = New-DevApiConfigurationPlan -ExistingConfiguration $existing -SecretOverrides $secretOverrides
        if (-not $plan.IsValid) {
            throw ('Configuration API DEV refusee avant toute modification : ' + ($plan.Violations -join ' | '))
        }

        [pscustomobject]@{
            ChangedKeyCount = @($plan.ChangedKeys).Count
            ConfigurationWillChange = @($plan.ChangedKeys).Count -gt 0
        }
    }

    Write-Host 'Configuration API DEV validee sans afficher de valeur sensible.'
    if ($validation.ConfigurationWillChange) {
        Write-Host "Rafraichissement explicite prepare : $($validation.ChangedKeyCount) secret(s)."
    } else {
        Write-Host 'Configuration API DEV preservee : aucune nouvelle valeur de secret recue.'
    }
    if ($ValidateOnly) {
        return
    }

    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $staging = "C:\apps\api-internal-dev-staging-$stamp"
    Invoke-Command -Session $session -ScriptBlock {
        param($staging)
        New-Item -ItemType Directory -Force -Path $staging | Out-Null
    } -ArgumentList $staging
    Copy-Item -Path (Join-Path $PublishDirectory '*') -Destination $staging -Recurse -ToSession $session

    Invoke-Command -Session $session -ArgumentList $staging, $stamp, $configLibrarySource, $secretOverrides, $RefreshConfiguration.IsPresent, $NoStart.IsPresent -ScriptBlock {
        param($staging, $stamp, $configLibrarySource, $secretOverrides, $refreshConfiguration, $noStart)
        $ErrorActionPreference = 'Stop'
        . ([scriptblock]::Create($configLibrarySource))

        $serviceName = 'KermariaApiInternalDev'
        $account = "NT SERVICE\$serviceName"
        $appDir = 'C:\apps\api-internal-dev'
        $configDir = 'C:\ProgramData\Kermaria-dev'
        $configPath = Join-Path $configDir 'api-internal.dev.config.json'
        $dataRoot = 'D:\Kermaria-dev'

        if ($serviceName -eq 'KermariaApiInternal' -or $appDir -eq 'C:\apps\api-internal') {
            throw 'Garde-fou : cible de production detectee.'
        }
        if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
            throw 'Configuration API DEV existante introuvable : aucune modification n est autorisee.'
        }
        try {
            $existing = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
        }
        catch {
            throw 'Configuration API DEV existante illisible : aucune modification n est autorisee.'
        }

        # Revalidation au plus pres de l'ecriture : un echec laisse service,
        # binaires et fichier de configuration intacts.
        $plan = New-DevApiConfigurationPlan -ExistingConfiguration $existing -SecretOverrides $secretOverrides
        if (-not $plan.IsValid) {
            throw ('Configuration API DEV refusee avant toute modification : ' + ($plan.Violations -join ' | '))
        }

        $existingService = Get-Service $serviceName -ErrorAction SilentlyContinue
        if ($existingService -and $existingService.Status -ne 'Stopped') {
            Stop-Service $serviceName -Force
            $existingService.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
        }

        if (Test-Path -LiteralPath $appDir) {
            Rename-Item -LiteralPath $appDir -NewName "api-internal-dev-old-$stamp"
        }
        Rename-Item -LiteralPath $staging -NewName (Split-Path $appDir -Leaf)

        foreach ($directory in $configDir, "$dataRoot\Logs\ApiInternal", "$dataRoot\Downloads") {
            New-Item -ItemType Directory -Force -Path $directory | Out-Null
        }

        # Le JSON est ecrit seulement avec un rafraichissement explicitement
        # demande. Sans cela, il n'est ni regenere, ni reecrit, ni sauvegarde.
        if ($refreshConfiguration -and @($plan.ChangedKeys).Count -gt 0) {
            Copy-Item -LiteralPath $configPath -Destination "$configPath.bak-$stamp"
            Write-DevApiConfiguration -Plan $plan -Path $configPath | Out-Null
        }

        if (-not $existingService) {
            New-Service -Name $serviceName `
                -DisplayName 'ZacharyIT API Internal DEV' `
                -Description 'API-INTERNAL environnement DEV isole (kermaria_dev, Stripe test, namespace KoXo DEV).' `
                -BinaryPathName "`"$appDir\Kermaria.ApiInternal.exe`" --environment Staging --urls http://192.168.100.213:5100" `
                -StartupType Manual | Out-Null
            & sc.exe config $serviceName obj= $account start= delayed-auto | Out-Null
            & sc.exe failure $serviceName reset= 86400 actions= restart/10000/restart/30000/restart/60000 | Out-Null
        }

        Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName" -Name Environment -Type MultiString -Value @(
            "KERMARIA_CONFIG_PATH=$configPath",
            'KERMARIA_CONFIG_AUTHORITATIVE=true',
            'ASPNETCORE_ENVIRONMENT=Staging',
            'DOTNET_ENVIRONMENT=Staging'
        )

        & icacls $configPath /inheritance:r /grant:r '*S-1-5-32-544:(F)' 'SYSTEM:(F)' "${account}:(R)" | Out-Null
        & icacls $appDir /grant "${account}:(OI)(CI)RX" | Out-Null
        & icacls $dataRoot /grant "${account}:(OI)(CI)M" | Out-Null

        $ruleName = 'Kermaria API DEV 5100 - bloquer hors SRV-12'
        if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
            New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort 5100 `
                -Action Block -RemoteAddress '0.0.0.0-192.168.100.211', '192.168.100.214-255.255.255.255' `
                -Profile Any | Out-Null
        }

        if (-not $noStart) {
            Start-Service $serviceName
        }
        Get-Service $serviceName | Select-Object Name, DisplayName, Status, StartType
    }
}
finally {
    if ($session) {
        Remove-PSSession $session
    }
}
