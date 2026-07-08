<#
.SYNOPSIS
Wrapper NSSM pour KermariaWebportal — charge webportal.config.json et exec node.

.DESCRIPTION
Lit le fichier de config JSON (par défaut
C:\ProgramData\Kermaria\webportal.config.json), injecte chaque clé
comme variable d'environnement de cette session PowerShell, puis exec
`node.exe <serverJsPath>`.

Les env vars n'existent QUE dans la vie de ce process (et ses enfants
comme Node). Aucune pollution Machine.

Ce wrapper est destiné à être appelé par NSSM :

  nssm install KermariaWebportal powershell.exe
  nssm set KermariaWebportal AppParameters "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File C:\apps\webportal\start-webportal.ps1"
  nssm set KermariaWebportal AppDirectory "C:\apps\webportal"

Aucune valeur sensible n'est loggée. Seuls les noms de clés chargées.

.PARAMETER ConfigPath
Chemin du config JSON. Défaut :
C:\ProgramData\Kermaria\webportal.config.json. Overridable via l'env
KERMARIA_WEBPORTAL_CONFIG_PATH.

.PARAMETER ServerJsPath
Chemin du server.js Next.js standalone. Défaut :
C:\apps\webportal\apps\webportal\server.js.

.PARAMETER NodeExe
Exécutable Node. Défaut : résolution via Get-Command node.exe.
#>
[CmdletBinding()]
param(
    [string]$ConfigPath = $(
        if ($env:KERMARIA_WEBPORTAL_CONFIG_PATH) {
            $env:KERMARIA_WEBPORTAL_CONFIG_PATH
        } else {
            "C:\ProgramData\Kermaria\webportal.config.json"
        }),
    [string]$ServerJsPath = "C:\apps\webportal\apps\webportal\server.js",
    [string]$NodeExe
)

$ErrorActionPreference = "Stop"

try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $ts = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffzzz")
    $line = "$ts [$Level] webportal-wrapper: $Message"
    # On écrit directement sur les handles stdout/stderr du process plutôt
    # que via Write-Host : sous NSSM (hôte de service non-interactif,
    # powershell -NonInteractive), le flux Information de Write-Host n'est
    # pas garanti d'atteindre le handle redirigé vers AppStdout, ce qui
    # rendait ces logs invisibles. [Console]::Out/Error écrit sur le fd
    # que NSSM capte. Les messages ERROR partent sur stderr (stderr.log),
    # alignés avec la sortie d'erreur de node.
    if ($Level -eq "ERROR") {
        [Console]::Error.WriteLine($line)
    } else {
        [Console]::Out.WriteLine($line)
    }
}

# Résolution de node.exe si non fourni
if (-not $NodeExe) {
    $cmd = Get-Command node.exe -ErrorAction SilentlyContinue
    if (-not $cmd) {
        throw "node.exe introuvable dans le PATH. Passer -NodeExe explicitement ou installer Node.js 24."
    }
    $NodeExe = $cmd.Source
}

Write-Log "node.exe        = $NodeExe"
Write-Log "server.js       = $ServerJsPath"
Write-Log "config file     = $ConfigPath"

if (-not (Test-Path -LiteralPath $ServerJsPath)) {
    throw "server.js introuvable : $ServerJsPath. Verifier le deploiement Next standalone."
}

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "Config file introuvable : $ConfigPath. Executer scripts/build-webportal-config.ps1."
}

# Lecture JSON
try {
    $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
} catch {
    throw "Config JSON invalide dans $ConfigPath : $_"
}

# Application des env vars — SESSION-scope, jamais Machine.
# ConvertFrom-Json produit un PSCustomObject ; on énumère ses propriétés.
$loaded = @()
foreach ($prop in $config.PSObject.Properties) {
    $name = $prop.Name
    $value = $prop.Value
    if ($null -ne $value -and "$value" -ne "") {
        Set-Item -Path "env:$name" -Value "$value"
        $loaded += $name
    }
}
Write-Log "Env vars chargees : $($loaded.Count) cles"

# NODE_ENV / HOSTNAME / PORT sont normalement dans le JSON (build-webportal-config.ps1
# les ajoute par défaut). Filet de sécurité si absents :
if (-not $env:NODE_ENV)  { $env:NODE_ENV  = "production" }
if (-not $env:HOSTNAME)  { $env:HOSTNAME  = "127.0.0.1" }
if (-not $env:PORT)      { $env:PORT      = "3000" }

Write-Log "Lancement de node.exe $ServerJsPath (NODE_ENV=$($env:NODE_ENV), HOSTNAME=$($env:HOSTNAME), PORT=$($env:PORT))"

# Exec node avec Start-Process aurait des complications d'héritage stdio et
# de gestion des signaux Ctrl+C envoyés par NSSM. Le call operator (&) reste
# dans le process PowerShell : NSSM voit toujours le même PID, stdout/stderr
# sont hérités, et l'arrêt via NSSM propage correctement.
& $NodeExe $ServerJsPath
$exitCode = $LASTEXITCODE

Write-Log "node.exe s'est termine avec exit code $exitCode" ($(if ($exitCode -eq 0) { "INFO" } else { "ERROR" }))
exit $exitCode
