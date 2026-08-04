$modulePath = Join-Path (Split-Path -Parent $PSScriptRoot) 'KoxoSync.Common.psm1'
Import-Module $modulePath -Force

function New-KoxoTestPayload {
    param(
        [string]$Identifier = 'CLI-000001'
    )

    [pscustomobject]@{
        schemaVersion = 1
        generatedAt = '2026-07-30T08:00:00.0000000Z'
        userCount = 1
        users = @(
            [pscustomobject]@{
                civilite = 'Mme'
                nom = 'Hounsa'
                prenom = 'Zoe'
                dateNaissance = '1994-03-22'
                identifiantUnique = $Identifier
                groupeSecondaire = 'CLI-DEMO-0042'
                email = 'zoe.hounsa@example.invalid'
            }
        )
    }
}

Describe 'Test-KoxoExportPayload' {
    It 'validates the expected JSON contract' {
        $result = Test-KoxoExportPayload -Payload (New-KoxoTestPayload)
        $result.IsValid | Should Be $true
        $result.Errors.Count | Should Be 0
    }

    It 'rejects invalid payloads globally' {
        $payload = New-KoxoTestPayload
        $payload.userCount = 2
        $payload.users[0].dateNaissance = '22/03/1994'
        $payload.users[0].civilite = 'Autre'
        $result = Test-KoxoExportPayload -Payload $payload
        $result.IsValid | Should Be $false
        @($result.Errors | Where-Object { $_.Field -eq 'userCount' }).Count | Should Be 1
        @($result.Errors | Where-Object { $_.Field -eq 'dateNaissance' }).Count | Should Be 1
        @($result.Errors | Where-Object { $_.Field -eq 'civilite' }).Count | Should Be 1
    }
}

Describe 'ConvertTo-KoxoCsvContent' {
    It 'generates 13 semicolon-separated columns' {
        $content = ConvertTo-KoxoCsvContent -Users (New-KoxoTestPayload).users
        ($content -split "`r`n")[0] | Should Be 'Civilite;Nom;Prenom;DateNaissance;IdentifiantUnique;GroupeSecondaire;Email;Telephone;TelephoneMobile;Fax;PageWeb;ChampLibre;Fonction'
        $root = Join-Path $env:TEMP ('koxo-csv-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        $path = Join-Path $root 'users.csv'
        Write-KoxoTextFile -Path $path -Content $content -EncodingName 'utf8'
        { Test-KoxoCsvFile -Path $path } | Should Not Throw
    }

    It 'preserves accents, quotes, and separators through escaping' {
        $payload = New-KoxoTestPayload
        $payload.users[0].nom = 'Le "Grand"; Hôtel'
        $payload.users[0].prenom = 'Élise'
        $content = ConvertTo-KoxoCsvContent -Users $payload.users
        $content | Should Match '""Grand""; Hôtel'

        $root = Join-Path $env:TEMP ('koxo-encoding-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        $path = Join-Path $root 'users.csv'
        Write-KoxoTextFile -Path $path -Content $content -EncodingName 'unicode'
        { Test-KoxoCsvFile -Path $path } | Should Not Throw
        ([System.IO.File]::ReadAllBytes($path)[0]) | Should Be 255
    }
}

Describe 'Encodage du CSV' {
    # Construit depuis les points de code : ce fichier de test est lui-meme lu
    # avec l'encodage de la console, un litteral accentue ne prouverait rien.
    $accentedSurname = 'LAUMAILL' + [char]0x00C9

    It 'defaults to utf8bom so KoXo never relit le fichier en ANSI' {
        $root = Join-Path $env:TEMP ('koxo-default-encoding-' + [guid]::NewGuid().ToString('N'))
        $configuration = Get-KoxoSyncConfiguration `
            -CsvTargetPath (Join-Path $root 'users.csv') `
            -WorkingDirectory (Join-Path $root 'work') `
            -Overrides @{
                KOXO_API_URL = 'https://localhost/api/internal/koxo/users'
                KOXO_API_TOKEN = 'LOCAL-TEST-TOKEN'
                KOXO_CSV_ENCODING = ''
                KOXO_LOG_DIRECTORY = (Join-Path $root 'logs')
            }

        $configuration.CsvEncoding | Should Be 'utf8bom'
    }

    It 'keeps accented capitals byte-for-byte in utf8bom' {
        $root = Join-Path $env:TEMP ('koxo-accents-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        $path = Join-Path $root 'users.csv'

        Write-KoxoTextFile -Path $path -Content $accentedSurname -EncodingName 'utf8bom'

        $bytes = [System.IO.File]::ReadAllBytes($path)
        $bytes[0] | Should Be 239
        $bytes[1] | Should Be 187
        $bytes[2] | Should Be 191
        $bytes[$bytes.Length - 2] | Should Be 195
        $bytes[$bytes.Length - 1] | Should Be 137
        [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8) | Should Be $accentedSurname
    }

    It 'refuses an encoding that cannot represent the content' {
        $root = Join-Path $env:TEMP ('koxo-lossy-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        $path = Join-Path $root 'users.csv'

        { Write-KoxoTextFile -Path $path -Content $accentedSurname -EncodingName 'ascii' } |
            Should Throw
    }

    It 'names the characters lost by a narrower encoding' {
        # OE ligature : absente de l'ISO-8859-1, donc silencieusement remplacee.
        $lost = Get-KoxoLostCharacters -Source ('C' + [char]0x0152 + 'UR') -RoundTrip 'C?UR'
        $lost | Should Match ([regex]::Escape([string][char]0x0152))
    }
}

Describe 'Deploy-KoxoScripts' {
    $deployScript = Join-Path (Split-Path -Parent $PSScriptRoot) 'Deploy-KoxoScripts.ps1'

    It 'lists a manifest without contacting the target' {
        $manifest = @(& $deployScript -ListOnly)
        $manifest.Count | Should BeGreaterThan 0
        foreach ($item in $manifest) {
            Test-Path -LiteralPath $item.SourcePath | Should Be $true
            $item.Sha256.Length | Should Be 64
        }
    }

    It 'never lists a file owned by the server or by KoXo' {
        $names = @(& $deployScript -ListOnly | ForEach-Object { $_.Name })
        foreach ($protected in @('CLIENTS.xml', 'clients.csv', 'koxo-webhook-token.txt')) {
            $names -contains $protected | Should Be $false
        }
    }

    It 'ships the sync module and its entry points' {
        $names = @(& $deployScript -ListOnly | ForEach-Object { $_.Name })
        foreach ($expected in @('KoxoSync.Common.psm1', 'Sync-KoXoClients.ps1', 'Invoke-KoxoSyncFromWebhook.ps1')) {
            $names -contains $expected | Should Be $true
        }
    }

    It 'refuses a manifest that would overwrite a protected name' {
        { & $deployScript -ListOnly -Include @('CLIENTS.xml') } | Should Throw
        { & $deployScript -ListOnly -Include @('koxo-webhook-token.txt') } | Should Throw
    }

    It 'refuses a manifest naming a file absent from the repository' {
        { & $deployScript -ListOnly -Include @('Absent-DuDepot.ps1') } | Should Throw
    }

    It 'ships the webhook launcher' {
        $names = @(& $deployScript -ListOnly | ForEach-Object { $_.Name })
        $names -contains 'Start-KoxoSyncWebhookReceiver-8042.cmd' | Should Be $true
    }

    It 'counts syntax errors only for PowerShell files' {
        # `@($null).Count` vaut 1 : compter sans avoir analyse declarait fautif
        # tout fichier non PowerShell, et le deploiement echouait sur le `.cmd`
        # apres l'avoir deja copie.
        $koxoRoot = Split-Path -Parent $PSScriptRoot
        # Sourcer le script y importe ses fonctions, mais `$PSScriptRoot` y
        # devient le dossier de ce fichier de test : passer la racine reelle.
        . $deployScript -ListOnly -SourcePath $koxoRoot | Out-Null

        Get-KoxoSyntaxErrorCount -Path (Join-Path $koxoRoot 'Start-KoxoSyncWebhookReceiver-8042.cmd') | Should Be 0
        Get-KoxoSyntaxErrorCount -Path (Join-Path $koxoRoot 'KoxoSync.Common.psm1') | Should Be 0

        $bad = Join-Path $env:TEMP ('koxo-mauvais-' + [guid]::NewGuid().ToString('N') + '.ps1')
        Set-Content -LiteralPath $bad -Value 'function {' -Encoding UTF8
        Get-KoxoSyntaxErrorCount -Path $bad | Should BeGreaterThan 0
    }
}

Describe 'Start-KoxoSyncWebhookReceiver-8042.cmd' {
    $launcher = Join-Path (Split-Path -Parent $PSScriptRoot) 'Start-KoxoSyncWebhookReceiver-8042.cmd'
    # Les commentaires du lanceur decrivent le bug d'echappement et citent le
    # chemin cible : les garde-fous ci-dessous portent sur les instructions.
    $commands = @(
        Get-Content -LiteralPath $launcher |
            Where-Object { $_.Trim() -notmatch '^rem\b' -and $_.Trim() -ne '' }
    ) -join "`n"

    It 'never escapes the PowerShell dollar sign' {
        # La version deployee sur SRV-21 portait « `$t » : `$ est un dollar
        # litteral, la variable n'etait jamais creee et le jeton transmis
        # valait la chaine « $t ». Le receveur ne pouvait pas demarrer.
        $commands.Contains('`$') | Should Be $false
    }

    It 'resolves its own directory instead of hard-coding the target path' {
        $commands | Should Match '%~dp0'
        $commands.Contains('C:\Program Files\KoXo Dev') | Should Be $false
    }

    It 'reads the token from the file rather than embedding it' {
        $commands | Should Match 'koxo-webhook-token\.txt'
        $commands | Should Match '\-Token \$t'
    }
}

Describe 'Invoke-KoxoSafeReplacement' {
    It 'replaces safely and keeps backups' {
        $root = Join-Path $env:TEMP ('koxo-replace-' + [guid]::NewGuid().ToString('N'))
        $backup = Join-Path $root 'backups'
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        $target = Join-Path $root 'users.csv'
        $temp = Join-Path $root 'users.csv.tmp'
        Set-Content -LiteralPath $target -Value 'old' -Encoding UTF8
        Set-Content -LiteralPath $temp -Value 'new' -Encoding UTF8

        $result = Invoke-KoxoSafeReplacement -TempPath $temp -TargetPath $target -BackupDirectory $backup -RetentionCount 2
        (Get-Content -LiteralPath $target -Raw) | Should Match 'new'
        $result.BackupPath | Should Not BeNullOrEmpty
        Test-Path -LiteralPath $result.BackupPath | Should Be $true
    }
}

Describe 'Acquire-KoxoFileLock' {
    It 'prevents concurrent runs' {
        $root = Join-Path $env:TEMP ('koxo-lock-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        $path = Join-Path $root 'sync.lock'
        $first = Acquire-KoxoFileLock -LockPath $path
        try {
            { Acquire-KoxoFileLock -LockPath $path } | Should Throw
        }
        finally {
            Release-KoxoFileLock -LockHandle $first
        }
    }
}

Describe 'Test-KoxoApiUrl' {
    It 'rejects insecure HTTP outside local execution by default' {
        { Test-KoxoApiUrl -ApiUrl 'http://172.16.90.1:3000/api/internal/koxo/users' } | Should Throw
    }

    It 'accepts insecure HTTP outside local execution only with explicit override' {
        { Test-KoxoApiUrl -ApiUrl 'http://172.16.90.1:3000/api/internal/koxo/users' -AllowInsecureHttp } | Should Not Throw
    }
}

Describe 'Test-KoxoLogOutcome' {
    It 'accepts a recent KoXo log with accepted parameter and end marker' {
        $root = Join-Path $env:TEMP ('koxo-log-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        $logPath = Join-Path $root 'CLIENTS-20260730.log'
        @'
Parametre accepte : /Synchro=CLIENTS.xml
Ajout/Modification de l'utilisateur
Fin de l'operation
'@ | Set-Content -LiteralPath $logPath -Encoding UTF8

        $result = Test-KoxoLogOutcome -GlobPattern (Join-Path $root '*') -NotBeforeUtc (Get-Date).ToUniversalTime().AddMinutes(-1)
        $result.HasRecentLog | Should Be $true
        $result.IsSuccessful | Should Be $true
        $result.AcceptedMarker | Should Be $true
        $result.CompletionMarker | Should Be $true
        $result.BlockingError | Should Be $false
    }

}

Describe 'Invoke-KoxoSync' {
    It 'supports DryRun without touching the target file or logging the token' {
        $root = Join-Path $env:TEMP ('koxo-dryrun-' + [guid]::NewGuid().ToString('N'))
        $targetRoot = Join-Path $root 'target'
        $workRoot = Join-Path $root 'work'
        New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
        $target = Join-Path $targetRoot 'users.csv'
        $token = 'TOPSECRET-TOKEN-DO-NOT-LOG'

        $result = Invoke-KoxoSync `
            -CsvTargetPath $target `
            -WorkingDirectory $workRoot `
            -DryRun `
            -Overrides @{
                KOXO_API_URL = 'https://localhost/api/internal/koxo/users'
                KOXO_API_TOKEN = $token
                KOXO_ALLOW_INSECURE_HTTP = 'false'
                KOXO_CSV_ENCODING = 'utf8'
                KOXO_MIN_USER_COUNT = '0'
                KOXO_MAX_USER_DROP_PERCENT = '100'
                KOXO_SYNC_TIMEOUT_SECONDS = '10'
                KOXO_LOG_DIRECTORY = (Join-Path $workRoot 'logs')
                KOXO_KOXO_LOG_GLOB = ''
                KOXO_BACKUP_RETENTION_COUNT = '2'
            } `
            -PayloadObject (New-KoxoTestPayload)

        $result.Status | Should Be 'dry_run'
        Test-Path -LiteralPath $target | Should Be $false
        (Get-Content -LiteralPath $result.LogPath -Raw) | Should Not Match $token
    }

    It 'can launch a post-sync process when explicitly requested' {
        $root = Join-Path $env:TEMP ('koxo-launch-' + [guid]::NewGuid().ToString('N'))
        $targetRoot = Join-Path $root 'target'
        $workRoot = Join-Path $root 'work'
        New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
        $target = Join-Path $targetRoot 'users.csv'

        $result = Invoke-KoxoSync `
            -CsvTargetPath $target `
            -WorkingDirectory $workRoot `
            -LaunchKoxo `
            -KoxoExecutablePath $env:ComSpec `
            -KoxoWorkingDirectory $targetRoot `
            -KoxoSyncArgument '/c exit 0' `
            -Overrides @{
                KOXO_API_URL = 'https://localhost/api/internal/koxo/users'
                KOXO_API_TOKEN = 'LOCAL-TEST-TOKEN'
                KOXO_ALLOW_INSECURE_HTTP = 'false'
                KOXO_CSV_ENCODING = 'utf8'
                KOXO_MIN_USER_COUNT = '0'
                KOXO_MAX_USER_DROP_PERCENT = '100'
                KOXO_SYNC_TIMEOUT_SECONDS = '10'
                KOXO_LOG_DIRECTORY = (Join-Path $workRoot 'logs')
                KOXO_KOXO_LOG_GLOB = ''
                KOXO_BACKUP_RETENTION_COUNT = '2'
            } `
            -PayloadObject (New-KoxoTestPayload)

        $result.Status | Should Be 'synchronized_and_launched'
        $result.KoxoLaunch.Status | Should Be 'completed'
        $result.KoxoLaunch.ExitCode | Should Be 0
        Test-Path -LiteralPath $target | Should Be $true
    }

    It 'accepts a non-zero KoXo exit code when the recent KoXo log proves success' {
        $root = Join-Path $env:TEMP ('koxo-launch-log-' + [guid]::NewGuid().ToString('N'))
        $targetRoot = Join-Path $root 'target'
        $workRoot = Join-Path $root 'work'
        $koxoLogRoot = Join-Path $root 'koxo-logs'
        New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $koxoLogRoot -Force | Out-Null
        $target = Join-Path $targetRoot 'users.csv'
        $logPath = Join-Path $koxoLogRoot 'CLIENTS-20260730.log'
        @'
Parametre accepte : /Synchro=CLIENTS.xml
Ajout/Modification de l'utilisateur
Fin de l'operation
'@ | Set-Content -LiteralPath $logPath -Encoding UTF8

        $result = Invoke-KoxoSync `
            -CsvTargetPath $target `
            -WorkingDirectory $workRoot `
            -LaunchKoxo `
            -KoxoExecutablePath $env:ComSpec `
            -KoxoWorkingDirectory $targetRoot `
            -KoxoSyncArgument '/c exit 1' `
            -Overrides @{
                KOXO_API_URL = 'https://localhost/api/internal/koxo/users'
                KOXO_API_TOKEN = 'LOCAL-TEST-TOKEN'
                KOXO_ALLOW_INSECURE_HTTP = 'false'
                KOXO_CSV_ENCODING = 'utf8'
                KOXO_MIN_USER_COUNT = '0'
                KOXO_MAX_USER_DROP_PERCENT = '100'
                KOXO_SYNC_TIMEOUT_SECONDS = '10'
                KOXO_LOG_DIRECTORY = (Join-Path $workRoot 'logs')
                KOXO_KOXO_LOG_GLOB = (Join-Path $koxoLogRoot '*')
                KOXO_BACKUP_RETENTION_COUNT = '2'
            } `
            -PayloadObject (New-KoxoTestPayload)

        $result.Status | Should Be 'synchronized_and_launched'
        $result.KoxoLaunch.Status | Should Be 'completed_with_nonzero_exit'
        $result.KoxoLaunch.ExitCode | Should Be 1
        $result.KoxoLaunch.LogSuccessful | Should Be $true
        Test-Path -LiteralPath $target | Should Be $true
    }

    It 'accepts a KoXo timeout when the recent KoXo log proves success' {
        $root = Join-Path $env:TEMP ('koxo-launch-timeout-' + [guid]::NewGuid().ToString('N'))
        $targetRoot = Join-Path $root 'target'
        $workRoot = Join-Path $root 'work'
        $koxoLogRoot = Join-Path $root 'koxo-logs'
        New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $koxoLogRoot -Force | Out-Null
        $target = Join-Path $targetRoot 'users.csv'
        $logPath = Join-Path $koxoLogRoot 'CLIENTS-20260804.log'
        @'
Parametre accepte : /Synchro=CLIENTS.xml
Ajout/Modification de l'utilisateur
Fin de l'operation
'@ | Set-Content -LiteralPath $logPath -Encoding UTF8

        # KoXoAdm.exe peut finir son travail sans rendre la main : le faux
        # executable dort bien au-dela de KOXO_SYNC_TIMEOUT_SECONDS.
        $result = Invoke-KoxoSync `
            -CsvTargetPath $target `
            -WorkingDirectory $workRoot `
            -LaunchKoxo `
            -KoxoExecutablePath $env:ComSpec `
            -KoxoWorkingDirectory $targetRoot `
            -KoxoSyncArgument '/c ping -n 60 127.0.0.1 >nul' `
            -Overrides @{
                KOXO_API_URL = 'https://localhost/api/internal/koxo/users'
                KOXO_API_TOKEN = 'LOCAL-TEST-TOKEN'
                KOXO_ALLOW_INSECURE_HTTP = 'false'
                KOXO_CSV_ENCODING = 'utf8'
                KOXO_MIN_USER_COUNT = '0'
                KOXO_MAX_USER_DROP_PERCENT = '100'
                KOXO_SYNC_TIMEOUT_SECONDS = '5'
                KOXO_LOG_DIRECTORY = (Join-Path $workRoot 'logs')
                KOXO_KOXO_LOG_GLOB = (Join-Path $koxoLogRoot '*')
                KOXO_BACKUP_RETENTION_COUNT = '2'
            } `
            -PayloadObject (New-KoxoTestPayload)

        $result.Status | Should Be 'synchronized_and_launched'
        $result.KoxoLaunch.Status | Should Be 'completed_after_timeout'
        $result.KoxoLaunch.TimedOut | Should Be $true
        $result.KoxoLaunch.LogSuccessful | Should Be $true
        Test-Path -LiteralPath $target | Should Be $true
        (Get-Content -LiteralPath $result.LogPath -Raw) | Should Match '"level":"warning"'
    }

    It 'still fails on a KoXo timeout when no recent KoXo log proves success' {
        $root = Join-Path $env:TEMP ('koxo-launch-timeout-fail-' + [guid]::NewGuid().ToString('N'))
        $targetRoot = Join-Path $root 'target'
        $workRoot = Join-Path $root 'work'
        $koxoLogRoot = Join-Path $root 'koxo-logs'
        New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $koxoLogRoot -Force | Out-Null
        $target = Join-Path $targetRoot 'users.csv'

        {
            Invoke-KoxoSync `
                -CsvTargetPath $target `
                -WorkingDirectory $workRoot `
                -LaunchKoxo `
                -KoxoExecutablePath $env:ComSpec `
                -KoxoWorkingDirectory $targetRoot `
                -KoxoSyncArgument '/c ping -n 60 127.0.0.1 >nul' `
                -Overrides @{
                    KOXO_API_URL = 'https://localhost/api/internal/koxo/users'
                    KOXO_API_TOKEN = 'LOCAL-TEST-TOKEN'
                    KOXO_ALLOW_INSECURE_HTTP = 'false'
                    KOXO_CSV_ENCODING = 'utf8'
                    KOXO_MIN_USER_COUNT = '0'
                    KOXO_MAX_USER_DROP_PERCENT = '100'
                    KOXO_SYNC_TIMEOUT_SECONDS = '5'
                    KOXO_LOG_DIRECTORY = (Join-Path $workRoot 'logs')
                    KOXO_KOXO_LOG_GLOB = (Join-Path $koxoLogRoot '*')
                    KOXO_BACKUP_RETENTION_COUNT = '2'
                } `
                -PayloadObject (New-KoxoTestPayload)
        } | Should Throw 'KoXo process timed out after 5 seconds.'
    }
}
