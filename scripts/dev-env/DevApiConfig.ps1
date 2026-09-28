<#
.SYNOPSIS
  Primitives non interactives de validation et d'ecriture de la configuration
  API-INTERNAL DEV.

.DESCRIPTION
  Ce fichier est volontairement un script de bibliotheque PowerShell 5.1 : il
  peut etre charge localement par les tests, ou en memoire dans une session
  WinRM par Install-ApiInternalDev.ps1. Il ne lit ni n'affiche une valeur de
  secret. Une source de secrets DEV est analysee par l'AST PowerShell, jamais
  executee.
#>

$script:DevApiSecretKeys = @(
    'SQL_HOST',
    'SQL_PORT',
    'SQL_USERNAME',
    'SQL_PASSWORD',
    'SERVICE_AUTH_TOKEN',
    'STRIPE_SECRET_KEY',
    'STRIPE_PUBLISHABLE_KEY',
    'STRIPE_WEBHOOK_SECRET',
    'AD_SERVICE_ACCOUNT_USERNAME',
    'AD_SERVICE_ACCOUNT_PASSWORD',
    'SMTP_USERNAME',
    'SMTP_PASSWORD',
    'BPCE_REFRESH_TOKEN',
    'PAYPAL_CLIENT_ID',
    'PAYPAL_CLIENT_SECRET',
    'PAYPAL_WEBHOOK_ID',
    'KOXO_EXPORT_API_TOKEN',
    'KOXO_SYNC_WEBHOOK_TOKEN',
    'KOXO_PENDING_PASSWORD_KEY',
    'BILLING_V2_KOXO_STORAGE_TOKEN'
)

$script:KnownProductionAdAccounts = @(
    'HOME\svc-kermaria'
)

function ConvertTo-DevApiHashtable {
    param([Parameter(Mandatory)] [object] $InputObject)

    $result = [ordered]@{}
    if ($InputObject -is [System.Collections.IDictionary]) {
        foreach ($key in $InputObject.Keys) {
            $result[[string]$key] = if ($null -eq $InputObject[$key]) { '' } else { [string]$InputObject[$key] }
        }
        return $result
    }

    foreach ($property in $InputObject.PSObject.Properties) {
        $result[[string]$property.Name] = if ($null -eq $property.Value) { '' } else { [string]$property.Value }
    }
    return $result
}

function Get-DevApiConfigValue {
    param(
        [Parameter(Mandatory)] [System.Collections.IDictionary] $Configuration,
        [Parameter(Mandatory)] [string] $Name
    )

    if ($Configuration.Contains($Name)) {
        return ([string]$Configuration[$Name]).Trim()
    }
    return ''
}

function Test-DevApiSecretKey {
    param([Parameter(Mandatory)] [string] $Name)
    return $script:DevApiSecretKeys -contains $Name
}

function Read-DevApiSecretOverrides {
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw 'La source de secrets DEV est introuvable.'
    }

    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile(
        (Resolve-Path -LiteralPath $Path),
        [ref]$tokens,
        [ref]$parseErrors)
    if ($parseErrors.Count -gt 0) {
        throw 'La source de secrets DEV contient une syntaxe PowerShell invalide.'
    }

    $commands = $ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] }, $true)
    if ($commands.Count -gt 0) {
        throw 'La source de secrets DEV ne peut contenir aucune commande PowerShell.'
    }

    $overrides = [ordered]@{}
    $assignments = $ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.AssignmentStatementAst] }, $true)
    foreach ($assignment in $assignments) {
        $left = $assignment.Left.Extent.Text
        if ($left -notmatch '^(?i)\$env:DEV_API_([A-Z0-9_]+)$') {
            throw 'La source de secrets DEV ne peut definir que des variables DEV_API_*.'
        }

        $runtimeKey = $Matches[1].ToUpperInvariant()
        if (-not (Test-DevApiSecretKey -Name $runtimeKey)) {
            throw "La source de secrets DEV contient une cle non autorisee : DEV_API_$runtimeKey."
        }

        $literal = $assignment.Right
        if ($literal -is [System.Management.Automation.Language.CommandExpressionAst]) {
            $literal = $literal.Expression
        }
        if ($literal -isnot [System.Management.Automation.Language.StringConstantExpressionAst]) {
            throw "La valeur de DEV_API_$runtimeKey doit etre une chaine litterale."
        }

        if ($overrides.Contains($runtimeKey)) {
            throw "La source de secrets DEV definit DEV_API_$runtimeKey plus d'une fois."
        }

        $overrides[$runtimeKey] = [string]$literal.Value
    }

    return $overrides
}

function Test-DevApiConfiguration {
    [CmdletBinding()]
    param([Parameter(Mandatory)] [System.Collections.IDictionary] $Configuration)

    $violations = New-Object System.Collections.Generic.List[string]
    $appEnvironment = Get-DevApiConfigValue -Configuration $Configuration -Name 'APP_ENV'
    $database = Get-DevApiConfigValue -Configuration $Configuration -Name 'SQL_DATABASE'
    $stripeMode = Get-DevApiConfigValue -Configuration $Configuration -Name 'STRIPE_MODE'
    $stripeSecretKey = Get-DevApiConfigValue -Configuration $Configuration -Name 'STRIPE_SECRET_KEY'
    $stripePublishableKey = Get-DevApiConfigValue -Configuration $Configuration -Name 'STRIPE_PUBLISHABLE_KEY'
    $identifierPrefix = Get-DevApiConfigValue -Configuration $Configuration -Name 'KOXO_IDENTIFIER_PREFIX'
    $referencePrefix = Get-DevApiConfigValue -Configuration $Configuration -Name 'CUSTOMER_REFERENCE_PREFIX'
    $primaryGroup = Get-DevApiConfigValue -Configuration $Configuration -Name 'KOXO_PRIMARY_GROUP_CLIENTS'
    $demoGroup = Get-DevApiConfigValue -Configuration $Configuration -Name 'KOXO_PRIMARY_GROUP_DEMO'
    $webhook = Get-DevApiConfigValue -Configuration $Configuration -Name 'KOXO_SYNC_WEBHOOK_URL'
    $clientsOu = Get-DevApiConfigValue -Configuration $Configuration -Name 'AD_CLIENTS_OU_DN'
    $adMode = Get-DevApiConfigValue -Configuration $Configuration -Name 'AD_INTEGRATION_MODE'
    $adAccount = Get-DevApiConfigValue -Configuration $Configuration -Name 'AD_SERVICE_ACCOUNT_USERNAME'

    if (-not [string]::Equals($appEnvironment, 'Development', [System.StringComparison]::Ordinal)) {
        $violations.Add('APP_ENV doit valoir Development.')
    }
    if (-not [string]::Equals($database, 'kermaria_dev', [System.StringComparison]::OrdinalIgnoreCase)) {
        $violations.Add('SQL_DATABASE doit valoir kermaria_dev.')
    }
    if (-not [string]::Equals($stripeMode, 'test', [System.StringComparison]::OrdinalIgnoreCase)) {
        $violations.Add('STRIPE_MODE doit valoir test.')
    }
    if ($stripeSecretKey -match '^(?i)(sk|rk)_live_') {
        $violations.Add('STRIPE_SECRET_KEY ne peut pas appartenir a Stripe live.')
    }
    if ($stripePublishableKey -match '^(?i)pk_live_') {
        $violations.Add('STRIPE_PUBLISHABLE_KEY ne peut pas appartenir a Stripe live.')
    }
    if (-not [string]::Equals($identifierPrefix, 'CLI-D', [System.StringComparison]::Ordinal)) {
        $violations.Add('KOXO_IDENTIFIER_PREFIX doit valoir CLI-D.')
    }
    if (-not [string]::Equals($referencePrefix, 'DEV-CLI-', [System.StringComparison]::Ordinal)) {
        $violations.Add('CUSTOMER_REFERENCE_PREFIX doit valoir DEV-CLI-.')
    }
    if (-not [string]::Equals($primaryGroup, 'CLIENTS DEV', [System.StringComparison]::Ordinal)) {
        $violations.Add('KOXO_PRIMARY_GROUP_CLIENTS doit valoir CLIENTS DEV.')
    }
    if (-not [string]::Equals($demoGroup, 'CLIENTS DEV DEMO', [System.StringComparison]::Ordinal)) {
        $violations.Add('KOXO_PRIMARY_GROUP_DEMO doit valoir CLIENTS DEV DEMO.')
    }
    if (-not [string]::Equals($adMode, 'controlled_write', [System.StringComparison]::OrdinalIgnoreCase)) {
        $violations.Add('AD_INTEGRATION_MODE doit valoir controlled_write.')
    }
    if ($clientsOu -notmatch '(?i)(?:^|,)OU=CLIENTS DEV(?:,|$)') {
        $violations.Add('AD_CLIENTS_OU_DN doit etre sous OU=CLIENTS DEV.')
    }
    if ($script:KnownProductionAdAccounts -contains $adAccount) {
        $violations.Add('AD_SERVICE_ACCOUNT_USERNAME designe un compte de production connu.')
    }

    $webhookUri = $null
    $invalidWebhook = (-not [System.Uri]::TryCreate($webhook, [System.UriKind]::Absolute, [ref]$webhookUri)) -or $webhookUri.Port -ne 8043 -or $webhookUri.Host -match '(?i)(prod|production)'
    if ($invalidWebhook) {
        $violations.Add('KOXO_SYNC_WEBHOOK_URL doit viser exclusivement le recepteur DEV :8043.')
    }

    $emailMode = Get-DevApiConfigValue -Configuration $Configuration -Name 'EMAIL_INTEGRATION_MODE'
    if ([string]::Equals($emailMode, 'live', [System.StringComparison]::OrdinalIgnoreCase)) {
        $allowlistOnly = Get-DevApiConfigValue -Configuration $Configuration -Name 'EMAIL_LIVE_ALLOWLIST_ONLY'
        $allowlist = Get-DevApiConfigValue -Configuration $Configuration -Name 'EMAIL_LIVE_ALLOWLIST'
        $invalidAllowlist = (-not [string]::Equals($allowlistOnly, 'true', [System.StringComparison]::OrdinalIgnoreCase)) -or [string]::IsNullOrWhiteSpace($allowlist) -or $allowlist.Contains('*')
        if ($invalidAllowlist) {
            $violations.Add('EMAIL_INTEGRATION_MODE=live exige une allowlist explicite sans joker.')
        }
    }

    return $violations.ToArray()
}

function New-DevApiConfigurationPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $ExistingConfiguration,
        [hashtable] $SecretOverrides = @{}
    )

    $configuration = ConvertTo-DevApiHashtable -InputObject $ExistingConfiguration
    if ($configuration.Count -eq 0) {
        throw 'Configuration DEV existante absente : initialisation explicite requise, aucun fallback n est autorise.'
    }

    $changedKeys = New-Object System.Collections.Generic.List[string]
    foreach ($entry in $SecretOverrides.GetEnumerator()) {
        $name = ([string]$entry.Key).ToUpperInvariant()
        if (-not (Test-DevApiSecretKey -Name $name)) {
            throw "Cle de rafraichissement non autorisee : $name."
        }

        $value = if ($null -eq $entry.Value) { '' } else { [string]$entry.Value }
        $valueChanged = (-not $configuration.Contains($name)) -or (-not [string]::Equals([string]$configuration[$name], $value, [System.StringComparison]::Ordinal))
        if ($valueChanged) {
            $configuration[$name] = $value
            $changedKeys.Add($name)
        }
    }

    $violations = @(Test-DevApiConfiguration -Configuration $configuration)
    return [pscustomobject]@{
        IsValid = ($violations.Count -eq 0)
        Violations = $violations
        Configuration = $configuration
        ChangedKeys = $changedKeys.ToArray()
    }
}

function Write-DevApiConfiguration {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)] [psobject] $Plan,
        [Parameter(Mandatory)] [string] $Path
    )

    if (-not $Plan.IsValid) {
        throw 'Configuration DEV invalide : aucune ecriture n est autorisee.'
    }
    if (-not $PSCmdlet.ShouldProcess($Path, 'Ecrire atomiquement la configuration API DEV')) {
        return $false
    }

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $temporaryPath = Join-Path $directory ((Split-Path -Leaf $Path) + '.' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $json = $Plan.Configuration | ConvertTo-Json -Depth 3
        [System.IO.File]::WriteAllText($temporaryPath, $json, [System.Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $Path) {
            $backupPath = $temporaryPath + '.backup'
            [System.IO.File]::Replace($temporaryPath, $Path, $backupPath)
            if (Test-Path -LiteralPath $backupPath) {
                Remove-Item -LiteralPath $backupPath -Force
            }
        } else {
            [System.IO.File]::Move($temporaryPath, $Path)
        }
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
    return $true
}
