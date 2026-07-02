# Deploiement Windows Server 2022 (SRV-01 / SRV-02 / SRV-07)

Runbook cible : deploiement natif sans VM ni Docker, sur trois hotes
Windows Server 2022 existants. Sert de reference pour la Brique 1 de
V0.24 (recette staging SRV-01/02) et d'ossature pour V1.0 beta 1
(R740xd, memes recettes appliquees sur la cible definitive).

Ce runbook complete [`DEPLOYMENT.md`](DEPLOYMENT.md) (variables
d'env, modes, garde-fous). Il ne le remplace pas.

## Topologie

```text
Internet
   │  443
   ▼
[ SRV-01  WEBPORTAL  ]  Windows Server 2022
   IIS 443 (TLS via win-acme) + ARR + URL Rewrite
      └── proxy 127.0.0.1:3000
            └── KermariaWebportal (Windows Service via NSSM)
                  = Node.js 24 + Next standalone
   │  privé, TCP 5000
   ▼
[ SRV-02  API-INTERNAL ]  Windows Server 2022
   KermariaApiInternal (Windows Service natif .NET)
      = dotnet.exe + Kestrel bind IP privée:5000
   │  privé, TCP 3306
   ▼
[ SRV-07  KERMARIA-SRV-07.home.bzh  (192.168.100.207) ]
   MariaDB 11.x
```

- SRV-01 : seul hote exposé Internet (port 443).
- SRV-02 : jamais Internet, joignable seulement depuis SRV-01 en 5000.
- SRV-07 : jamais Internet, joignable seulement depuis SRV-02 en 3306.

## Prerequis code applicatif

Ces deux changements sont deja livres dans le depot mais rappeles ici
pour la lisibilite :

- `apps/api-internal/Program.cs` appelle `builder.Host.UseWindowsService()`
  pour que le Service Control Manager Windows puisse arreter proprement
  le process (le package
  `Microsoft.Extensions.Hosting.WindowsServices` est reference dans
  le csproj).
- `apps/webportal/next.config.ts` declare `output: "standalone"` pour
  que `next build` produise un serveur autonome dans
  `.next/standalone/` (sinon il faut embarquer tout `node_modules`
  en prod, non-viable en RAM).

## Prerequis operateur

- Comptes de service locaux **non-Administrator** sur chaque hote :
  - SRV-01 : `svc-kermaria-web`
  - SRV-02 : `svc-kermaria-api`
  - SRV-07 : `svc-mariadb` (fourni par l'installeur MariaDB)
- Nom de domaine FQDN pointe vers l'IP publique de SRV-01 (pour
  Let's Encrypt HTTP-01).
- Ports pare-feu perimeter : 80 + 443 vers SRV-01 uniquement.
- Verrouillage : aucun SDK (.NET, Node) sur SRV-01/02, uniquement les
  runtimes. Le build est produit sur le poste de dev.

## 1. Build des artefacts (poste de dev)

Depuis un checkout `main` a jour :

```powershell
# API-INTERNAL — publish framework-dependent, avec apphost .exe
dotnet publish .\apps\api-internal\Kermaria.ApiInternal.csproj `
  -c Release -r win-x64 --self-contained false `
  -p:UseAppHost=true `
  -o .\out\api-internal

# WEBPORTAL — build standalone
npm --prefix apps\webportal run build

# On rassemble le paquet WEBPORTAL a copier tel quel
$dst = ".\out\webportal"
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item -Recurse -Force apps\webportal\.next\standalone\* $dst
Copy-Item -Recurse -Force apps\webportal\.next\static $dst\.next\static
Copy-Item -Recurse -Force apps\webportal\public $dst\public
```

Verifier les artefacts :

- `.\out\api-internal\Kermaria.ApiInternal.exe` existe ;
- `.\out\webportal\server.js` existe (~50 lignes) et
  `.\out\webportal\node_modules\` est mince (< 100 Mo).

Transferer les dossiers vers les serveurs (SMB partage administratif
`\\SRV-01\C$\apps\webportal-staging\` et `\\SRV-02\C$\apps\api-internal-staging\`,
ou zip + scp/RDP). **Ne pas** ecraser un deploiement live : on copie
d'abord dans un dossier `-staging` puis on bascule (voir section
"Mise a jour").

## 2. SRV-07 — MariaDB

### Installation

Installeur MSI MariaDB 11.x. Choisir la version LTS courante.
Compte root avec mot de passe fort, refuser l'installation en
"remote root access", laisser le port par defaut 3306.

### Configuration reseau

Editer `C:\Program Files\MariaDB 11.x\data\my.ini` :

```ini
[mysqld]
bind-address = 192.168.100.207
skip-name-resolve
default-storage-engine = InnoDB
innodb_buffer_pool_size = 512M    # ajuster selon RAM SRV-07
```

`bind-address` restrictif : jamais `0.0.0.0`, jamais laisse par
defaut. `skip-name-resolve` evite la latence DNS et empeche les
GRANT sur nom d'hote (on utilise IP).

Redemarrer le service :

```powershell
Restart-Service MariaDB
```

### Compte applicatif

Depuis un client `mysql` sur SRV-07 :

```sql
CREATE DATABASE kermaria
  DEFAULT CHARACTER SET utf8mb4
  DEFAULT COLLATE utf8mb4_unicode_ci;

-- <IP_SRV_02> = IP privee statique de SRV-02
CREATE USER 'kermaria_api'@'<IP_SRV_02>' IDENTIFIED BY '<mdp_fort>';

GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE
  ON kermaria.* TO 'kermaria_api'@'<IP_SRV_02>';

-- Compte de migration separe, active uniquement le temps d'appliquer
-- les migrations, revoque ensuite.
CREATE USER 'kermaria_migrator'@'<IP_SRV_02>' IDENTIFIED BY '<mdp_fort>';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX,
      REFERENCES, TRIGGER
  ON kermaria.* TO 'kermaria_migrator'@'<IP_SRV_02>';

FLUSH PRIVILEGES;
```

Note : le compte `kermaria_api` n'a **pas** CREATE/ALTER/DROP. Les
migrations passent par `kermaria_migrator` (bascule variable
`SQL_USERNAME`/`SQL_PASSWORD` le temps de l'operation).

### Pare-feu SRV-07

```powershell
New-NetFirewallRule -DisplayName "MariaDB from SRV-02" `
  -Direction Inbound -Protocol TCP -LocalPort 3306 `
  -RemoteAddress <IP_SRV_02> -Action Allow

# Refus explicite depuis tout autre origine
New-NetFirewallRule -DisplayName "MariaDB deny all others" `
  -Direction Inbound -Protocol TCP -LocalPort 3306 -Action Block
```

### Verification

Depuis SRV-02 (une fois SRV-02 provisionne) :

```powershell
Test-NetConnection 192.168.100.207 -Port 3306   # doit repondre
```

### TLS MariaDB (V1.0 beta 1)

Non exige en V0.24 (staging VLAN prive). Pour V1.0 beta 1 :
generer un certif serveur MariaDB, ajouter `ssl-ca`, `ssl-cert`,
`ssl-key` dans `my.ini`, `ALTER USER 'kermaria_api'@'…' REQUIRE
SSL;` et cote API-INTERNAL positionner `SQL_USE_SSL=true`. A
tracker dans V0.24 audit securite (Brique 2) ou en task chip.

## 3. SRV-02 — API-INTERNAL

### Installation runtime

Installer **.NET 10 Runtime (win-x64)**, pas le SDK :

- `dotnet-runtime-10.0.x-win-x64.exe`
- verification : `dotnet --list-runtimes` doit lister
  `Microsoft.NETCore.App 10.0.x` et
  `Microsoft.AspNetCore.App 10.0.x`.

### Deploiement binaire

Copier `\\SRV-02\C$\apps\api-internal-staging\` (produit en section 1)
vers `C:\apps\api-internal\` (bascule atomique : voir "Mise a jour").

Preparer le dossier de logs :

```powershell
New-Item -ItemType Directory -Force -Path C:\apps\api-internal\logs
icacls C:\apps\api-internal /inheritance:r `
  /grant:r 'Administrators:(OI)(CI)F' `
  /grant:r 'svc-kermaria-api:(OI)(CI)RX' `
  /grant:r 'svc-kermaria-api:(OI)(CI)M' # M sur logs seulement, cf plus bas
icacls C:\apps\api-internal\logs `
  /grant:r 'svc-kermaria-api:(OI)(CI)M'
```

### Variables d'environnement Machine

Injecter les secrets et la config comme **Machine-scope** (survit
au reboot, lisible par le service) :

```powershell
$scope = "Machine"
[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT","Staging",$scope)
[Environment]::SetEnvironmentVariable("DOTNET_ENVIRONMENT","Staging",$scope)
[Environment]::SetEnvironmentVariable("LOG_LEVEL","Information",$scope)
[Environment]::SetEnvironmentVariable("LOG_FILE_DIRECTORY","C:\apps\api-internal\logs",$scope)
[Environment]::SetEnvironmentVariable("LOG_FILE_LEVEL","Information",$scope)
[Environment]::SetEnvironmentVariable("LOG_FILE_RETENTION_DAYS","30",$scope)

[Environment]::SetEnvironmentVariable("SQL_PROVIDER","mariadb",$scope)
[Environment]::SetEnvironmentVariable("SQL_HOST","192.168.100.207",$scope)
[Environment]::SetEnvironmentVariable("SQL_PORT","3306",$scope)
[Environment]::SetEnvironmentVariable("SQL_DATABASE","kermaria",$scope)
[Environment]::SetEnvironmentVariable("SQL_USERNAME","kermaria_api",$scope)
[Environment]::SetEnvironmentVariable("SQL_PASSWORD","<inject>",$scope)

[Environment]::SetEnvironmentVariable("SERVICE_AUTH_TOKEN","<inject>",$scope)
[Environment]::SetEnvironmentVariable("SESSION_DURATION_MINUTES","480",$scope)
[Environment]::SetEnvironmentVariable("LOGIN_MAX_FAILURES","5",$scope)
[Environment]::SetEnvironmentVariable("LOGIN_LOCKOUT_MINUTES","15",$scope)

# Modes strictement disabled/mock/sandbox en V0.24
[Environment]::SetEnvironmentVariable("AD_INTEGRATION_MODE","disabled",$scope)
[Environment]::SetEnvironmentVariable("BPCE_INTEGRATION_MODE","mock",$scope)
[Environment]::SetEnvironmentVariable("EMAIL_INTEGRATION_MODE","mock",$scope)
[Environment]::SetEnvironmentVariable("SIGNUP_ENABLED","false",$scope)
[Environment]::SetEnvironmentVariable("PUBLIC_VITRINE_ENABLED","false",$scope)
[Environment]::SetEnvironmentVariable("AD_PASSWORD_CHANGE_ENABLED","false",$scope)
```

Repeter pour les secrets Stripe/PayPal si les scenarios de recette
correspondants sont execute (garde-fou `STRIPE_MODE=live` refuse
sans les 3 variables non-placeholder — voir
`RuntimeConfigurationValidator.cs`).

**Rappel securite** : les variables Machine-scope sont lisibles par
tout admin local. Acceptable en V0.24 staging. Pour V1.0 beta 1,
migrer vers DPAPI ou `appsettings.Production.json` avec ACL
restrictive. Traite en V0.24 Brique 2 (audit secrets).

### Appliquer les migrations

Depuis une session PowerShell **elevée** sur SRV-02, avec
temporairement les credentials `kermaria_migrator` :

```powershell
$env:SQL_USERNAME = "kermaria_migrator"
$env:SQL_PASSWORD = "<mdp migrator>"
C:\apps\api-internal\Kermaria.ApiInternal.exe --apply-migrations
```

Verifier :

```sql
SELECT migration_id, applied_at FROM schema_migrations ORDER BY applied_at;
```

Rebasculer les variables Machine-scope sur `kermaria_api` pour le
runtime nominal.

### Enregistrement Windows Service

```powershell
sc.exe create KermariaApiInternal `
  binPath= "\"C:\apps\api-internal\Kermaria.ApiInternal.exe\" --urls http://<IP_SRV_02>:5000" `
  DisplayName= "Kermaria API Internal" `
  start= auto `
  obj= ".\svc-kermaria-api" password= "<pwd>"

sc.exe description KermariaApiInternal "Kermaria portal API. Listen on private VLAN only."

# Politique de restart : 5s, 10s, 30s ; compteur remis a zero apres 1j
sc.exe failure KermariaApiInternal reset= 86400 `
  actions= restart/5000/restart/10000/restart/30000
```

Demarrer :

```powershell
Start-Service KermariaApiInternal
Get-Service KermariaApiInternal
```

### Verification

Depuis SRV-02 :

```powershell
Invoke-RestMethod http://<IP_SRV_02>:5000/health/live
Invoke-RestMethod http://<IP_SRV_02>:5000/health/ready
```

Attendu : HTTP 200 avec `X-Correlation-Id`. Si `ready` echoue,
inspecter `C:\apps\api-internal\logs\api-internal-YYYY-MM-DD.log` :
principale cause = SQL_* incorrect ou pare-feu vers SRV-07.

### Pare-feu SRV-02

```powershell
# Entrant : uniquement depuis SRV-01
New-NetFirewallRule -DisplayName "API from SRV-01" `
  -Direction Inbound -Protocol TCP -LocalPort 5000 `
  -RemoteAddress <IP_SRV_01> -Action Allow

# Sortant : uniquement vers SRV-07 sur 3306
New-NetFirewallRule -DisplayName "MariaDB to SRV-07" `
  -Direction Outbound -Protocol TCP -RemotePort 3306 `
  -RemoteAddress 192.168.100.207 -Action Allow
```

## 4. SRV-01 — WEBPORTAL

### Installation runtime

Installer **Node.js 24 LTS** (installeur MSI officiel,
`node-v24.x.x-x64.msi`). Verification :

```powershell
node --version   # v24.x.x
npm --version
```

Installer **NSSM** (https://nssm.cc, telecharger `nssm-2.24.zip`,
extraire `win64\nssm.exe` vers `C:\Program Files\nssm\`, ajouter
au PATH machine).

### Deploiement binaire

Copier `.\out\webportal\` du poste de dev vers `C:\apps\webportal\`.

Preparer les logs :

```powershell
New-Item -ItemType Directory -Force -Path C:\apps\webportal\logs
icacls C:\apps\webportal /inheritance:r `
  /grant:r 'Administrators:(OI)(CI)F' `
  /grant:r 'svc-kermaria-web:(OI)(CI)RX'
icacls C:\apps\webportal\logs /grant:r 'svc-kermaria-web:(OI)(CI)M'
```

### Variables d'environnement Machine

```powershell
$scope = "Machine"
[Environment]::SetEnvironmentVariable("NODE_ENV","production",$scope)
[Environment]::SetEnvironmentVariable("HOSTNAME","127.0.0.1",$scope)
[Environment]::SetEnvironmentVariable("PORT","3000",$scope)

[Environment]::SetEnvironmentVariable("INTERNAL_API_URL","http://<IP_SRV_02>:5000",$scope)
[Environment]::SetEnvironmentVariable("ALLOW_LOCAL_INTERNAL_API_URL","false",$scope)
[Environment]::SetEnvironmentVariable("SERVICE_AUTH_TOKEN","<meme valeur que SRV-02>",$scope)

[Environment]::SetEnvironmentVariable("SESSION_COOKIE_NAME","kermaria_session",$scope)
[Environment]::SetEnvironmentVariable("SESSION_COOKIE_SECURE","true",$scope)
[Environment]::SetEnvironmentVariable("SESSION_COOKIE_SAME_SITE","lax",$scope)
```

### Enregistrement Windows Service via NSSM

```powershell
$node = (Get-Command node.exe).Source
nssm install KermariaWebportal $node "C:\apps\webportal\server.js"
nssm set KermariaWebportal AppDirectory "C:\apps\webportal"
nssm set KermariaWebportal DisplayName "Kermaria WEBPORTAL"
nssm set KermariaWebportal Description "Kermaria Next.js portal front (SRV-01). Bound to 127.0.0.1:3000, fronted by IIS."
nssm set KermariaWebportal Start SERVICE_AUTO_START
nssm set KermariaWebportal ObjectName ".\svc-kermaria-web" "<pwd>"

# Logs rotatifs
nssm set KermariaWebportal AppStdout "C:\apps\webportal\logs\stdout.log"
nssm set KermariaWebportal AppStderr "C:\apps\webportal\logs\stderr.log"
nssm set KermariaWebportal AppRotateFiles 1
nssm set KermariaWebportal AppRotateOnline 1
nssm set KermariaWebportal AppRotateBytes 10485760      # 10 Mo
nssm set KermariaWebportal AppStopMethodSkip 0
nssm set KermariaWebportal AppExit Default Restart
nssm set KermariaWebportal AppRestartDelay 5000

Start-Service KermariaWebportal
```

### Verification interne

Depuis SRV-01 :

```powershell
Invoke-RestMethod http://127.0.0.1:3000/api/health/live
Invoke-RestMethod http://127.0.0.1:3000/api/health/ready
```

Attendu : HTTP 200. `ready` peut echouer si `INTERNAL_API_URL` non
joignable — verifier la route SRV-01 -> SRV-02:5000 et la valeur
de `SERVICE_AUTH_TOKEN` identique cote API.

### Pare-feu SRV-01 (partie WEBPORTAL)

```powershell
# Sortant : uniquement vers SRV-02 sur 5000
New-NetFirewallRule -DisplayName "API-INTERNAL to SRV-02" `
  -Direction Outbound -Protocol TCP -RemotePort 5000 `
  -RemoteAddress <IP_SRV_02> -Action Allow

# Sortant PayPal / Stripe / BPCE / SMTP : uniquement si les modes
# correspondants ne sont pas 'disabled'. Ne pas ouvrir en V0.24 tant
# que le scenario ne le demande pas.
```

## 5. SRV-01 — IIS + ARR + URL Rewrite

### Installation

```powershell
Install-WindowsFeature Web-Server, Web-Common-Http, Web-Static-Content, `
  Web-Http-Redirect, Web-Http-Logging, Web-Custom-Logging, `
  Web-Filtering, Web-Windows-Auth, Web-Basic-Auth, `
  Web-Mgmt-Tools, Web-Mgmt-Console
```

Installer **URL Rewrite 2.1** et **Application Request Routing 3.0**
via les installeurs MSI officiels de iis.net :

- https://www.iis.net/downloads/microsoft/url-rewrite
- https://www.iis.net/downloads/microsoft/application-request-routing

### Activer le proxy ARR (piege le plus frequent)

Sans cette etape, URL Rewrite ignore silencieusement les regles
`Rewrite type="Rewrite" url="http://..."` :

```powershell
Import-Module WebAdministration
Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
  -filter "system.webServer/proxy" -name "enabled" -value $true
```

Autoriser les server variables custom via IIS Manager -> serveur ->
URL Rewrite -> View Server Variables -> Add :

- `HTTP_X_FORWARDED_PROTO`
- `HTTP_X_FORWARDED_HOST`

### Site IIS

Creer un site "kermaria" pointant sur un dossier vide (le rewrite
est la vraie config) :

```powershell
New-Item -ItemType Directory -Force -Path C:\inetpub\kermaria
New-Website -Name "kermaria" -PhysicalPath "C:\inetpub\kermaria" `
  -Port 80 -HostHeader "portail.example.com" -Force
```

Placer ce `web.config` dans `C:\inetpub\kermaria\` :

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <!-- ACME challenge Let's Encrypt : servir en clair 80 -->
        <rule name="AcmeChallenge" stopProcessing="true">
          <match url="^\.well-known/acme-challenge/(.*)$" />
          <action type="None" />
        </rule>

        <!-- Force HTTPS -->
        <rule name="ForceHttps" stopProcessing="true">
          <match url="(.*)" />
          <conditions>
            <add input="{HTTPS}" pattern="off" />
          </conditions>
          <action type="Redirect" url="https://{HTTP_HOST}/{R:1}"
                  redirectType="Permanent" />
        </rule>

        <!-- Reverse proxy vers Node -->
        <rule name="ReverseProxyToNext" stopProcessing="true">
          <match url="(.*)" />
          <action type="Rewrite" url="http://127.0.0.1:3000/{R:1}" />
          <serverVariables>
            <set name="HTTP_X_FORWARDED_PROTO" value="https" />
            <set name="HTTP_X_FORWARDED_HOST" value="{HTTP_HOST}" />
          </serverVariables>
        </rule>
      </rules>
    </rewrite>

    <httpProtocol>
      <customHeaders>
        <remove name="X-Powered-By" />
        <remove name="Server" />
      </customHeaders>
    </httpProtocol>

    <security>
      <requestFiltering>
        <requestLimits maxAllowedContentLength="10485760" />  <!-- 10 Mo -->
      </requestFiltering>
    </security>
  </system.webServer>
</configuration>
```

## 6. SRV-01 — TLS via win-acme (Let's Encrypt)

Telecharger `win-acme.v2.x.x.x64.pluggable.zip` depuis
https://github.com/win-acme/win-acme/releases, extraire vers
`C:\tools\wacs\`.

Prealable : le site IIS repond deja en 80 sur
`http://portail.example.com/` (verifier avec un curl externe).

Emission :

```powershell
cd C:\tools\wacs
.\wacs.exe --target iis --siteid <id_du_site_kermaria> `
  --installation iis --emailaddress "ops@example.com" --accepttos
```

Mode interactif : selectionner "Single binding of an IIS site" pour
un domaine sans wildcard.

`wacs.exe` :

- valide le challenge HTTP-01 en placant un fichier temporaire dans
  `<siteRoot>\.well-known\acme-challenge\` ;
- obtient le certif Let's Encrypt ;
- l'importe dans le store `Local Machine\Personal` ;
- cree ou reconfigure le binding IIS 443 avec ce certif ;
- cree une **tache planifiee** `win-acme renew` qui renouvelle
  chaque 55 jours.

Verifier :

```powershell
Get-ScheduledTask -TaskName "win-acme renew*"
Get-WebBinding -Name kermaria     # doit lister 443 avec un certif
```

Ajouter le pare-feu :

```powershell
New-NetFirewallRule -DisplayName "HTTPS in" -Direction Inbound `
  -Protocol TCP -LocalPort 443 -Action Allow
New-NetFirewallRule -DisplayName "HTTP in for ACME" -Direction Inbound `
  -Protocol TCP -LocalPort 80 -Action Allow
```

Alternative pour un wildcard ou si port 80 ferme : plugin DNS-01
OVH (`--validation dns --dnsvalidation dns-01-ovh`) — necessite
cles API OVH stockees chiffrees dans le config wacs.

### HSTS (a differer)

Ajouter apres 1-2 semaines de fonctionnement stable :

```xml
<httpProtocol>
  <customHeaders>
    <add name="Strict-Transport-Security"
         value="max-age=31536000; includeSubDomains" />
  </customHeaders>
</httpProtocol>
```

Pas d'`preload` avant V1.0 RC.

## 7. Recapitulatif pare-feu

| Source | Destination | Port | Direction | Regle |
|---|---|---|---|---|
| Internet | SRV-01 | 443 | In | Autorise |
| Internet | SRV-01 | 80 | In | Autorise (ACME) |
| SRV-01 | SRV-02 | 5000 | Out | Autorise |
| SRV-02 | SRV-07 | 3306 | Out | Autorise |
| SRV-01 | Internet 443 | 443 | Out | Autorise si PayPal/Stripe/BPCE/SMTP `!= disabled` |
| Reseau admin | SRV-01/02/07 | RDP 3389 | In | Autorise depuis VPN uniquement |
| Tout autre flux | — | — | — | Refuse par defaut |

## 8. Verifications post-installation

Depuis un poste externe :

```powershell
Invoke-RestMethod https://portail.example.com/api/health/live
Invoke-RestMethod https://portail.example.com/api/health/ready
```

Attendu : HTTP 200 + `X-Correlation-Id`. Ouvrir DevTools navigateur :

- cookie session `HttpOnly`, `Secure`, `SameSite=Lax`, presente
  seulement apres login ;
- headers reponses : `X-Content-Type-Options: nosniff`,
  `X-Frame-Options: DENY`, CSP, `Referrer-Policy`,
  `Permissions-Policy`, `X-Robots-Tag`, `Strict-Transport-Security`
  (si HSTS active).

Executer la [`V0.17_RECETTE_PREPRODUCTION.md`](V0.17_RECETTE_PREPRODUCTION.md)
et la Brique 1 de [`V0.24_STABILISATION.md`](V0.24_STABILISATION.md).

## 9. Mise a jour (redeploiement d'une nouvelle version)

Regle : ne jamais ecraser un dossier live en place. Deux dossiers,
bascule par renommage.

Sur SRV-02 :

```powershell
Stop-Service KermariaApiInternal
Rename-Item C:\apps\api-internal C:\apps\api-internal-old-<DATE>
Rename-Item C:\apps\api-internal-staging C:\apps\api-internal
Start-Service KermariaApiInternal
# Verifier /health/ready
# Si OK : Remove-Item -Recurse -Force C:\apps\api-internal-old-<DATE>
# Si KO : Stop, renommage inverse, restart
```

Sur SRV-01 :

```powershell
Stop-Service KermariaWebportal
Rename-Item C:\apps\webportal C:\apps\webportal-old-<DATE>
Rename-Item C:\apps\webportal-staging C:\apps\webportal
Start-Service KermariaWebportal
# Verifier /api/health/ready via IIS
```

Migrations MariaDB : appliquer **avant** la bascule API si la nouvelle
version en contient. Rollback plus complexe si une migration cassante
est deja appliquee — voir [`BACKUP_RESTORE.md`](BACKUP_RESTORE.md).

## 10. Rollback

Enchainement general :

1. `Stop-Service KermariaWebportal` sur SRV-01 (arret trafic
   utilisateur).
2. `Stop-Service KermariaApiInternal` sur SRV-02.
3. Restaurer les dossiers `-old-<DATE>` par renommage inverse.
4. Restaurer la sauvegarde MariaDB sur SRV-07 si une migration est
   en cause (procedure detaillee dans
   [`BACKUP_RESTORE.md`](BACKUP_RESTORE.md)).
5. `Start-Service KermariaApiInternal` puis `KermariaWebportal`.
6. Verifier `/health/ready` et `/api/health/ready`.
7. Verifier login client et admin.

## 11. Surveillance minimale

Tache planifiee toutes les 5 min sur un hote de monitoring
(ou SRV-01 si pas d'hote dedie) :

```powershell
try {
    $r = Invoke-RestMethod https://portail.example.com/api/health/ready `
      -TimeoutSec 10
    if ($r.status -ne "ok") { throw "ready reports $($r.status)" }
} catch {
    # envoyer un mail ou webhook alerte
    Send-MailMessage -To ops@example.com -Subject "KO Kermaria ready" `
      -Body "$($_.Exception.Message)"
}
```

Supervision externe recommandee (UptimeRobot, healthchecks.io,
BetterUptime) pointant sur `/api/health/ready`. Alerte si HTTP != 200
deux fois consecutives (evite le faux positif sur un restart 5s).

Journal fichier :

- API-INTERNAL : `C:\apps\api-internal\logs\api-internal-YYYY-MM-DD.log`,
  rotation quotidienne, retention `LOG_FILE_RETENTION_DAYS`.
- WEBPORTAL : `C:\apps\webportal\logs\stdout.log` / `stderr.log`,
  rotation 10 Mo geree par NSSM.
- IIS : `C:\inetpub\logs\LogFiles\W3SVC<id>\`, retention par
  default 30 jours (ajuster via Get-WebSite -> logFile).

Aucun log ne doit contenir de secret. Verification periodique :

```powershell
Select-String C:\apps\api-internal\logs\*.log `
  -Pattern 'password|secret|Bearer|refresh_token|client_secret' `
  -CaseSensitive:$false
```

## 12. RAM et sizing

| Composant | Hote | RAM typique |
|---|---|---|
| Windows Server 2022 (avec GUI) | tous | ~1.5 Go |
| MariaDB (base < 5 Go) | SRV-07 | 512 Mo - 1 Go |
| Kestrel API-INTERNAL | SRV-02 | 150-300 Mo |
| Node.js WEBPORTAL | SRV-01 | 150-300 Mo |
| IIS worker + ARR | SRV-01 | 100-200 Mo |

Configurations minimum recommandees :

- SRV-01 : 4 Go
- SRV-02 : 4 Go
- SRV-07 : 4 Go (8 Go si volumetrie MariaDB > 2 Go)

Passer en **Server Core** (sans GUI) libere ~500 Mo par hote et
reduit la surface d'attaque, au prix d'une administration
exclusivement PowerShell/RSAT.

## 13. Suite

Ce runbook couvre la phase de tests SRV-01/02/07 (V0.24 Brique 1)
et sera adapte comme ossature de la procedure V1.0 beta 1 sur
R740xd (V0.24 Brique 3, livrable
[`PRODUCTION_DEPLOYMENT.md`](PRODUCTION_DEPLOYMENT.md)) avec les
delta suivants :

- comptes de service **AD domain-joined** au lieu de comptes locaux
  (si SRV-01/02 rejoignent le domaine `home.bzh`) ;
- migration secrets vers DPAPI ou secret store dedie ;
- TLS MariaDB active (`REQUIRE SSL`) ;
- bascule des modes vers `live` selon l'ordre documente en
  V0.24 Brique 3 ;
- HSTS et HSTS preload apres validation ;
- retention des logs et sauvegardes selon la politique
  d'exploitation definitive.

Voir aussi [`DEPLOYMENT.md`](DEPLOYMENT.md), [`SECURITY.md`](SECURITY.md),
[`SECRET_ROTATION.md`](SECRET_ROTATION.md), [`OPERATIONS.md`](OPERATIONS.md),
[`BACKUP_RESTORE.md`](BACKUP_RESTORE.md).
