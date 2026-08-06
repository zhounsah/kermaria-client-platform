<#
.SYNOPSIS
    Deploie les scripts KoXo du depot vers la machine de synchronisation.

.DESCRIPTION
    Le dossier cible melange trois choses qui n'ont pas le meme proprietaire :

    - les scripts, qui viennent du depot et peuvent etre ecrases ;
    - la configuration de KoXo (`CLIENTS.xml`) et le secret du webhook
      (`koxo-webhook-token.txt`), qui n'existent que sur le serveur ;
    - les donnees vivantes (`clients.csv`, `backups\`, `Logs\`, `work\`).

    Une copie en bloc detruirait les deux dernieres categories. Ce script
    deploie donc une liste explicite de fichiers, refuse par construction
    d'ecrire sur un nom protege, et signale ce qui traine sur la cible sans
    venir du depot.

    Il pose aussi les variables d'environnement Machine `KOXO_*`, parce
    qu'elles priment sur les defauts du module : deployer le module sans
    corriger la variable ne change rien au comportement.

.EXAMPLE
    .\Deploy-KoxoScripts.ps1 -DryRun

.EXAMPLE
    .\Deploy-KoxoScripts.ps1 -Settings @{ KOXO_CSV_ENCODING = 'utf8bom' } -RestartReceiver
#>
[CmdletBinding()]
param(
    [string]$ComputerName = 'KERMARIA-SRV-21.clients.home.bzh',

    [string]$DestinationPath = 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro',

    [string]$SourcePath = $PSScriptRoot,

    # Variables Machine `KOXO_*` a poser et verifier sur la cible.
    [hashtable]$Settings = @{},

    # Obligatoire pour qu'un changement de variable prenne effet : le receveur
    # garde son bloc d'environnement tant qu'il n'est pas relance.
    [switch]$RestartReceiver,

    [string]$ReceiverTaskName = 'Kermaria-KoXoWebhookReceiver-8042',

    # Saute la synchronisation `-DryRun` de validation finale.
    [switch]$SkipVerification,

    # N'ecrit rien : compare et rend le plan.
    [switch]$DryRun,

    # Restreint le deploiement a un sous-ensemble de la liste par defaut.
    [string[]]$Include,

    # Rend seulement le manifeste, sans contacter la cible.
    [switch]$ListOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Liste explicite : ajouter un script au depot ne le deploie pas tout seul.
$DeployableFiles = @(
    'KoxoSync.Common.psm1',
    'Sync-KoXoClients.ps1',
    'Invoke-KoxoSyncFromWebhook.ps1',
    'Start-KoxoSyncWebhookReceiver.ps1',
    'Install-KoxoSyncWebhookReceiverTask.ps1',
    'Install-KoXoScheduledTask.ps1',
    'Test-KoxoAccentHandling.ps1',
    'Start-KoxoSyncWebhookReceiver-8042.cmd'
)

# Jamais ecrasables : propriete du serveur ou de KoXo.
$ProtectedNames = @(
    'CLIENTS.xml',
    'CLIENTS-DEMO.xml',
    'clients.csv',
    'clients-demo.csv',
    'koxo-webhook-token.txt',
    'backups',
    'Logs',
    'work'
)

# Empreinte insensible aux fins de ligne et a la marque d'ordre d'octets :
# `.ps1` n'est pas couvert par `.gitattributes`, git rend donc du CRLF a la
# sortie alors que la cible peut porter du LF. Comparer les octets bruts
# ferait voir une derive permanente sur des fichiers rigoureusement
# identiques, et masquerait les vraies divergences dans le bruit.
$NormalizedHashBody = {
    param([string]$Path)
    $text = [System.IO.File]::ReadAllText($Path) -replace "`r`n", "`n"
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
        return (-join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }))
    }
    finally {
        $sha.Dispose()
    }
}

function Get-KoxoNormalizedHash {
    param([string]$Path)
    # Sans prefixe de portee : le script est appele normalement en exploitation
    # et source par les tests, et la recherche dynamique trouve la variable
    # dans les deux cas.
    & $NormalizedHashBody $Path
}

# Analyse syntaxique des seuls fichiers PowerShell. `@($null).Count` vaut 1 :
# compter sans avoir analyse declarerait fautif tout fichier non PowerShell.
$SyntaxErrorCountBody = {
    param([string]$Path)
    if ($Path -notmatch '\.psm?1$') {
        return 0
    }

    $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$null, [ref]$parseErrors)
    if ($null -eq $parseErrors) {
        return 0
    }

    return @($parseErrors).Count
}

function Get-KoxoSyntaxErrorCount {
    param([string]$Path)
    & $SyntaxErrorCountBody $Path
}

function Get-KoxoDeployManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string[]]$Names,
        [Parameter(Mandatory = $true)][string[]]$Protected
    )

    $collision = @($Names | Where-Object { $Protected -contains $_ })
    if ($collision.Count -gt 0) {
        throw ("Refus de deployer un nom protege : {0}." -f ($collision -join ', '))
    }

    foreach ($name in $Names) {
        $path = Join-Path $Root $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw ("Fichier source introuvable : {0}." -f $path)
        }

        [pscustomobject]@{
            Name = $name
            SourcePath = $path
            Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            NormalizedHash = (Get-KoxoNormalizedHash -Path $path)
            Length = (Get-Item -LiteralPath $path).Length
        }
    }
}

function Write-KoxoDeployLine {
    param([string]$Message)
    Write-Host $Message
}

$selectedFiles = $DeployableFiles
if ($PSBoundParameters.ContainsKey('Include')) {
    $selectedFiles = $Include
}

$manifest = @(Get-KoxoDeployManifest -Root $SourcePath -Names $selectedFiles -Protected $ProtectedNames)
$DeployableFiles = $selectedFiles

if ($ListOnly) {
    return $manifest
}

Write-KoxoDeployLine ("Cible      : {0}" -f $ComputerName)
Write-KoxoDeployLine ("Destination: {0}" -f $DestinationPath)
Write-KoxoDeployLine ("Fichiers   : {0}" -f $manifest.Count)
if ($DryRun) {
    Write-KoxoDeployLine 'Mode       : DryRun, aucune ecriture'
}
Write-KoxoDeployLine ''

$session = New-PSSession -ComputerName $ComputerName
try {
    # --- 1. etat de la cible -------------------------------------------------
    $remoteState = Invoke-Command -Session $session -ArgumentList $DestinationPath, $DeployableFiles, $ProtectedNames, $NormalizedHashBody.ToString() -ScriptBlock {
        param($Destination, $Names, $Protected, $HashBody)

        if (-not (Test-Path -LiteralPath $Destination -PathType Container)) {
            throw ("Dossier de destination introuvable : {0}." -f $Destination)
        }

        $normalizedHash = [scriptblock]::Create($HashBody)
        $hashes = @{}
        $normalized = @{}
        foreach ($name in $Names) {
            $path = Join-Path $Destination $name
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                $hashes[$name] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
                $normalized[$name] = & $normalizedHash $path
            }
        }

        # Scripts presents sur la cible que le depot ne pilote pas : c'est la
        # derive silencieuse qu'on veut voir apparaitre.
        $unmanaged = @(
            Get-ChildItem -LiteralPath $Destination -File |
                Where-Object {
                    $_.Extension -in @('.ps1', '.psm1', '.cmd', '.bat') -and
                    $Names -notcontains $_.Name -and
                    $Protected -notcontains $_.Name
                } |
                ForEach-Object { $_.Name }
        )

        [pscustomobject]@{ Hashes = $hashes; NormalizedHashes = $normalized; Unmanaged = $unmanaged }
    }

    # --- 2. plan -------------------------------------------------------------
    $plan = foreach ($item in $manifest) {
        $state = 'nouveau'
        if ($remoteState.NormalizedHashes.ContainsKey($item.Name)) {
            if ($remoteState.NormalizedHashes[$item.Name] -ne $item.NormalizedHash) {
                $state = 'a remplacer'
            }
            elseif ($remoteState.Hashes[$item.Name] -ne $item.Sha256) {
                # Contenu identique, seules les fins de ligne different : rien
                # a copier, mais on le dit plutot que de laisser croire a une
                # egalite parfaite.
                $state = 'a jour (fins de ligne differentes)'
            }
            else {
                $state = 'a jour'
            }
        }

        [pscustomobject]@{ Name = $item.Name; SourcePath = $item.SourcePath; Sha256 = $item.Sha256; Etat = $state }
    }

    foreach ($p in $plan) { Write-KoxoDeployLine ("  {0,-42} {1}" -f $p.Name, $p.Etat) }

    if ($remoteState.Unmanaged.Count -gt 0) {
        Write-KoxoDeployLine ''
        Write-KoxoDeployLine '  Presents sur la cible mais absents du depot (non geres) :'
        foreach ($u in $remoteState.Unmanaged) { Write-KoxoDeployLine ("    {0}" -f $u) }
    }

    $toCopy = @($plan | Where-Object { $_.Etat -eq 'nouveau' -or $_.Etat -eq 'a remplacer' })
    Write-KoxoDeployLine ''
    Write-KoxoDeployLine ("A copier : {0} fichier(s)" -f $toCopy.Count)

    # --- 3. copie ------------------------------------------------------------
    $backupDirectory = $null
    if (-not $DryRun -and $toCopy.Count -gt 0) {
        $stamp = Get-Date -Format 'yyyyMMddHHmmss'
        $backupDirectory = Invoke-Command -Session $session -ArgumentList $DestinationPath, $stamp, @($toCopy.Name) -ScriptBlock {
            param($Destination, $Stamp, $Names)
            $dir = Join-Path $Destination ("backups\deploy-{0}" -f $Stamp)
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            foreach ($name in $Names) {
                $path = Join-Path $Destination $name
                if (Test-Path -LiteralPath $path -PathType Leaf) {
                    Copy-Item -LiteralPath $path -Destination (Join-Path $dir $name) -Force
                }
            }
            $dir
        }
        Write-KoxoDeployLine ("Sauvegarde : {0}" -f $backupDirectory)

        foreach ($p in $toCopy) {
            Copy-Item -Path $p.SourcePath -Destination (Join-Path $DestinationPath $p.Name) -ToSession $session -Force
        }

        # --- 4. verification d'integrite et de syntaxe -----------------------
        $checks = Invoke-Command -Session $session -ArgumentList $DestinationPath, @($toCopy.Name), $SyntaxErrorCountBody.ToString() -ScriptBlock {
            param($Destination, $Names, $SyntaxBody)
            $syntaxCount = [scriptblock]::Create($SyntaxBody)
            foreach ($name in $Names) {
                $path = Join-Path $Destination $name
                [pscustomobject]@{
                    Name = $name
                    Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
                    SyntaxErrors = (& $syntaxCount $path)
                }
            }
        }

        foreach ($check in @($checks)) {
            $expected = ($toCopy | Where-Object { $_.Name -eq $check.Name }).Sha256
            if ($check.Sha256 -ne $expected) {
                throw ("Empreinte differente apres copie pour {0}." -f $check.Name)
            }
            if ($check.SyntaxErrors -gt 0) {
                throw ("{0} erreur(s) de syntaxe dans {1} apres copie." -f $check.SyntaxErrors, $check.Name)
            }
        }
        Write-KoxoDeployLine 'Empreintes et syntaxe verifiees sur la cible.'
    }

    # --- 5. variables d'environnement Machine --------------------------------
    $settingsChanged = $false
    if ($Settings.Count -gt 0) {
        Write-KoxoDeployLine ''
        Write-KoxoDeployLine 'Variables Machine :'
        foreach ($name in ($Settings.Keys | Sort-Object)) {
            $wanted = [string]$Settings[$name]
            $current = Invoke-Command -Session $session -ArgumentList $name -ScriptBlock {
                param($n) [Environment]::GetEnvironmentVariable($n, 'Machine')
            }

            $shown = $wanted
            if ($name -match 'TOKEN|SECRET|PASSWORD') { $shown = '<masque>' }

            if ($current -eq $wanted) {
                Write-KoxoDeployLine ("  {0,-32} = {1}  (deja conforme)" -f $name, $shown)
                continue
            }

            if ($DryRun) {
                Write-KoxoDeployLine ("  {0,-32} -> {1}  (a poser)" -f $name, $shown)
                $settingsChanged = $true
                continue
            }

            Invoke-Command -Session $session -ArgumentList $name, $wanted -ScriptBlock {
                param($n, $v) [Environment]::SetEnvironmentVariable($n, $v, 'Machine')
            }
            Write-KoxoDeployLine ("  {0,-32} = {1}  (pose)" -f $name, $shown)
            $settingsChanged = $true
        }
    }

    # --- 6. receveur ---------------------------------------------------------
    $receiverState = 'inchange'
    if ($RestartReceiver -and -not $DryRun) {
        $receiverState = Invoke-Command -Session $session -ArgumentList $ReceiverTaskName -ScriptBlock {
            param($TaskName)
            Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
            Start-ScheduledTask -TaskName $TaskName
            Start-Sleep -Seconds 3
            (Get-ScheduledTask -TaskName $TaskName).State.ToString()
        }
        Write-KoxoDeployLine ''
        Write-KoxoDeployLine ("Receveur {0} relance : etat={1}" -f $ReceiverTaskName, $receiverState)
    }
    elseif ($settingsChanged -and -not $RestartReceiver) {
        Write-KoxoDeployLine ''
        Write-Warning ("Variables modifiees sans -RestartReceiver : {0} continue de tourner avec l'ancien environnement." -f $ReceiverTaskName)
    }

    # --- 7. validation par une synchro a blanc -------------------------------
    $verification = $null
    if (-not $DryRun -and -not $SkipVerification) {
        Write-KoxoDeployLine ''
        Write-KoxoDeployLine 'Validation par une synchronisation -DryRun...'
        $verification = Invoke-Command -Session $session -ArgumentList $DestinationPath -ScriptBlock {
            param($Destination)
            # Une session WinRM n'herite d'aucune variable Machine : hydrater
            # depuis le registre, sinon la synchro echoue sur KOXO_API_URL.
            $machine = [Environment]::GetEnvironmentVariables('Machine')
            foreach ($k in ($machine.Keys | Where-Object { $_ -like 'KOXO_*' })) {
                Set-Item -Path ("env:{0}" -f $k) -Value $machine[$k]
            }

            # -PrimaryGroup obligatoire depuis la separation : l'export publie
            # deux groupes primaires, et une synchro sans aiguillage est
            # desormais refusee — c'est justement ce qu'on veut verifier ici.
            # Le nom s'ecrit par code de caractere, la session distante ne
            # garantissant pas l'encodage de ce bloc de script.
            $result = & (Join-Path $Destination 'Sync-KoXoClients.ps1') `
                -CsvTargetPath (Join-Path $Destination 'clients.csv') `
                -WorkingDirectory (Join-Path $Destination 'work') `
                -PrimaryGroup 'CLIENTS' `
                -DryRun

            # TempPath vaut $null quand le profil est saute faute d'identite a
            # publier : la validation doit le dire, pas planter dessus.
            $bytes = @()
            if ($result.TempPath) {
                $bytes = [System.IO.File]::ReadAllBytes($result.TempPath)
                Remove-Item -LiteralPath $result.TempPath -Force -ErrorAction SilentlyContinue
            }

            [pscustomobject]@{
                Status = $result.Status
                UserCount = $result.UserCount
                CsvEncoding = $result.CsvEncoding
                HasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
            }
        }

        if ($verification.Status -ne 'dry_run') {
            throw ("Validation echouee : statut inattendu '{0}'." -f $verification.Status)
        }

        Write-KoxoDeployLine ("  statut={0}  utilisateurs={1}  encodage={2}  BOM={3}" -f
            $verification.Status, $verification.UserCount, $verification.CsvEncoding, $verification.HasBom)
    }

    Write-KoxoDeployLine ''
    Write-KoxoDeployLine 'Deploiement termine.'

    [pscustomobject]@{
        ComputerName = $ComputerName
        DestinationPath = $DestinationPath
        DryRun = [bool]$DryRun
        Plan = $plan
        # Pas `$toCopy.Name` : sous Set-StrictMode, acceder a une propriete sur
        # un tableau vide leve — c'est-a-dire exactement quand tout est deja a
        # jour, le cas le plus frequent en exploitation.
        Copied = @($toCopy | ForEach-Object { $_.Name })
        Unmanaged = $remoteState.Unmanaged
        BackupDirectory = $backupDirectory
        SettingsChanged = $settingsChanged
        ReceiverState = $receiverState
        Verification = $verification
    }
}
finally {
    Remove-PSSession $session
}
