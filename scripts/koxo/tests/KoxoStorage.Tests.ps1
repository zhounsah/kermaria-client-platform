$modulePath = Join-Path (Split-Path -Parent $PSScriptRoot) 'KoxoStorage.Common.psm1'
Import-Module $modulePath -Force -DisableNameChecking

# Ce fichier n'a pas de marque d'ordre d'octets et PowerShell 5.1 le relirait
# alors en ANSI : le nom du groupe primaire de demonstration s'ecrit donc par
# code de caractere, jamais litteralement.
$script:PrimaryGroupDemo = 'CLIENTS D' + [char]0x00C9 + 'MO'

# Un mot de passe en clair figure volontairement dans la fiche de test : c'est
# le cas reel, et c'est ce qui rend verifiable le fait qu'il ne fuit ni dans le
# journal, ni a la reecriture.
$script:SheetSecret = 'mot-de-passe-en-clair'

function New-KoxoStorageSandbox {
    $root = Join-Path ([System.IO.Path]::GetTempPath()) ('koxo-storage-' + [guid]::NewGuid().ToString('N'))
    $dataRoot = Join-Path $root 'Data'
    $work = Join-Path $root 'work'
    New-Item -ItemType Directory -Path (Join-Path $dataRoot 'Users') -Force | Out-Null
    New-Item -ItemType Directory -Path $work -Force | Out-Null

    $configuration = Get-KoxoStorageConfiguration -Overrides @{
        KOXO_STORAGE_DATA_ROOT = $dataRoot
        KOXO_LOG_DIRECTORY = (Join-Path $work 'logs')
        KOXO_KOXO_LOG_GLOB = ''
        KOXO_SYNC_TIMEOUT_SECONDS = '30'
        KOXO_BACKUP_RETENTION_COUNT = '3'
        KOXO_STORAGE_FSRM_ENABLED = 'false'
    } -WorkingDirectory $work

    [pscustomobject]@{
        Root = $root
        DataRoot = $dataRoot
        Configuration = $configuration
    }
}

function New-KoxoSheetContent {
    param(
        [int]$Enabled = 0,
        [long]$Quota = 5120
    )

    @"
<?xml version="1.0" encoding="utf-8"?>
<User>
  <Login>zachary.hounsahou</Login>
  <UserFolderQuota>32768</UserFolderQuota>
  <EnableFolderQuota>$Enabled</EnableFolderQuota>
  <FolderQuota>$Quota</FolderQuota>
  <Password>$script:SheetSecret</Password>
  <AllowRDS>1</AllowRDS>
</User>
"@
}

function Write-KoxoSheet {
    param(
        [string]$Path,
        [string]$Content
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    [System.IO.File]::WriteAllText($Path, $Content, (New-Object System.Text.UTF8Encoding($false)))
}

function Invoke-KoxoStorageTestReconcile {
    param(
        $Sandbox,
        [string]$TargetKind = 'user',
        [string]$PrimaryGroup = 'CLIENTS',
        [string]$SecondaryGroup = 'CLI-000042',
        [string]$UserId = 'zachary.hounsahou',
        [long]$DesiredQuotaMib = 32768,
        [scriptblock]$RepairInvoker = $null
    )

    if ($null -eq $RepairInvoker) {
        $RepairInvoker = { param($a) $script:CapturedArguments = $a }
    }

    Invoke-KoxoStorageReconcile `
        -Configuration $Sandbox.Configuration `
        -TargetKind $TargetKind `
        -PrimaryGroup $PrimaryGroup `
        -SecondaryGroup $SecondaryGroup `
        -UserId $UserId `
        -DesiredQuotaMib $DesiredQuotaMib `
        -CorrelationId 'corr-test' `
        -TargetKey 'user:55555555-5555-5555-5555-555555555555' `
        -SubscriptionItemId 'item-a' `
        -RepairInvoker $RepairInvoker
}

# Le preflight est un script, pas un module : on en extrait la seule fonction
# testable par l'arbre syntaxique plutot que de le sourcer, ce qui declencherait
# tout le parcours de lecture.
$script:ReadinessScriptPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'Test-KoxoStorageReadiness.ps1'
$script:ReadinessAst = [System.Management.Automation.Language.Parser]::ParseFile(
    $script:ReadinessScriptPath, [ref]$null, [ref]$null)
$script:ReadOnlyQueryAst = $script:ReadinessAst.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Invoke-ReadOnlyQuery'
}, $true)
. ([scriptblock]::Create($script:ReadOnlyQueryAst[0].Extent.Text))

# Le client MariaDB reel emet un avertissement a chaque appel : le substitut le
# reproduit pour que le filtrage reste couvert par le comptage des lignes.
$script:FakeRowCount = 0
function Invoke-FakeMysqlClient {
    Write-Output 'Warning: Using a password on the command line interface can be insecure.'
    for ($i = 1; $i -le $script:FakeRowCount; $i++) {
        Write-Output ("id-$i`tvaleur-$i")
    }
    $global:LASTEXITCODE = 0
}

$script:MysqlClientPath = 'Invoke-FakeMysqlClient'
$script:SqlHost = 'localhost'
$script:SqlPort = '3306'
$script:SqlDatabase = 'kermaria'
$script:SqlUsername = 'kermaria_api'
$script:SqlPassword = ''

Describe 'Test-KoxoStorageReadiness.ps1 : Invoke-ReadOnlyQuery' {
    It 'returns an empty array when nothing matches' {
        $script:FakeRowCount = 0
        $rows = Invoke-ReadOnlyQuery -Sql 'SELECT 1;'
        $rows.GetType().IsArray | Should Be $true
        $rows.Count | Should Be 0
    }

    It 'returns a one-element array rather than a scalar for a single row' {
        # C'est le cas nominal du preflight : un utilisateur portail, un lien
        # annuaire. Un scalaire ferait echouer $rows.Count sous StrictMode.
        $script:FakeRowCount = 1
        $rows = Invoke-ReadOnlyQuery -Sql 'SELECT 1;'
        $rows.GetType().IsArray | Should Be $true
        $rows.Count | Should Be 1
        $rows[0] | Should Be "id-1`tvaleur-1"
    }

    It 'returns every row when several match' {
        $script:FakeRowCount = 3
        $rows = Invoke-ReadOnlyQuery -Sql 'SELECT 1;'
        $rows.GetType().IsArray | Should Be $true
        $rows.Count | Should Be 3
        $rows[2] | Should Be "id-3`tvaleur-3"
    }
}

Describe 'Resolve-KoxoStorageTargetPath' {
    It 'places a user sheet at the exact KoXo location' {
        $path = Resolve-KoxoStorageTargetPath -UsersRoot 'C:\Data\Users' -TargetKind 'user' `
            -PrimaryGroup 'CLIENTS' -SecondaryGroup 'CLI-000042' -UserId 'zachary.hounsahou'

        $path | Should Be 'C:\Data\Users\CLIENTS\CLI-000042\zachary.hounsahou.xml'
    }

    It 'places a secondary group sheet under its primary group' {
        $path = Resolve-KoxoStorageTargetPath -UsersRoot 'C:\Data\Users' -TargetKind 'secondary_group' `
            -PrimaryGroup $script:PrimaryGroupDemo -SecondaryGroup 'DEMO-CLI-000042'

        $path | Should Be ('C:\Data\Users\{0}\DEMO-CLI-000042.xml' -f $script:PrimaryGroupDemo)
    }

    It 'refuses a component that would escape the data root' {
        # Aucune recherche approximative et aucune remontee d'arborescence : ces
        # valeurs viennent d'une requete distante.
        { Resolve-KoxoStorageTargetPath -UsersRoot 'C:\Data\Users' -TargetKind 'user' `
            -PrimaryGroup 'CLIENTS' -SecondaryGroup '..' -UserId 'x' } | Should Throw

        { Resolve-KoxoStorageTargetPath -UsersRoot 'C:\Data\Users' -TargetKind 'user' `
            -PrimaryGroup 'CLIENTS' -SecondaryGroup 'CLI-1' -UserId 'a\b' } | Should Throw

        { Resolve-KoxoStorageTargetPath -UsersRoot 'C:\Data\Users' -TargetKind 'user' `
            -PrimaryGroup 'CLIENTS' -SecondaryGroup 'CLI-1' -UserId '' } | Should Throw
    }

    It 'refuses a titleholder on a shared target' {
        { Resolve-KoxoStorageTargetPath -UsersRoot 'C:\Data\Users' -TargetKind 'secondary_group' `
            -PrimaryGroup 'CLIENTS' -SecondaryGroup 'CLI-1' -UserId 'zachary' } | Should Throw
    }
}

Describe 'Get-KoxoStorageDecision' {
    It 'reports a missing object instead of creating it' {
        $state = [pscustomobject]@{ Exists = $false; Enabled = $false; QuotaMib = $null; Content = $null; Ambiguous = $false; AmbiguityReason = $null }
        (Get-KoxoStorageDecision -State $state -DesiredQuotaMib 32768).Decision | Should Be 'NOT_MATERIALIZED'
    }

    It 'reports a no-op when the applied quota already matches' {
        $state = [pscustomobject]@{ Exists = $true; Enabled = $true; QuotaMib = 32768; Content = ''; Ambiguous = $false; AmbiguityReason = $null }
        (Get-KoxoStorageDecision -State $state -DesiredQuotaMib 32768).Decision | Should Be 'NOOP'
    }

    It 'reports an increase when the applied quota is lower' {
        $state = [pscustomobject]@{ Exists = $true; Enabled = $true; QuotaMib = 5120; Content = ''; Ambiguous = $false; AmbiguityReason = $null }
        (Get-KoxoStorageDecision -State $state -DesiredQuotaMib 32768).Decision | Should Be 'APPLY_INCREASE'
    }

    It 'refuses a reduction' {
        # Abaisser un quota sous l'occupation reelle bloque l'utilisateur sans
        # rien liberer : cette phase ne reduit jamais.
        $state = [pscustomobject]@{ Exists = $true; Enabled = $true; QuotaMib = 65536; Content = ''; Ambiguous = $false; AmbiguityReason = $null }
        (Get-KoxoStorageDecision -State $state -DesiredQuotaMib 32768).Decision | Should Be 'BLOCKED_REDUCTION'
    }

    It 'treats a disabled quota as a change, and still refuses to lower it' {
        $lower = [pscustomobject]@{ Exists = $true; Enabled = $false; QuotaMib = 5120; Content = ''; Ambiguous = $false; AmbiguityReason = $null }
        (Get-KoxoStorageDecision -State $lower -DesiredQuotaMib 32768).Decision | Should Be 'APPLY_INCREASE'

        $higher = [pscustomobject]@{ Exists = $true; Enabled = $false; QuotaMib = 65536; Content = ''; Ambiguous = $false; AmbiguityReason = $null }
        (Get-KoxoStorageDecision -State $higher -DesiredQuotaMib 32768).Decision | Should Be 'BLOCKED_REDUCTION'
    }

    It 'fails closed on an ambiguous sheet and on a non positive quota' {
        $ambiguous = [pscustomobject]@{ Exists = $true; Enabled = $false; QuotaMib = $null; Content = ''; Ambiguous = $true; AmbiguityReason = 'x' }
        (Get-KoxoStorageDecision -State $ambiguous -DesiredQuotaMib 32768).Decision | Should Be 'FAILED_CLOSED'

        $valid = [pscustomobject]@{ Exists = $true; Enabled = $true; QuotaMib = 5120; Content = ''; Ambiguous = $false; AmbiguityReason = $null }
        (Get-KoxoStorageDecision -State $valid -DesiredQuotaMib 0).Decision | Should Be 'FAILED_CLOSED'
    }
}

Describe 'Read-KoxoStorageQuotaState' {
    It 'does not confuse FolderQuota with UserFolderQuota' {
        $sandbox = New-KoxoStorageSandbox
        try {
            $path = Join-Path $sandbox.DataRoot 'Users\CLIENTS\CLI-000042\zachary.hounsahou.xml'
            Write-KoxoSheet -Path $path -Content (New-KoxoSheetContent -Enabled 1 -Quota 5120)

            $state = Read-KoxoStorageQuotaState -Path $path
            $state.Exists | Should Be $true
            $state.Enabled | Should Be $true
            $state.QuotaMib | Should Be 5120
            $state.Ambiguous | Should Be $false
        }
        finally {
            Remove-Item -LiteralPath $sandbox.Root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'flags a duplicated quota element as ambiguous' {
        $sandbox = New-KoxoStorageSandbox
        try {
            $path = Join-Path $sandbox.DataRoot 'Users\CLIENTS\CLI-000042\zachary.hounsahou.xml'
            Write-KoxoSheet -Path $path -Content "<User><EnableFolderQuota>1</EnableFolderQuota><FolderQuota>1</FolderQuota><FolderQuota>2</FolderQuota></User>"

            (Read-KoxoStorageQuotaState -Path $path).Ambiguous | Should Be $true
        }
        finally {
            Remove-Item -LiteralPath $sandbox.Root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'Set-KoxoStorageQuotaContent' {
    It 'changes only the two quota elements' {
        $before = New-KoxoSheetContent -Enabled 0 -Quota 5120
        $after = Set-KoxoStorageQuotaContent -Content $before -DesiredQuotaMib 32768

        $after | Should Match '<EnableFolderQuota>1</EnableFolderQuota>'
        $after | Should Match '<FolderQuota>32768</FolderQuota>'
        # Le reste de la fiche est reapplique tel quel a l'annuaire a chaque
        # synchro : le modifier au passage aurait des effets hors sujet.
        $after | Should Match '<UserFolderQuota>32768</UserFolderQuota>'
        $after | Should Match '<AllowRDS>1</AllowRDS>'
        $after | Should Match ([regex]::Escape($script:SheetSecret))
    }
}

Describe 'Get-KoxoStorageRepairArguments' {
    It 'repairs only the storage aspect of a user' {
        $arguments = Get-KoxoStorageRepairArguments -TargetKind 'user' -PrimaryGroup 'CLIENTS' `
            -SecondaryGroup 'CLI-000042' -UserId 'zachary.hounsahou'

        $arguments | Should Be '/RepairUser UserId="zachary.hounsahou" Type="Storage"'
    }

    It 'repairs only the storage aspect of a secondary group' {
        $arguments = Get-KoxoStorageRepairArguments -TargetKind 'secondary_group' -PrimaryGroup 'CLIENTS' `
            -SecondaryGroup 'CLI-000042'

        $arguments | Should Be '/RepairSecondaryGroup Group="CLI-000042" PrimaryGroup="CLIENTS" Type="Storage"'
    }

    It 'never asks for a full repair' {
        $source = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'KoxoStorage.Common.psm1') -Raw
        $source | Should Not Match '/Synchro'
        $source | Should Not Match 'Invoke-KoxoSync\b'
    }
}

Describe 'Invoke-KoxoStorageReconcile (fiche personnelle)' {
    It 'applies an increase, proves it and repairs only the storage' {
        $sandbox = New-KoxoStorageSandbox
        try {
            $path = Join-Path $sandbox.DataRoot 'Users\CLIENTS\CLI-000042\zachary.hounsahou.xml'
            Write-KoxoSheet -Path $path -Content (New-KoxoSheetContent -Enabled 0 -Quota 5120)
            $script:CapturedArguments = $null

            $result = Invoke-KoxoStorageTestReconcile -Sandbox $sandbox

            $result.status | Should Be 'applied'
            $result.verification | Should Be 'xml_verified'
            $script:CapturedArguments | Should Be '/RepairUser UserId="zachary.hounsahou" Type="Storage"'

            $final = Read-KoxoStorageQuotaState -Path $path
            $final.Enabled | Should Be $true
            $final.QuotaMib | Should Be 32768

            # Sauvegarde locale avant modification.
            @(Get-ChildItem -LiteralPath $sandbox.Configuration.BackupDirectory -Filter '*.bak').Count | Should Be 1
        }
        finally {
            Remove-Item -LiteralPath $sandbox.Root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'does nothing when the quota already matches' {
        $sandbox = New-KoxoStorageSandbox
        try {
            $path = Join-Path $sandbox.DataRoot 'Users\CLIENTS\CLI-000042\zachary.hounsahou.xml'
            Write-KoxoSheet -Path $path -Content (New-KoxoSheetContent -Enabled 1 -Quota 32768)
            $script:CapturedArguments = $null

            $result = Invoke-KoxoStorageTestReconcile -Sandbox $sandbox

            $result.status | Should Be 'noop'
            $script:CapturedArguments | Should BeNullOrEmpty
        }
        finally {
            Remove-Item -LiteralPath $sandbox.Root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'refuses a reduction without touching the sheet' {
        $sandbox = New-KoxoStorageSandbox
        try {
            $path = Join-Path $sandbox.DataRoot 'Users\CLIENTS\CLI-000042\zachary.hounsahou.xml'
            Write-KoxoSheet -Path $path -Content (New-KoxoSheetContent -Enabled 1 -Quota 65536)
            $script:CapturedArguments = $null

            $result = Invoke-KoxoStorageTestReconcile -Sandbox $sandbox

            $result.status | Should Be 'blocked_reduction'
            $script:CapturedArguments | Should BeNullOrEmpty
            (Read-KoxoStorageQuotaState -Path $path).QuotaMib | Should Be 65536
        }
        finally {
            Remove-Item -LiteralPath $sandbox.Root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'blocks on a user whose sheet does not exist' {
        $sandbox = New-KoxoStorageSandbox
        try {
            $result = Invoke-KoxoStorageTestReconcile -Sandbox $sandbox -UserId 'inconnu.absent'
            $result.status | Should Be 'not_materialized'
        }
        finally {
            Remove-Item -LiteralPath $sandbox.Root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'fails closed on an ambiguous sheet' {
        $sandbox = New-KoxoStorageSandbox
        try {
            $path = Join-Path $sandbox.DataRoot 'Users\CLIENTS\CLI-000042\zachary.hounsahou.xml'
            Write-KoxoSheet -Path $path -Content '<User><Login>x</Login></User>'

            $result = Invoke-KoxoStorageTestReconcile -Sandbox $sandbox
            $result.status | Should Be 'failed'
            $result.reasonCode | Should Be 'BILLING_V2_KOXO_STORAGE_SHEET_AMBIGUOUS'
        }
        finally {
            Remove-Item -LiteralPath $sandbox.Root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'propagates a failing repair instead of claiming success' {
        $sandbox = New-KoxoStorageSandbox
        try {
            $path = Join-Path $sandbox.DataRoot 'Users\CLIENTS\CLI-000042\zachary.hounsahou.xml'
            Write-KoxoSheet -Path $path -Content (New-KoxoSheetContent -Enabled 0 -Quota 5120)

            # Invoke-KoxoProcess leve quand rien, dans le journal KoXo, ne
            # prouve l'execution : le code de sortie ne suffit pas, KoXoAdm.exe
            # sort en 1 meme en succes.
            { Invoke-KoxoStorageTestReconcile -Sandbox $sandbox -RepairInvoker {
                param($a) throw 'KoXo process timed out after 30 seconds.'
            } } | Should Throw
        }
        finally {
            Remove-Item -LiteralPath $sandbox.Root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'fails when the sheet no longer proves the quota after the repair' {
        $sandbox = New-KoxoStorageSandbox
        try {
            $path = Join-Path $sandbox.DataRoot 'Users\CLIENTS\CLI-000042\zachary.hounsahou.xml'
            Write-KoxoSheet -Path $path -Content (New-KoxoSheetContent -Enabled 0 -Quota 5120)

            # Une reparation qui reecrirait la fiche depuis la base KoXo
            # annulerait la modification : l'ecriture n'est pas sa preuve.
            $result = Invoke-KoxoStorageTestReconcile -Sandbox $sandbox -RepairInvoker {
                param($a)
                Write-KoxoSheet -Path $path -Content (New-KoxoSheetContent -Enabled 0 -Quota 5120)
            }

            $result.status | Should Be 'failed'
            $result.reasonCode | Should Be 'BILLING_V2_KOXO_STORAGE_VERIFICATION_FAILED'
        }
        finally {
            Remove-Item -LiteralPath $sandbox.Root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'never writes a sheet secret into the storage log' {
        $sandbox = New-KoxoStorageSandbox
        try {
            $path = Join-Path $sandbox.DataRoot 'Users\CLIENTS\CLI-000042\zachary.hounsahou.xml'
            Write-KoxoSheet -Path $path -Content (New-KoxoSheetContent -Enabled 0 -Quota 5120)
            Invoke-KoxoStorageTestReconcile -Sandbox $sandbox | Out-Null

            $log = Get-Content -LiteralPath $sandbox.Configuration.LogPath -Raw
            $log | Should Not Match ([regex]::Escape($script:SheetSecret))
        }
        finally {
            Remove-Item -LiteralPath $sandbox.Root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'Invoke-KoxoStorageReconcile (groupe secondaire)' {
    It 'applies, no-ops and refuses a reduction on the shared sheet' {
        $sandbox = New-KoxoStorageSandbox
        try {
            $path = Join-Path $sandbox.DataRoot 'Users\CLIENTS\CLI-000042.xml'
            Write-KoxoSheet -Path $path -Content (New-KoxoSheetContent -Enabled 0 -Quota 5120)
            $script:CapturedArguments = $null

            $applied = Invoke-KoxoStorageTestReconcile -Sandbox $sandbox -TargetKind 'secondary_group' -UserId ''
            $applied.status | Should Be 'applied'
            $script:CapturedArguments | Should Be '/RepairSecondaryGroup Group="CLI-000042" PrimaryGroup="CLIENTS" Type="Storage"'

            $again = Invoke-KoxoStorageTestReconcile -Sandbox $sandbox -TargetKind 'secondary_group' -UserId ''
            $again.status | Should Be 'noop'

            $lower = Invoke-KoxoStorageTestReconcile -Sandbox $sandbox -TargetKind 'secondary_group' -UserId '' -DesiredQuotaMib 1024
            $lower.status | Should Be 'blocked_reduction'
        }
        finally {
            Remove-Item -LiteralPath $sandbox.Root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'blocks on a secondary group whose sheet does not exist' {
        $sandbox = New-KoxoStorageSandbox
        try {
            $result = Invoke-KoxoStorageTestReconcile -Sandbox $sandbox -TargetKind 'secondary_group' -UserId '' -SecondaryGroup 'CLI-INEXISTANT'
            $result.status | Should Be 'not_materialized'
        }
        finally {
            Remove-Item -LiteralPath $sandbox.Root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'Read-KoxoStorageRequest' {
    It 'accepts a complete user request' {
        $request = Read-KoxoStorageRequest -Body '{"correlationId":"c1","targetKind":"user","userId":"zachary.hounsahou","primaryGroup":"CLIENTS","secondaryGroup":"CLI-000042","desiredQuotaMib":32768,"targetKey":"user:g","subscriptionItemId":"item-a"}'

        $request.TargetKind | Should Be 'user'
        $request.UserId | Should Be 'zachary.hounsahou'
        $request.DesiredQuotaMib | Should Be 32768
    }

    It 'refuses an incoherent or incomplete contract' {
        # Rien n'est complete par defaut : un champ manquant signale un appelant
        # qui ne decrit pas ce qu'il croit decrire.
        { Read-KoxoStorageRequest -Body '' } | Should Throw
        { Read-KoxoStorageRequest -Body '{"correlationId":"c1","targetKind":"everything","primaryGroup":"CLIENTS","secondaryGroup":"CLI-1","desiredQuotaMib":1}' } | Should Throw
        { Read-KoxoStorageRequest -Body '{"targetKind":"user","userId":"x","primaryGroup":"CLIENTS","secondaryGroup":"CLI-1","desiredQuotaMib":1}' } | Should Throw
        { Read-KoxoStorageRequest -Body '{"correlationId":"c1","targetKind":"user","primaryGroup":"CLIENTS","secondaryGroup":"CLI-1","desiredQuotaMib":1}' } | Should Throw
        { Read-KoxoStorageRequest -Body '{"correlationId":"c1","targetKind":"secondary_group","userId":"x","primaryGroup":"CLIENTS","secondaryGroup":"CLI-1","desiredQuotaMib":1}' } | Should Throw
        { Read-KoxoStorageRequest -Body '{"correlationId":"c1","targetKind":"user","userId":"x","primaryGroup":"CLIENTS","secondaryGroup":"CLI-1","desiredQuotaMib":0}' } | Should Throw
        { Read-KoxoStorageRequest -Body '{"correlationId":"c1","targetKind":"user","userId":"x","primaryGroup":"CLIENTS","secondaryGroup":"..","desiredQuotaMib":1}' } | Should Throw
    }
}

Describe 'Start-KoxoSyncWebhookReceiver.ps1 (route de stockage)' {
    $receiver = Join-Path (Split-Path -Parent $PSScriptRoot) 'Start-KoxoSyncWebhookReceiver.ps1'
    $source = Get-Content -LiteralPath $receiver -Raw

    It 'serves the targeted operation on its own route' {
        $source | Should Match '/internal/koxo/storage/reconcile/'
        $source | Should Match '\$listener\.Prefixes\.Add\(\$storagePrefix\)'
        $source | Should Match 'Invoke-KoxoStorageReconcile'
    }

    It 'never lets the storage route reach the global CSV synchronisation' {
        # La branche de stockage rend sa reponse puis « continue » : la
        # synchronisation globale reconcilie TOUTE la branche et, avec
        # DisableOrphanedAccounts, desactive ce qui n'est plus dans le CSV.
        $storageIndex = $source.IndexOf('# Traitement SYNCHRONE')
        $syncIndex = $source.IndexOf('$payload = if ([string]::IsNullOrWhiteSpace($body))')
        $storageIndex | Should BeGreaterThan 0
        $syncIndex | Should BeGreaterThan $storageIndex

        $storageBranch = $source.Substring($storageIndex, $syncIndex - $storageIndex)
        $storageBranch | Should Not Match 'Start-Process'
        $storageBranch | Should Match 'continue'
    }

    It 'authenticates the storage route and rejects an unknown route' {
        $source | Should Match '\$expectedToken = if \(\$isStorageRequest\)'
        $source | Should Match "code = 'UNAUTHORIZED'"
        $source | Should Match "code = 'NOT_FOUND'"
    }

    It 'keeps the storage token separable from the synchronisation token' {
        $source | Should Match 'KOXO_STORAGE_WEBHOOK_TOKEN'
    }
}
