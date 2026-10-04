$koxoRoot = Split-Path -Parent $PSScriptRoot
$modulePath = Join-Path $koxoRoot 'KoxoSync.Common.psm1'
Import-Module $modulePath -Force -DisableNameChecking

# Ce fichier n'a pas de marque d'ordre d'octets : le E accent aigu s'ecrit par
# code de caractere.
$script:PrimaryGroupDemo = 'CLIENTS D' + [char]0x00C9 + 'MO'
$script:Launcher = Join-Path $koxoRoot 'Invoke-KoxoSyncFromWebhook.ps1'

# Aucun jeton n'est ecrit dans le depot : chaque valeur est tiree au hasard a
# l'execution et ne sert qu'a ce processus.
function New-KoxoFakeSecret {
    'fixture-' + [guid]::NewGuid().ToString('N')
}

function New-KoxoInstanceFixture {
    param(
        [hashtable]$Changes = @{},
        [switch]$WithoutReceiver
    )

    $root = Join-Path $env:TEMP ('koxo-instance-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    $apiToken = New-KoxoFakeSecret
    $webhookToken = New-KoxoFakeSecret
    $apiTokenPath = Join-Path $root 'api-token.txt'
    $webhookTokenPath = Join-Path $root 'webhook-token.txt'
    [System.IO.File]::WriteAllText($apiTokenPath, $apiToken)
    [System.IO.File]::WriteAllText($webhookTokenPath, $webhookToken)

    $definition = [ordered]@{
        instanceName = 'dev'
        apiUrl = 'https://dev.example.invalid/api/internal/koxo/users'
        apiTokenPath = $apiTokenPath
        identifierPrefix = 'CLI-D'
        workingDirectory = (Join-Path $root 'work')
        logDirectory = (Join-Path $root 'Logs')
        profiles = @(
            [ordered]@{
                primaryGroup = 'CLIENTS DEV'
                csvTargetPath = (Join-Path $root 'clients-dev.csv')
                koxoSyncArgument = '/Synchro=CLIENTS-DEV.xml'
            }
        )
        receiver = [ordered]@{
            prefix = 'http://+:8043/internal/koxo/sync/'
            tokenPath = $webhookTokenPath
            logDirectory = (Join-Path $root 'Logs\webhook')
        }
    }

    if ($WithoutReceiver) {
        $definition.Remove('receiver')
    }

    foreach ($key in $Changes.Keys) {
        $definition[$key] = $Changes[$key]
    }

    $path = Join-Path $root 'koxo-instance.json'
    [System.IO.File]::WriteAllText($path, ($definition | ConvertTo-Json -Depth 5), (New-Object System.Text.UTF8Encoding($false)))

    [pscustomobject]@{
        Root = $root
        Path = $path
        ApiToken = $apiToken
        WebhookToken = $webhookToken
        Definition = $definition
    }
}

function Invoke-KoxoLauncherPlan {
    param(
        [string]$InstanceConfigPath = '',
        [hashtable]$Environment = @{}
    )

    # Processus enfant : le lanceur modifie l'environnement de SON processus,
    # jamais celui des tests. Les variables fournies simulent l'environnement
    # herite du recepteur, donc celui de la production.
    $saved = @{}
    foreach ($name in $Environment.Keys) {
        $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $Environment[$name], 'Process')
    }

    try {
        # -SyncScriptPath passe explicitement, comme le fait le recepteur : sa
        # valeur par defaut historique n'est pas evaluable sous -File.
        $arguments = @(
            '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
            '-File', $script:Launcher,
            '-SyncScriptPath', (Join-Path (Split-Path -Parent $script:Launcher) 'Sync-KoXoClients.ps1'),
            '-PlanOnly')
        if ($InstanceConfigPath) {
            $arguments += @('-InstanceConfigPath', $InstanceConfigPath)
        }

        $output = & powershell.exe @arguments 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw ("Launcher exited with {0}: {1}" -f $LASTEXITCODE, ($output -join ' '))
        }

        # Seul le document JSON est lu : l'import du module peut ecrire un
        # avertissement (verbes non approuves) sur la sortie de l'enfant.
        $lines = @($output | ForEach-Object { [string]$_ })
        $start = [array]::FindIndex($lines, [Predicate[string]] { param($line) $line.StartsWith('{') })
        if ($start -lt 0) {
            throw ("Launcher printed no plan: {0}" -f ($lines -join ' '))
        }
        ($lines[$start..($lines.Count - 1)] -join "`n") | ConvertFrom-Json
    }
    finally {
        foreach ($name in $saved.Keys) {
            [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process')
        }
    }
}

Describe 'Test-KoxoExportPayload : namespace des identifiants' {
    function New-Payload([string]$Identifier) {
        [pscustomobject]@{
            schemaVersion = 3
            generatedAt = '2026-09-26T08:00:00.0000000Z'
            userCount = 1
            users = @(
                [pscustomobject]@{
                    civilite = 'Mme'
                    nom = 'Hounsa'
                    prenom = 'Zoe'
                    dateNaissance = '1994-03-22'
                    identifiantUnique = $Identifier
                    groupeSecondaire = 'DEV-CLI-ABCDEF'
                    email = 'zoe.hounsa@example.invalid'
                    qualitesSupplementaires = ''
                    groupePrimaire = 'CLIENTS DEV'
                }
            )
        }
    }

    It 'keeps the production rule and message by default' {
        (Test-KoxoExportPayload -Payload (New-Payload 'CLI-000001')).IsValid | Should Be $true
        $refus = Test-KoxoExportPayload -Payload (New-Payload 'CLI-D000001')
        $refus.IsValid | Should Be $false
        ($refus.Errors | Where-Object Field -eq 'identifiantUnique').Message | Should Be 'identifiantUnique must match CLI-000000.'
    }

    It 'accepts only its own namespace in a DEV instance' {
        (Test-KoxoExportPayload -Payload (New-Payload 'CLI-D000001') -IdentifierPrefix 'CLI-D').IsValid | Should Be $true
        (Test-KoxoExportPayload -Payload (New-Payload 'CLI-000001') -IdentifierPrefix 'CLI-D').IsValid | Should Be $false
        (Test-KoxoExportPayload -Payload (New-Payload 'CLI-D00001') -IdentifierPrefix 'CLI-D').IsValid | Should Be $false
    }

    It 'refuses a malformed prefix' {
        { Test-KoxoIdentifierPrefix -IdentifierPrefix 'CLI-1' } | Should Throw 'KOXO_IDENTIFIER_PREFIX is invalid'
        { Test-KoxoIdentifierPrefix -IdentifierPrefix 'cli-' } | Should Throw 'KOXO_IDENTIFIER_PREFIX is invalid'
        { Test-KoxoIdentifierPrefix -IdentifierPrefix '' } | Should Throw 'KOXO_IDENTIFIER_PREFIX is invalid'
    }
}

Describe 'Resolve-KoxoSyncLaunchPlan : instance de production (compatibilite)' {
    $machine = @{
        KOXO_API_URL = 'https://prod.example.invalid/api/internal/koxo/users'
        KOXO_API_TOKEN = (New-KoxoFakeSecret)
        KOXO_CSV_ENCODING = 'utf8bom'
        KOXO_MAX_USER_DROP_PERCENT = '20'
    }
    $reader = { param($Name) $machine[$Name] }.GetNewClosure()

    It 'reloads exactly the non-empty Machine settings and neutralises KOXO_OTHER_CSV_PATHS' {
        $plan = Resolve-KoxoSyncLaunchPlan -MachineSettingReader $reader
        $plan.Mode | Should Be 'production'
        $plan.InstanceName | Should Be 'prod'
        @($plan.ProcessEnvironment.Keys).Count | Should Be 5
        $plan.ProcessEnvironment['KOXO_API_URL'] | Should Be $machine.KOXO_API_URL
        $plan.ProcessEnvironment['KOXO_API_TOKEN'] | Should Be $machine.KOXO_API_TOKEN
        $plan.ProcessEnvironment['KOXO_CSV_ENCODING'] | Should Be 'utf8bom'
        $plan.ProcessEnvironment['KOXO_MAX_USER_DROP_PERCENT'] | Should Be '20'
        $plan.ProcessEnvironment['KOXO_OTHER_CSV_PATHS'] | Should Be ''
        $plan.ProcessEnvironment.Contains('KOXO_MIN_USER_COUNT') | Should Be $false
        $plan.Overrides.Count | Should Be 0
    }

    It 'keeps the two historical profiles, CSV and KoXo profile files' {
        $plan = Resolve-KoxoSyncLaunchPlan -MachineSettingReader $reader
        $plan.Profiles.Count | Should Be 2
        $plan.Profiles[0].PrimaryGroup | Should BeExactly 'CLIENTS'
        $plan.Profiles[0].CsvTargetPath | Should Be 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients.csv'
        $plan.Profiles[0].KoxoSyncArgument | Should Be '/Synchro=CLIENTS.xml'
        $plan.Profiles[1].PrimaryGroup | Should BeExactly $script:PrimaryGroupDemo
        $plan.Profiles[1].CsvTargetPath | Should Be 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients-demo.csv'
        $plan.Profiles[1].KoxoSyncArgument | Should Be '/Synchro=CLIENTS-DEMO.xml'
        $plan.WorkingDirectory | Should Be 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\work'
        $plan.KoxoExecutablePath | Should Be 'C:\Program Files\KoXo Dev\KoXoAdm\KoXoAdm.exe'
    }

    It 'still resolves the URL, prefix and profiles through the launcher without instance' {
        $url = 'https://prod.example.invalid/api/internal/koxo/users'
        $plan = Invoke-KoxoLauncherPlan -Environment @{ KOXO_API_URL = $url; KOXO_API_TOKEN = (New-KoxoFakeSecret) }
        $plan.Mode | Should Be 'production'
        $plan.ApiUrl | Should Be $url
        $plan.ApiTokenPresent | Should Be $true
        $plan.IdentifierPrefix | Should Be 'CLI-'
        $plan.OtherCsvPaths | Should Be ''
        @($plan.Profiles | ForEach-Object { $_.CsvTargetPath -replace '^.*\\', '' }) -join ',' | Should Be 'clients.csv,clients-demo.csv'
        $plan.AdmLockName | Should Be 'Global\Kermaria-KoXoAdm'
    }

    It 'keeps the production defaults of the sync configuration' {
        $work = Join-Path $env:TEMP ('koxo-conf-' + [guid]::NewGuid().ToString('N'))
        $configuration = Get-KoxoSyncConfiguration -CsvTargetPath (Join-Path $work 'clients.csv') -WorkingDirectory $work -Overrides @{
            KOXO_API_URL = 'https://prod.example.invalid/x'
            KOXO_API_TOKEN = (New-KoxoFakeSecret)
        }
        $configuration.IdentifierPrefix | Should Be 'CLI-'
        $configuration.InstanceName | Should Be 'prod'
        $configuration.AdmLockTimeoutSeconds | Should Be 600
        $configuration.LockPath | Should Be (Join-Path (Join-Path $work 'logs') 'koxo-sync.lock')
    }
}

Describe 'Resolve-KoxoSyncLaunchPlan : instance isolee' {
    It 'takes every setting from its definition, never from the Machine variables' {
        $fixture = New-KoxoInstanceFixture
        $prodReader = { param($Name) @{ KOXO_API_URL = 'https://prod.example.invalid/api/internal/koxo/users'; KOXO_API_TOKEN = 'another-value' }[$Name] }
        $plan = Resolve-KoxoSyncLaunchPlan -InstanceConfigPath $fixture.Path -MachineSettingReader $prodReader
        $plan.Mode | Should Be 'instance'
        $plan.InstanceName | Should Be 'dev'
        $plan.Overrides.KOXO_API_URL | Should Be 'https://dev.example.invalid/api/internal/koxo/users'
        $plan.Overrides.KOXO_API_TOKEN | Should Be $fixture.ApiToken
        $plan.Overrides.KOXO_IDENTIFIER_PREFIX | Should Be 'CLI-D'
        $plan.Overrides.KOXO_OTHER_CSV_PATHS | Should Be ''
        $plan.Overrides.KOXO_ALLOW_USER_DROP | Should Be 'false'
        $plan.Overrides.KOXO_ALLOW_EMPTY_CSV | Should Be 'false'
        foreach ($name in @('KOXO_API_URL', 'KOXO_API_TOKEN', 'KOXO_LOG_DIRECTORY', 'KOXO_OTHER_CSV_PATHS', 'KOXO_IDENTIFIER_PREFIX')) {
            $plan.ProcessEnvironment.Contains($name) | Should Be $true
            $plan.ProcessEnvironment[$name] | Should BeNullOrEmpty
        }
        $plan.Profiles.Count | Should Be 1
        $plan.Profiles[0].PrimaryGroup | Should Be 'CLIENTS DEV'
    }

    It 'is not overridden by production variables inherited by the launcher process' {
        $fixture = New-KoxoInstanceFixture
        $plan = Invoke-KoxoLauncherPlan -InstanceConfigPath $fixture.Path -Environment @{
            KOXO_API_URL = 'https://prod.example.invalid/api/internal/koxo/users'
            KOXO_API_TOKEN = (New-KoxoFakeSecret)
            KOXO_LOG_DIRECTORY = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\Logs'
            KOXO_OTHER_CSV_PATHS = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients-demo.csv'
            KOXO_IDENTIFIER_PREFIX = 'CLI-'
        }
        $plan.Mode | Should Be 'instance'
        $plan.ApiUrl | Should Be 'https://dev.example.invalid/api/internal/koxo/users'
        $plan.IdentifierPrefix | Should Be 'CLI-D'
        $plan.LogDirectory | Should Be (Join-Path $fixture.Root 'Logs')
        $plan.OtherCsvPaths | Should Be ''
        $plan.Profiles[0].CsvTargetPath | Should Be (Join-Path $fixture.Root 'clients-dev.csv')
        $plan.AdmLockName | Should Be 'Global\Kermaria-KoXoAdm'
    }

    It 'never prints the tokens in its plan' {
        $fixture = New-KoxoInstanceFixture
        $plan = Invoke-KoxoLauncherPlan -InstanceConfigPath $fixture.Path
        $json = $plan | ConvertTo-Json -Depth 5
        $json.Contains($fixture.ApiToken) | Should Be $false
        $json.Contains($fixture.WebhookToken) | Should Be $false
        $plan.ApiTokenPresent | Should Be $true
    }

    It 'refuses the production URL or token read on the machine' {
        $fixture = New-KoxoInstanceFixture
        $sameUrl = { param($Name) if ($Name -eq 'KOXO_API_URL') { 'https://DEV.example.invalid/api/internal/koxo/users/' } }
        { Get-KoxoInstanceDefinition -InstanceConfigPath $fixture.Path -MachineSettingReader $sameUrl } | Should Throw 'production KOXO_API_URL'

        $token = $fixture.ApiToken
        $sameToken = { param($Name) if ($Name -eq 'KOXO_API_TOKEN') { $token } }.GetNewClosure()
        $message = $null
        try { Get-KoxoInstanceDefinition -InstanceConfigPath $fixture.Path -MachineSettingReader $sameToken } catch { $message = $_.Exception.Message }
        $message | Should Match 'production KOXO_API_TOKEN'
        $message.Contains($token) | Should Be $false
    }

    $noMachine = { param($Name) $null }
    $cases = @(
        @{ Name = 'production primary group'; Changes = @{ profiles = @(@{ primaryGroup = 'clients'; csvTargetPath = 'C:\koxo-dev\clients-dev.csv'; koxoSyncArgument = '/Synchro=CLIENTS-DEV.xml' }) }; Expected = 'belongs to the production instance' },
        @{ Name = 'production demo group'; Changes = @{ profiles = @(@{ primaryGroup = $script:PrimaryGroupDemo; csvTargetPath = 'C:\koxo-dev\clients-dev.csv'; koxoSyncArgument = '/Synchro=CLIENTS-DEV.xml' }) }; Expected = 'belongs to the production instance' },
        @{ Name = 'production CSV'; Changes = @{ profiles = @(@{ primaryGroup = 'CLIENTS DEV'; csvTargetPath = 'C:\koxo-dev\clients.csv'; koxoSyncArgument = '/Synchro=CLIENTS-DEV.xml' }) }; Expected = 'belongs to the production instance' },
        @{ Name = 'production KoXo profile file'; Changes = @{ profiles = @(@{ primaryGroup = 'CLIENTS DEV'; csvTargetPath = 'C:\koxo-dev\clients-dev.csv'; koxoSyncArgument = '/Synchro=CLIENTS.xml' }) }; Expected = 'belongs to the production instance' },
        @{ Name = 'production identifier prefix'; Changes = @{ identifierPrefix = 'CLI-' }; Expected = 'must differ from the production prefix' },
        @{ Name = 'production working directory'; Changes = @{ workingDirectory = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\work' }; Expected = 'belongs to the production instance' },
        @{ Name = 'production log directory'; Changes = @{ logDirectory = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\Logs\dev' }; Expected = 'belongs to the production instance' },
        @{ Name = 'production receiver port'; Changes = @{ receiver = @{ prefix = 'http://+:8042/internal/koxo/sync/'; tokenPath = 'C:\koxo-dev\t.txt'; logDirectory = 'C:\koxo-dev\Logs' } }; Expected = 'production port 8042' },
        @{ Name = 'instance named prod'; Changes = @{ instanceName = 'prod' }; Expected = 'instanceName' },
        @{ Name = 'relative path'; Changes = @{ workingDirectory = 'work' }; Expected = 'workingDirectory must be an absolute path' },
        @{ Name = 'no profile'; Changes = @{ profiles = @() }; Expected = 'At least one profile' }
    )

    foreach ($case in $cases) {
        It ("refuses a definition with a {0}" -f $case.Name) {
            $fixture = New-KoxoInstanceFixture -Changes $case.Changes
            { Get-KoxoInstanceDefinition -InstanceConfigPath $fixture.Path -MachineSettingReader $noMachine } | Should Throw $case.Expected
        }
    }
}

Describe 'Recepteur : instance isolee' {
    $receiver = Join-Path $koxoRoot 'Start-KoxoSyncWebhookReceiver.ps1'
    $instanceCmd = Join-Path $koxoRoot 'Start-KoxoSyncWebhookReceiver-Instance.cmd'

    It 'keeps the production defaults of the receiver' {
        $source = Get-Content -LiteralPath $receiver -Raw
        $source | Should Match "\[string\]\`$Prefix = 'http://\+:8041/internal/koxo/sync/'"
        $source | Should Match "\[string\]\`$InstanceConfigPath = ''"
        $source | Should Match '\$storageRouteEnabled = \$true'
    }

    It 'forwards the instance definition to the sync launcher' {
        (Get-Content -LiteralPath $receiver -Raw) | Should Match '-InstanceConfigPath "\{0\}"'
    }

    It 'starts an instance from its definition without any token on the command line' {
        $source = Get-Content -LiteralPath $instanceCmd -Raw
        $source | Should Match "-InstanceConfigPath '%~1'"
        $source | Should Not Match '-Token'
        $source | Should Not Match 'koxo-webhook-token\.txt'
    }

    It 'really reaches the definition when started through its launcher' {
        # Execution reelle du .cmd, pas une lecture de sa source : sous -File,
        # Windows PowerShell 5.1 n'evaluait pas $PSScriptRoot dans les valeurs
        # par defaut du receveur, qui echouait sur Join-Path avant meme de lire
        # sa definition (constate sur SRV-21 le 2026-09-26). Une definition
        # absente doit donc etre la PREMIERE erreur rencontree.
        $missing = Join-Path $env:TEMP ('koxo-absente-' + [guid]::NewGuid().ToString('N') + '.json')
        $output = (& cmd.exe /c $instanceCmd $missing 2>&1 | ForEach-Object { "$_" }) -join ' '
        $output | Should Match 'KoXo instance definition not found'
        $output | Should Not Match 'Join-Path'
    }

    It 'closes the storage route unless the definition opens it' {
        $fixture = New-KoxoInstanceFixture
        (Get-KoxoInstanceDefinition -InstanceConfigPath $fixture.Path -MachineSettingReader { param($Name) $null }).Receiver.StorageRouteEnabled | Should Be $false
    }

    It 'ships a DEV example without any secret value' {
        $example = Join-Path $koxoRoot 'instances\koxo-instance.dev.example.json'
        $json = Get-Content -LiteralPath $example -Raw | ConvertFrom-Json
        $json.identifierPrefix | Should Be 'CLI-D'
        $json.profiles[0].primaryGroup | Should Be 'CLIENTS DEV'
        $json.profiles[0].csvTargetPath | Should Match 'clients-dev\.csv$'
        $json.receiver.prefix | Should Match ':8043/'
        $json.receiver.storageRouteEnabled | Should Be $false
        @($json.PSObject.Properties.Name) -contains 'apiToken' | Should Be $false
        $json.apiTokenPath | Should Match '\.txt$'
    }
}

Describe 'Instance storage-only isolation' {
    function New-StorageOnlyFixture {
        $fixture = New-KoxoInstanceFixture
        $fixture.Definition.apiTokenPath = Join-Path $fixture.Root 'unavailable-export-token.txt'
        $fixture.Definition.receiver.storageRouteEnabled = $true
        $fixture.Definition.receiver.storage = @{
            dataRoot = (Join-Path $fixture.Root 'Data')
            fsrmEnabled = $true
            fsrmServer = 'fsrm.test.invalid'
            fsrmUserPathTemplate = 'F:\KoXoDATA\{primaryGroup}\{secondaryGroup}\{userId}'
        }
        [IO.File]::WriteAllText($fixture.Path, ($fixture.Definition | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
        $fixture
    }
    It 'starts storage configuration without reading the CSV export secret' {
        $fixture = New-StorageOnlyFixture
        $definition = Get-KoxoInstanceDefinition -InstanceConfigPath $fixture.Path -StorageOnly -MachineSettingReader {
            param($Name)
            if ($Name -eq 'KOXO_API_TOKEN') { throw 'Storage must not read the production export secret' }
            $null
        }
        $definition.ApiToken | Should BeNullOrEmpty
        $definition.Receiver.Storage.FsrmEnabled | Should Be $true
        $definition.Receiver.Storage.FsrmServer | Should Be 'fsrm.test.invalid'
        $definition.Profiles[0].PrimaryGroup | Should Be 'CLIENTS DEV'
    }
    It 'still requires the export secret for the normal sync mode' {
        $fixture = New-StorageOnlyFixture
        { Get-KoxoInstanceDefinition -InstanceConfigPath $fixture.Path -MachineSettingReader {param($Name) $null} } | Should Throw 'apiTokenPath does not exist'
    }
    It 'refuses a disabled storage route' {
        $fixture = New-StorageOnlyFixture
        $fixture.Definition.receiver.storageRouteEnabled = $false
        [IO.File]::WriteAllText($fixture.Path, ($fixture.Definition | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
        { Get-KoxoInstanceDefinition -InstanceConfigPath $fixture.Path -StorageOnly -MachineSettingReader {param($Name) $null} } | Should Throw 'enabled receiver.storage'
    }
    It 'refuses XML-only verification and string boolean ambiguity' {
        foreach ($flag in @($false, 'false')) {
            $fixture = New-StorageOnlyFixture
            $fixture.Definition.receiver.storage.fsrmEnabled = $flag
            [IO.File]::WriteAllText($fixture.Path, ($fixture.Definition | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
            { Get-KoxoInstanceDefinition -InstanceConfigPath $fixture.Path -StorageOnly -MachineSettingReader {param($Name) $null} } | Should Throw 'effective FSRM'
        }
    }
    It 'refuses missing server and path instead of inheriting machine settings' {
        foreach ($key in @('fsrmServer', 'fsrmUserPathTemplate')) {
            $fixture = New-StorageOnlyFixture
            $fixture.Definition.receiver.storage[$key] = ''
            [IO.File]::WriteAllText($fixture.Path, ($fixture.Definition | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
            { Get-KoxoInstanceDefinition -InstanceConfigPath $fixture.Path -StorageOnly -MachineSettingReader {param($Name) 'must-not-be-used'} } | Should Throw 'effective FSRM'
        }
    }
    It 'refuses storage-only startup without an isolated definition' {
        $receiver = Join-Path $koxoRoot 'Start-KoxoSyncWebhookReceiver.ps1'
        try {
            { & $receiver -StorageOnly } | Should Throw 'isolated instance definition'
        }
        finally {
            Import-Module $modulePath -Force -DisableNameChecking
        }
    }
}

Describe 'Verrou systeme KoXoAdm' {
    $lockName = 'Global\Kermaria-KoXoAdm-Test-' + [guid]::NewGuid().ToString('N')
    $module = Get-Module KoxoSync.Common
    & $module { param($Name) $script:KoxoAdmLockName = $Name } $lockName

    # Enfant qui prend le verrou et le garde : c'est un AUTRE processus, comme
    # le serait l'instance PROD face a la DEV.
    function Start-KoxoLockHolder([string]$ReadyPath, [int]$HoldSeconds = 30) {
        $script = @"
`$m = New-Object System.Threading.Mutex(`$false, '$lockName')
[void]`$m.WaitOne()
[System.IO.File]::WriteAllText('$ReadyPath', 'ready')
Start-Sleep -Seconds $HoldSeconds
"@
        $process = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-NonInteractive', '-Command', $script) -WindowStyle Hidden -PassThru
        $deadline = (Get-Date).AddSeconds(30)
        while (-not (Test-Path -LiteralPath $ReadyPath) -and (Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 100
        }
        if (-not (Test-Path -LiteralPath $ReadyPath)) {
            throw 'lock holder did not start'
        }
        $process
    }

    function Test-LockFreeFromAnotherProcess {
        $script = "`$m = New-Object System.Threading.Mutex(`$false, '$lockName'); try { if (`$m.WaitOne(2000)) { 'free'; `$m.ReleaseMutex() } else { 'held' } } catch [System.Threading.AbandonedMutexException] { 'abandoned' }"
        (& powershell.exe -NoProfile -NonInteractive -Command $script) -join ''
    }

    function New-LockConfiguration([int]$TimeoutSeconds) {
        $directory = Join-Path $env:TEMP ('koxo-lock-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        [pscustomobject]@{
            LogPath = Join-Path $directory 'koxo-sync.log'
            SyncTimeoutSeconds = 30
            KoxoLogGlob = ''
            InstanceName = 'test'
            AdmLockTimeoutSeconds = $TimeoutSeconds
            Directory = $directory
        }
    }

    It 'times out with a clear diagnostic while another process holds it' {
        $ready = Join-Path $env:TEMP ('koxo-lock-ready-' + [guid]::NewGuid().ToString('N'))
        $holder = Start-KoxoLockHolder -ReadyPath $ready
        try {
            { Enter-KoxoAdmLock -TimeoutSeconds 1 -Holder 'dev' } | Should Throw 'KOXO_ADM_LOCK_TIMEOUT'
            try { Enter-KoxoAdmLock -TimeoutSeconds 1 -Holder 'dev' } catch { $_.Exception.Message | Should Match 'requesting instance: dev' }
        }
        finally {
            Stop-Process -Id $holder.Id -Force -ErrorAction SilentlyContinue
            $holder.WaitForExit()
        }
    }

    It 'is taken over when its previous holder died without releasing it' {
        $ready = Join-Path $env:TEMP ('koxo-lock-ready-' + [guid]::NewGuid().ToString('N'))
        $holder = Start-KoxoLockHolder -ReadyPath $ready
        # Un autre processus attend deja (poignee ouverte) : sans cela, le
        # mutex nomme disparaitrait avec son dernier detenteur et il n'y aurait
        # rien a reprendre.
        $waiter = New-Object System.Threading.Mutex($false, $lockName)
        try {
            Stop-Process -Id $holder.Id -Force
            $holder.WaitForExit()
            $handle = Enter-KoxoAdmLock -TimeoutSeconds 5
            try {
                $handle.Abandoned | Should Be $true
            }
            finally {
                Exit-KoxoAdmLock -LockHandle $handle
            }
        }
        finally {
            $waiter.Dispose()
        }
        Test-LockFreeFromAnotherProcess | Should Be 'free'
    }

    It 'never starts KoXoAdm while another instance holds the lock' {
        $ready = Join-Path $env:TEMP ('koxo-lock-ready-' + [guid]::NewGuid().ToString('N'))
        $configuration = New-LockConfiguration -TimeoutSeconds 1
        $marker = Join-Path $configuration.Directory 'koxoadm-started.txt'
        $holder = Start-KoxoLockHolder -ReadyPath $ready
        try {
            {
                Invoke-KoxoProcess -Configuration $configuration `
                    -ExecutablePath (Join-Path $env:SystemRoot 'System32\cmd.exe') `
                    -WorkingDirectory $configuration.Directory `
                    -Arguments ('/c echo started > "{0}"' -f $marker)
            } | Should Throw 'KOXO_ADM_LOCK_TIMEOUT'
            Test-Path -LiteralPath $marker | Should Be $false
            (Get-Content -LiteralPath $configuration.LogPath -Raw) | Should Match 'KOXO_ADM_LOCK_TIMEOUT'
        }
        finally {
            Stop-Process -Id $holder.Id -Force -ErrorAction SilentlyContinue
            $holder.WaitForExit()
        }
    }

    It 'releases the lock when the KoXo run fails' {
        $configuration = New-LockConfiguration -TimeoutSeconds 5
        {
            Invoke-KoxoProcess -Configuration $configuration `
                -ExecutablePath (Join-Path $configuration.Directory 'absent-KoXoAdm.exe') `
                -WorkingDirectory $configuration.Directory `
                -Arguments '/Synchro=CLIENTS-DEV.xml'
        } | Should Throw 'KoXo executable not found'
        # Un mutex est reentrant pour son proprietaire : seul un AUTRE processus
        # prouve qu'il a bien ete rendu.
        Test-LockFreeFromAnotherProcess | Should Be 'free'
    }

    It 'serialises two concurrent instances launching KoXoAdm' {
        $directory = Join-Path $env:TEMP ('koxo-lock-race-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        $trace = Join-Path $directory 'trace.txt'
        # Faux KoXoAdm : note son entree et sa sortie, et dure deux secondes.
        $fake = Join-Path $directory 'fake-koxoadm.ps1'
        [System.IO.File]::WriteAllText($fake, @'
param([string]$Trace, [string]$Name)
$stream = $null
function Write-Trace([string]$Line) {
    for ($i = 0; $i -lt 50; $i++) {
        try { [System.IO.File]::AppendAllText($Trace, $Line + "`r`n"); return } catch { Start-Sleep -Milliseconds 20 }
    }
}
Write-Trace ("start {0} {1}" -f $Name, [DateTime]::UtcNow.Ticks)
Start-Sleep -Seconds 2
Write-Trace ("end {0} {1}" -f $Name, [DateTime]::UtcNow.Ticks)
'@)

        $runner = Join-Path $directory 'runner.ps1'
        [System.IO.File]::WriteAllText($runner, @"
param([string]`$Name)
Import-Module '$modulePath' -Force -DisableNameChecking
& (Get-Module KoxoSync.Common) { param(`$n) `$script:KoxoAdmLockName = `$n } '$lockName'
`$configuration = [pscustomobject]@{
    LogPath = Join-Path '$directory' ("koxo-sync-{0}.log" -f `$Name)
    SyncTimeoutSeconds = 30
    KoxoLogGlob = ''
    InstanceName = `$Name
    AdmLockTimeoutSeconds = 60
}
Invoke-KoxoProcess -Configuration `$configuration ``
    -ExecutablePath (Join-Path `$PSHOME 'powershell.exe') ``
    -WorkingDirectory '$directory' ``
    -Arguments ('-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$fake" -Trace "$trace" -Name {0}' -f `$Name) | Out-Null
"@)

        $processes = @('prod', 'dev') | ForEach-Object {
            Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $runner, '-Name', $_) -WindowStyle Hidden -PassThru
        }
        foreach ($process in $processes) {
            $process.WaitForExit(90000) | Should Be $true
            $process.ExitCode | Should Be 0
        }

        $events = @(Get-Content -LiteralPath $trace | ForEach-Object {
            $parts = $_ -split ' '
            [pscustomobject]@{ Kind = $parts[0]; Name = $parts[1]; Ticks = [long]$parts[2] }
        } | Sort-Object Ticks)
        $events.Count | Should Be 4
        # Serialise : chaque entree est suivie de SA sortie avant toute autre entree.
        $events[0].Kind | Should Be 'start'
        $events[1].Kind | Should Be 'end'
        $events[1].Name | Should Be $events[0].Name
        $events[2].Kind | Should Be 'start'
        $events[3].Kind | Should Be 'end'
        $events[3].Name | Should Be $events[2].Name
        $events[0].Name | Should Not Be $events[2].Name
    }
}
