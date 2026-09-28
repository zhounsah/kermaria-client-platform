$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $scriptRoot 'DevApiConfig.ps1')

function New-ValidDevApiConfiguration {
    return [ordered]@{
        APP_ENV = 'Development'
        SQL_PROVIDER = 'mariadb'
        SQL_DATABASE = 'kermaria_dev'
        STRIPE_MODE = 'test'
        KOXO_IDENTIFIER_PREFIX = 'CLI-D'
        CUSTOMER_REFERENCE_PREFIX = 'DEV-CLI-'
        KOXO_PRIMARY_GROUP_CLIENTS = 'CLIENTS DEV'
        KOXO_PRIMARY_GROUP_DEMO = 'CLIENTS DEV DEMO'
        AD_INTEGRATION_MODE = 'controlled_write'
        AD_CLIENTS_OU_DN = 'OU=CLIENTS DEV,DC=clients,DC=home,DC=bzh'
        AD_SERVICE_ACCOUNT_USERNAME = 'HOME\svc-kermaria-ad-dev'
        AD_SERVICE_ACCOUNT_PASSWORD = 'x'
        KOXO_SYNC_WEBHOOK_URL = 'http://192.168.100.221:8043/internal/koxo/sync/'
        KOXO_SYNC_WEBHOOK_TOKEN = 'x'
        KOXO_PENDING_PASSWORD_KEY = 'x'
        SERVICE_AUTH_TOKEN = 'x'
        EMAIL_INTEGRATION_MODE = 'live'
        EMAIL_LIVE_ALLOWLIST_ONLY = 'true'
        EMAIL_LIVE_ALLOWLIST = 'e2e@example.invalid'
    }
}

Describe 'Configuration API DEV fail-closed' {
    It 'accepts the explicit DEV configuration' {
        $plan = New-DevApiConfigurationPlan -ExistingConfiguration (New-ValidDevApiConfiguration)
        $plan.IsValid | Should Be $true
        @($plan.Violations).Count | Should Be 0
    }

    It 'ships a non-secret DEV safety template accepted by the validator' {
        $templatePath = Join-Path $scriptRoot 'api-internal.dev.template.json'
        $template = Get-Content -LiteralPath $templatePath -Raw | ConvertFrom-Json
        $plan = New-DevApiConfigurationPlan -ExistingConfiguration $template
        $plan.IsValid | Should Be $true
        (Get-Content -LiteralPath $templatePath -Raw) | Should Not Match '(?i)(password|token|secret)\s*[:=]\s*["'']?[A-Za-z0-9+/=._-]{8,}'
    }

    $refusals = @(
        @{ Name = 'production database'; Key = 'SQL_DATABASE'; Value = 'kermaria'; Expected = 'SQL_DATABASE' },
        @{ Name = 'live Stripe'; Key = 'STRIPE_MODE'; Value = 'live'; Expected = 'STRIPE_MODE' },
        @{ Name = 'live Stripe secret family'; Key = 'STRIPE_SECRET_KEY'; Value = 'sk_live_not_a_real_value'; Expected = 'STRIPE_SECRET_KEY' },
        @{ Name = 'production KoXo receiver'; Key = 'KOXO_SYNC_WEBHOOK_URL'; Value = 'http://192.168.100.221:8042/internal/koxo/sync/'; Expected = 'KOXO_SYNC_WEBHOOK_URL' },
        @{ Name = 'production KoXo identifier prefix'; Key = 'KOXO_IDENTIFIER_PREFIX'; Value = 'CLI-'; Expected = 'KOXO_IDENTIFIER_PREFIX' },
        @{ Name = 'non DEV customer reference prefix'; Key = 'CUSTOMER_REFERENCE_PREFIX'; Value = 'CLI-'; Expected = 'CUSTOMER_REFERENCE_PREFIX' },
        @{ Name = 'production clients OU'; Key = 'AD_CLIENTS_OU_DN'; Value = 'OU=CLIENTS,DC=clients,DC=home,DC=bzh'; Expected = 'AD_CLIENTS_OU_DN' },
        @{ Name = 'known production AD account'; Key = 'AD_SERVICE_ACCOUNT_USERNAME'; Value = 'HOME\svc-kermaria'; Expected = 'AD_SERVICE_ACCOUNT_USERNAME' }
    )

    foreach ($refusal in $refusals) {
        It ("refuses {0}" -f $refusal.Name) {
            $configuration = New-ValidDevApiConfiguration
            $configuration[$refusal.Key] = $refusal.Value
            $plan = New-DevApiConfigurationPlan -ExistingConfiguration $configuration
            $plan.IsValid | Should Be $false
            ($plan.Violations -join ' | ') | Should Match $refusal.Expected
        }
    }

    It 'refuses a wildcard in an email live allowlist' {
        $configuration = New-ValidDevApiConfiguration
        $configuration.EMAIL_LIVE_ALLOWLIST = '*'
        $plan = New-DevApiConfigurationPlan -ExistingConfiguration $configuration
        $plan.IsValid | Should Be $false
        ($plan.Violations -join ' | ') | Should Match 'allowlist'
    }

    It 'preserves an existing secret when the source supplies no replacement' {
        $configuration = New-ValidDevApiConfiguration
        $configuration.SERVICE_AUTH_TOKEN = 'old'
        $plan = New-DevApiConfigurationPlan -ExistingConfiguration $configuration
        $plan.IsValid | Should Be $true
        $plan.Configuration.SERVICE_AUTH_TOKEN | Should Be 'old'
        @($plan.ChangedKeys).Count | Should Be 0
    }

    It 'parses only explicit DEV_API secret assignments without executing them' {
        $sourcePath = Join-Path $TestDrive 'api-dev-secrets.ps1'
        [System.IO.File]::WriteAllText($sourcePath, "`$env:DEV_API_SERVICE_AUTH_TOKEN = 'new'`r`n")
        $overrides = Read-DevApiSecretOverrides -Path $sourcePath
        $overrides.SERVICE_AUTH_TOKEN | Should Be 'new'

        [System.IO.File]::WriteAllText($sourcePath, "`$env:SQL_DATABASE = 'kermaria'`r`n")
        { Read-DevApiSecretOverrides -Path $sourcePath } | Should Throw 'DEV_API_*'
    }

    It 'never writes a secret to diagnostic output' {
        $secret = 's3'
        $configuration = New-ValidDevApiConfiguration
        $configuration.SERVICE_AUTH_TOKEN = $secret
        $plan = New-DevApiConfigurationPlan -ExistingConfiguration $configuration
        $target = Join-Path $TestDrive 'config.json'
        $output = (& { $null = Write-DevApiConfiguration -Plan $plan -Path $target -WhatIf } 2>&1 | Out-String)
        $output | Should Not Match $secret
        Test-Path -LiteralPath $target | Should Be $false
    }

    It 'does not modify an existing file when validation fails' {
        $target = Join-Path $TestDrive 'config.json'
        [System.IO.File]::WriteAllText($target, 'unchanged')
        $configuration = New-ValidDevApiConfiguration
        $configuration.SQL_DATABASE = 'kermaria'
        $plan = New-DevApiConfigurationPlan -ExistingConfiguration $configuration
        { Write-DevApiConfiguration -Plan $plan -Path $target } | Should Throw 'aucune ecriture'
        [System.IO.File]::ReadAllText($target) | Should Be 'unchanged'
    }

    It 'does not modify an existing file in dry-run mode' {
        $target = Join-Path $TestDrive 'config.json'
        [System.IO.File]::WriteAllText($target, 'unchanged')
        $plan = New-DevApiConfigurationPlan -ExistingConfiguration (New-ValidDevApiConfiguration)
        $null = Write-DevApiConfiguration -Plan $plan -Path $target -WhatIf
        [System.IO.File]::ReadAllText($target) | Should Be 'unchanged'
    }

    It 'is stable when the same valid configuration is written twice' {
        $target = Join-Path $TestDrive 'config.json'
        $plan = New-DevApiConfigurationPlan -ExistingConfiguration (New-ValidDevApiConfiguration)
        Write-DevApiConfiguration -Plan $plan -Path $target | Should Be $true
        $first = [System.IO.File]::ReadAllText($target)
        $secondPlan = New-DevApiConfigurationPlan -ExistingConfiguration (Get-Content -LiteralPath $target -Raw | ConvertFrom-Json)
        Write-DevApiConfiguration -Plan $secondPlan -Path $target | Should Be $true
        [System.IO.File]::ReadAllText($target) | Should Be $first
    }

    It 'remains executable in Windows PowerShell 5.1' {
        $scriptPath = (Join-Path $scriptRoot 'DevApiConfig.ps1').Replace("'", "''")
        $command = ". '$scriptPath'; if (`$PSVersionTable.PSVersion.Major -ne 5) { throw 'Expected Windows PowerShell 5.1.' }"
        & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command $command
        $LASTEXITCODE | Should Be 0
    }
}
