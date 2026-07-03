# Deploiement Windows Server 2022 (KERMARIA-SRV-01 / KERMARIA-SRV-02 / KERMARIA-SRV-07)

Runbook cible : deploiement natif sans VM ni Docker, sur trois hotes
Windows Server 2022 existants. Sert de reference pour la Brique 1 de
V0.24 (recette staging KERMARIA-SRV-01/02) et d'ossature pour V1.0 beta 1
(R740xd, memes recettes appliquees sur la cible definitive).

Ce runbook complete [`DEPLOYMENT.md`](DEPLOYMENT.md) (variables
d'env, modes, garde-fous). Il ne le remplace pas.

## Topologie et materiel

```text
Internet
   │  443
   ▼
[ KERMARIA-SRV-01  WEBPORTAL  ]  Dell Optiplex 5070 (i7-9700, 40 Go DDR4)
   Windows Server 2022
   IIS 443 (TLS via win-acme) + ARR + URL Rewrite
      └── proxy 127.0.0.1:3000
            └── KermariaWebportal (Windows Service via NSSM)
                  = Node.js 24 + Next standalone
   │  privé, TCP 5000
   ▼
[ KERMARIA-SRV-02  API-INTERNAL ]  ASUS FX753VD (i7-7700HQ, 32 Go DDR4)
   Windows Server 2022
   KermariaApiInternal (Windows Service natif .NET)
      = Kermaria.ApiInternal.exe + Kestrel bind IP privée:5000
   │  privé, TCP 3306
   ▼
[ KERMARIA-SRV-07  (kermaria-srv-07.home.bzh, 192.168.100.207) ]
   MariaDB 11.x
```

- KERMARIA-SRV-01 : seul hote exposé Internet (port 443). 40 Go RAM
  largement suffisants pour Node + IIS + supervision.
- KERMARIA-SRV-02 : jamais Internet, joignable seulement depuis
  KERMARIA-SRV-01 en 5000. 32 Go RAM permettent une marge confortable
  meme si un dump MariaDB ou une charge de recette temporaire y
  transite. GPU GTX 1050 non utilise (pas de pilote necessaire cote
  serveur, laisser desactive dans Device Manager pour eviter les
  MAJ Windows Update qui redemarrent la machine).
- KERMARIA-SRV-07 : jamais Internet, joignable seulement depuis
  KERMARIA-SRV-02 en 3306.

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

- Comptes de service **non-Administrator** sur chaque hote :
  - KERMARIA-SRV-01 : `HOME\svc_api_portal_ad` (compte AD partagé
    avec l'API, voir section "Comptes de service")
  - KERMARIA-SRV-02 : `HOME\svc_api_portal_ad`
  - KERMARIA-SRV-07 : `svc-mariadb` (fourni par l'installeur MariaDB)
- Nom de domaine FQDN pointe vers l'IP publique de KERMARIA-SRV-01 (pour
  Let's Encrypt HTTP-01).
- Ports pare-feu perimeter : 80 + 443 vers KERMARIA-SRV-01 uniquement.
- Verrouillage : aucun SDK (.NET, Node) sur KERMARIA-SRV-01/02, uniquement les
  runtimes. Le build est produit sur le poste de dev.

### Comptes de service

**Option retenue** (2026-07-03) : compte AD unique
`HOME\svc_api_portal_ad` pour les deux services (KermariaApiInternal
sur SRV-02 et KermariaWebportal sur SRV-01). Meilleure hygiene que
les comptes locaux : mot de passe gere cote domaine, revocation
centrale, un seul secret a tracker.

Conditions :

- Les deux serveurs doivent etre **joints au domaine HOME** :
  `(Get-CimInstance Win32_ComputerSystem).PartOfDomain` retourne
  `True`.
- Le compte `HOME\svc_api_portal_ad` doit exister cote AD
  (pre-existant dans cette infra).
- NSSM et `New-Service` ajoutent automatiquement le droit "Log on
  as a service" au compte. Si une GPO du domaine bloque cet
  ajout, il faut declarer le compte dans "Log on as a service"
  via `secpol.msc` (Local Security Policy) ou GPO.

**Fallback comptes locaux** (si le domaine n'est pas dispo ou pour
un environnement standalone) :

```powershell
# Sur KERMARIA-SRV-02
$pwd = Read-Host -AsSecureString "Mot de passe svc-kermaria-api"
New-LocalUser -Name "svc-kermaria-api" -Password $pwd `
  -PasswordNeverExpires -UserMayNotChangePassword `
  -Description "Kermaria API-INTERNAL service account" `
  -AccountNeverExpires

# Sur KERMARIA-SRV-01
$pwd = Read-Host -AsSecureString "Mot de passe svc-kermaria-web"
New-LocalUser -Name "svc-kermaria-web" -Password $pwd `
  -PasswordNeverExpires -UserMayNotChangePassword `
  -Description "Kermaria WEBPORTAL service account" `
  -AccountNeverExpires
```

**Rappel** : le compte utilise (AD ou local) doit exister avant les
`icacls /grant:r`, sinon la commande echoue avec `Le mappage entre
les noms de compte et les ID de sécurité n'a pas été effectué`.

Dans le reste de ce runbook, remplacer :

- `HOME\svc_api_portal_ad` par `.\svc-kermaria-api` (SRV-02) ou
  `.\svc-kermaria-web` (SRV-01) si tu bascules sur les comptes
  locaux.

### ACL avec SIDs bien-known (langue-neutre)

Windows FR utilise `Administrateurs` (groupe), `Administrateur` etant
un compte. Pour ne pas dependre de la localisation, utiliser les SIDs
bien-known avec le prefixe `*` dans `icacls` :

| SID | Signification |
|---|---|
| `*S-1-5-32-544` | Groupe local `Administrators` / `Administrateurs` |
| `*S-1-5-18` | Compte `SYSTEM` / `Système` |
| `*S-1-5-32-545` | Groupe `Users` / `Utilisateurs` |

Exemple applique dans les sections KERMARIA-SRV-01 et KERMARIA-SRV-02
plus bas.

## 1. Build des artefacts (poste de dev)

Depuis un checkout `main` a jour, sur le poste de dev (pas dans un
worktree Claude Code : les junctions node_modules d'un worktree
cassent Turbopack, cf. memoire `workflow-preferences`) :

```powershell
# API-INTERNAL — publish framework-dependent, avec apphost .exe
dotnet publish .\apps\api-internal\Kermaria.ApiInternal.csproj `
  -c Release -r win-x64 --self-contained false `
  -p:UseAppHost=true `
  -o .\out\api-internal

# WEBPORTAL — build standalone
npm --prefix apps\webportal run build
```

`next build` avec `output: "standalone"` produit un layout
**monorepo-aware** (parce que `turbopack.root` pointe sur le repo
racine dans `next.config.ts`) :

```text
apps\webportal\.next\standalone\
├── apps\webportal\
│   ├── server.js          # entrypoint Node
│   ├── package.json
│   └── .next\             # chunks serveur (sans /static ni /public)
└── node_modules\          # deps prod hoistées
```

Il faut copier `.next\static` et `public` **manuellement** — Next
ne les inclut jamais dans le standalone (voir doc Next.js).
Assembler le paquet a transferer :

```powershell
$dst = ".\out\webportal"
Remove-Item -Recurse -Force $dst -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $dst | Out-Null

# Base standalone (contient apps\webportal\ + node_modules\)
Copy-Item -Recurse -Force apps\webportal\.next\standalone\* $dst

# static et public a placer au niveau du server.js
Copy-Item -Recurse -Force apps\webportal\.next\static `
  "$dst\apps\webportal\.next\static"
Copy-Item -Recurse -Force apps\webportal\public `
  "$dst\apps\webportal\public"
```

Verifier les artefacts :

- `.\out\api-internal\Kermaria.ApiInternal.exe` existe ;
- `.\out\webportal\apps\webportal\server.js` existe ;
- `.\out\webportal\apps\webportal\.next\static\` contient les chunks
  client (bundles JS/CSS hashes) ;
- `.\out\webportal\apps\webportal\public\portfolio\` contient bien
  le portfolio embarque V0.27 ;
- `.\out\webportal\node_modules\next\` present.

Transferer les dossiers vers les serveurs (SMB partage administratif
`\\KERMARIA-SRV-01\C$\apps\webportal-staging\` et
`\\KERMARIA-SRV-02\C$\apps\api-internal-staging\`, ou zip + scp/RDP).
**Ne pas** ecraser un deploiement live : on copie d'abord dans un
dossier `-staging` puis on bascule (voir section "Mise a jour").

## 2. KERMARIA-SRV-07 — MariaDB

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
innodb_buffer_pool_size = 512M    # ajuster selon RAM KERMARIA-SRV-07
```

`bind-address` restrictif : jamais `0.0.0.0`, jamais laisse par
defaut. `skip-name-resolve` evite la latence DNS et empeche les
GRANT sur nom d'hote (on utilise IP).

Redemarrer le service :

```powershell
Restart-Service MariaDB
```

### Compte applicatif

Depuis un client `mysql` sur KERMARIA-SRV-07 :

```sql
CREATE DATABASE kermaria
  DEFAULT CHARACTER SET utf8mb4
  DEFAULT COLLATE utf8mb4_unicode_ci;

-- <IP_KERMARIA_SRV_02> = IP privee statique de KERMARIA-SRV-02
CREATE USER 'kermaria_api'@'<IP_KERMARIA_SRV_02>' IDENTIFIED BY '<mdp_fort>';

GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE
  ON kermaria.* TO 'kermaria_api'@'<IP_KERMARIA_SRV_02>';

-- Compte de migration separe, active uniquement le temps d'appliquer
-- les migrations, revoque ensuite.
CREATE USER 'kermaria_migrator'@'<IP_KERMARIA_SRV_02>' IDENTIFIED BY '<mdp_fort>';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX,
      REFERENCES, TRIGGER
  ON kermaria.* TO 'kermaria_migrator'@'<IP_KERMARIA_SRV_02>';

FLUSH PRIVILEGES;
```

Note : le compte `kermaria_api` n'a **pas** CREATE/ALTER/DROP. Les
migrations passent par `kermaria_migrator` (bascule variable
`SQL_USERNAME`/`SQL_PASSWORD` le temps de l'operation).

### Pare-feu KERMARIA-SRV-07

```powershell
New-NetFirewallRule -DisplayName "MariaDB from KERMARIA-SRV-02" `
  -Direction Inbound -Protocol TCP -LocalPort 3306 `
  -RemoteAddress <IP_KERMARIA_SRV_02> -Action Allow

# Refus explicite depuis tout autre origine
New-NetFirewallRule -DisplayName "MariaDB deny all others" `
  -Direction Inbound -Protocol TCP -LocalPort 3306 -Action Block
```

### Verification

Depuis KERMARIA-SRV-02 (une fois KERMARIA-SRV-02 provisionne) :

```powershell
Test-NetConnection 192.168.100.207 -Port 3306   # doit repondre
```

### TLS MariaDB (V1.0 beta 1)

Non exige en V0.24 (staging VLAN prive). Pour V1.0 beta 1 :
generer un certif serveur MariaDB, ajouter `ssl-ca`, `ssl-cert`,
`ssl-key` dans `my.ini`, `ALTER USER 'kermaria_api'@'…' REQUIRE
SSL;` et cote API-INTERNAL positionner `SQL_USE_SSL=true`. A
tracker dans V0.24 audit securite (Brique 2) ou en task chip.

## 3. KERMARIA-SRV-02 — API-INTERNAL

### Installation runtime

Installer **.NET 10 Runtime (win-x64)**, pas le SDK :

- `dotnet-runtime-10.0.x-win-x64.exe`
- verification : `dotnet --list-runtimes` doit lister
  `Microsoft.NETCore.App 10.0.x` et
  `Microsoft.AspNetCore.App 10.0.x`.

### Deploiement binaire

Copier `\\KERMARIA-SRV-02\C$\apps\api-internal-staging\` (produit en section 1)
vers `C:\apps\api-internal\` (bascule atomique : voir "Mise a jour").

Preparer le dossier de logs :

```powershell
New-Item -ItemType Directory -Force -Path C:\apps\api-internal\logs

# SIDs langue-neutre : S-1-5-32-544 = Administrators/Administrateurs,
# S-1-5-18 = SYSTEM/Système. Le prefixe '*' indique un SID litteral.
# Le compte AD HOME\svc_api_portal_ad est le compte de service partage.
icacls C:\apps\api-internal /inheritance:r `
  /grant:r '*S-1-5-32-544:(OI)(CI)F' `
  /grant:r '*S-1-5-18:(OI)(CI)RX' `
  /grant:r 'HOME\svc_api_portal_ad:(OI)(CI)RX'

# Sur logs, le service doit ecrire (Modify)
icacls C:\apps\api-internal\logs `
  /grant:r 'HOME\svc_api_portal_ad:(OI)(CI)M'
```

Si le compte AD n'est pas resolvable (SRV-02 hors domaine, faute
de frappe, compte inexistant), `icacls` renvoie `Le mappage entre
les noms de compte et les ID de sécurité n'a pas été effectué`.
Verifier `(Get-CimInstance Win32_ComputerSystem).PartOfDomain` et
`Get-ADUser svc_api_portal_ad` (depuis un DC ou avec RSAT).

### Configuration

Toute la config runtime (SQL, secrets, modes, logs, session, seuils,
BPCE / PayPal / Stripe / SMTP / hCaptcha) est **rassemblee dans un
seul fichier JSON externe** :
`C:\ProgramData\Kermaria\api-internal.config.json`.

Aucune variable d'environnement Machine n'est necessaire cote
API-INTERNAL. L'environnement (`Staging`, `Production`) se passe
via l'argument CLI `--environment` du service Windows (voir
section "Enregistrement Windows Service").

Precedence des sources de config (la plus a droite gagne) :

```
appsettings.json < appsettings.{Env}.json < config.json < env vars < CLI args
```

Les env vars gardent la main : ca permet un override ponctuel
depuis une session PowerShell (typiquement `--apply-migrations`
avec `kermaria_migrator`) sans editer le fichier.

#### Creation du dossier

```powershell
New-Item -ItemType Directory -Force -Path C:\ProgramData\Kermaria

icacls C:\ProgramData\Kermaria /inheritance:r `
  /grant:r '*S-1-5-32-544:(OI)(CI)F' `
  /grant:r 'HOME\svc_api_portal_ad:(OI)(CI)RX'
```

#### Generation du fichier config

Deposer `C:\ProgramData\Kermaria\api-internal.config.json`, soit
manuellement, soit avec le convertisseur
[`scripts/build-api-config.ps1`](../scripts/build-api-config.ps1)
qui derive le JSON depuis un `.local.env.ps1` (regex sur les
`$env:KEY = "value"`, blocklist des cles interdites, n'affiche
jamais les valeurs) :

```powershell
# Depuis le poste de dev, ecrit directement sur KERMARIA-SRV-02 via SMB
.\scripts\build-api-config.ps1 `
  -OutputPath \\KERMARIA-SRV-02\C$\ProgramData\Kermaria\api-internal.config.json

# Aperçu sans ecriture
.\scripts\build-api-config.ps1 -WhatIf

# Sur KERMARIA-SRV-02 directement avec un source local
.\scripts\build-api-config.ps1 -InputPath C:\admin\dev.env.ps1
```

Le script auto-detecte le fichier source dans (ordre) :
1. `<repo>/.local.env.ps1`
2. `<repo-parent>/<repo-name>.local.env.ps1`
3. `<repo-parent>/.local.env.ps1`

Blocklist appliquee (jamais extraites, meme presentes en source) :
`DEMO_PORTAL_EMAIL`, `DEMO_PORTAL_PASSWORD`,
`DEMO_INTERNAL_ADMIN_EMAIL`, `DEMO_INTERNAL_ADMIN_PASSWORD`,
`RUN_MARIADB_TESTS`, `ALLOW_LOCAL_INTERNAL_API_URL`,
`ASPNETCORE_ENVIRONMENT`, `DOTNET_ENVIRONMENT`,
`KERMARIA_CONFIG_PATH`.

Les valeurs vides sont egalement omises (bruit inutile).

Format du JSON produit (plat, cles = noms de config API) :

```json
{
  "LOG_LEVEL": "Information",
  "LOG_FILE_DIRECTORY": "C:\\apps\\api-internal\\logs",
  "LOG_FILE_LEVEL": "Information",
  "LOG_FILE_RETENTION_DAYS": "30",
  "SQL_PROVIDER": "mariadb",
  "SQL_HOST": "192.168.100.207",
  "SQL_PORT": "3306",
  "SQL_DATABASE": "kermaria",
  "SQL_USERNAME": "kermaria_api",
  "SQL_PASSWORD": "<mdp_kermaria_api>",
  "SERVICE_AUTH_TOKEN": "<token_partage_avec_webportal>",
  "SESSION_DURATION_MINUTES": "480",
  "LOGIN_MAX_FAILURES": "5",
  "LOGIN_LOCKOUT_MINUTES": "15",
  "AD_INTEGRATION_MODE": "disabled",
  "BPCE_INTEGRATION_MODE": "mock",
  "EMAIL_INTEGRATION_MODE": "mock",
  "SIGNUP_ENABLED": "false",
  "PUBLIC_VITRINE_ENABLED": "false",
  "AD_PASSWORD_CHANGE_ENABLED": "false"
}
```

Ajouter les secrets BPCE / PayPal / Stripe / SMTP au fur et a
mesure des scenarios de recette V0.24 qui les exigent.

#### ACL restrictive sur le fichier

```powershell
icacls C:\ProgramData\Kermaria\api-internal.config.json /inheritance:r `
  /grant:r '*S-1-5-32-544:F' `
  /grant:r 'HOME\svc_api_portal_ad:R'
```

Seul un admin peut ecrire, seul le compte de service peut lire.

#### Nettoyage des anciennes variables Machine

Si des variables Machine ont ete positionnees avant la bascule (ancien
runbook, `.local.env.ps1` sourcé) :

```powershell
Get-ChildItem Env: `
  | Where-Object Name -match '^(SQL_|BPCE_|PAYPAL_|STRIPE_|SMTP_|SERVICE_AUTH|HCAPTCHA_|LOG_|SESSION_|LOGIN_|AD_|EMAIL_|SIGNUP_|PUBLIC_VITRINE|BILLING_)' `
  | ForEach-Object {
      [Environment]::SetEnvironmentVariable($_.Name, $null, 'Machine')
    }
```

Verifier ensuite `Get-ChildItem Env:` : seules les variables systeme
Windows doivent rester.

#### Override ponctuel via env vars

Les env vars gagnent sur le fichier config. Utile pour un run
ad-hoc (par exemple `--apply-migrations` avec `kermaria_migrator`
temporaire) : ouvrir une session PowerShell, definir uniquement les
valeurs a overrider comme `$env:...`, lancer la commande, fermer
la session. Voir section "Appliquer les migrations".

### Appliquer les migrations

`--apply-migrations` refuse tout environnement autre que
`Development` (garde-fou code en dur dans
[Program.cs:277-283](../apps/api-internal/Program.cs)) — c'est un
choix explicite pour eviter qu'une commande ops ne touche
accidentellement une base staging/prod. Pour appliquer les
migrations en staging il faut basculer l'environnement **au niveau
de la session PowerShell seulement** (pas Machine), le temps de
l'operation. Le process quitte de lui-meme apres migration
(`return;`), donc l'API ne demarre jamais en mode Development.

Depuis une session PowerShell **elevée** sur KERMARIA-SRV-02 :

```powershell
# Override SQL pour CETTE session uniquement (le service utilisera
# toujours kermaria_api depuis le config file au prochain start).
$env:SQL_USERNAME = "kermaria_migrator"
$env:SQL_PASSWORD = "<mdp migrator>"

# --environment Development satisfait le garde-fou de Program.cs
C:\apps\api-internal\Kermaria.ApiInternal.exe --environment Development --apply-migrations
```

Le process applique les migrations puis quitte. **Fermer la fenetre
PowerShell** juste apres pour que `$env:SQL_USERNAME` /
`$env:SQL_PASSWORD` disparaissent. Le service Windows continuera a
utiliser `kermaria_api` depuis
`C:\ProgramData\Kermaria\api-internal.config.json`.

Prerequis MariaDB (sur KERMARIA-SRV-07 en `mysql -u root -p`) : la
base et les deux comptes doivent exister avant, sinon le migration
runner echoue immediatement avec `Unknown database 'kermaria'` ou
`Access denied for user 'kermaria_migrator'`. Voir section
"KERMARIA-SRV-07 — MariaDB / Compte applicatif".

Verifier depuis KERMARIA-SRV-07 :

```sql
USE kermaria;
SELECT migration_id, applied_at FROM schema_migrations ORDER BY applied_at;
```

Doit lister toutes les migrations 001 a 020_signup_pending inclus.
Si une manque, verifier les credentials `kermaria_migrator` et les
GRANTs (CREATE/ALTER/DROP requis).

### Enregistrement Windows Service

`--environment Staging` remplace la variable Machine
`ASPNETCORE_ENVIRONMENT`. Parseé par ASP.NET Core dans
`CreateBuilder(args)` avant la lecture du config file, donc
`app.Environment.IsDevelopment()` fonctionne comme attendu.

`New-Service` est plus lisible que `sc.exe create` (quoting geré
par PowerShell natif, pas de `binPath= ` avec espace obligatoire
qui fait échouer si on l'oublie). Les placeholders `<IP…>` et
`<pwd>` doivent etre substitues par des valeurs reelles.

```powershell
# Credentials du compte de service AD (prompt secure)
$cred = Get-Credential -UserName "HOME\svc_api_portal_ad" `
  -Message "Mot de passe svc_api_portal_ad"

# Assigner "Log on as a service" au compte (sinon New-Service refuse le start)
# Cette ligne est optionnelle : sc.exe / New-Service l'ajoute automatiquement
# lors de la creation avec ObjectName.

New-Service -Name "KermariaApiInternal" `
  -BinaryPathName '"C:\apps\api-internal\Kermaria.ApiInternal.exe" --environment Staging --urls http://192.168.100.202:5000' `
  -DisplayName "Kermaria API Internal" `
  -Description "Kermaria portal API. Listen on private VLAN only. Config from C:\ProgramData\Kermaria\api-internal.config.json." `
  -StartupType Automatic `
  -Credential $cred

# Politique de restart : 5s, 10s, 30s ; compteur remis a zero apres 1 jour.
# sc.exe failure a une syntaxe finicky (espace apres =), mais fonctionne ici :
sc.exe failure KermariaApiInternal reset= 86400 actions= restart/5000/restart/10000/restart/30000

Start-Service KermariaApiInternal
Get-Service KermariaApiInternal
```

### Verification

Depuis KERMARIA-SRV-02 :

```powershell
Invoke-RestMethod http://<IP_KERMARIA_SRV_02>:5000/health/live
Invoke-RestMethod http://<IP_KERMARIA_SRV_02>:5000/health/ready
```

Attendu : HTTP 200 avec `X-Correlation-Id`. Si `ready` echoue,
inspecter `C:\apps\api-internal\logs\api-internal-YYYY-MM-DD.log` :
principale cause = SQL_* incorrect ou pare-feu vers KERMARIA-SRV-07.

### Pare-feu KERMARIA-SRV-02

```powershell
# Entrant : uniquement depuis KERMARIA-SRV-01
New-NetFirewallRule -DisplayName "API from KERMARIA-SRV-01" `
  -Direction Inbound -Protocol TCP -LocalPort 5000 `
  -RemoteAddress <IP_KERMARIA_SRV_01> -Action Allow

# Sortant : uniquement vers KERMARIA-SRV-07 sur 3306
New-NetFirewallRule -DisplayName "MariaDB to KERMARIA-SRV-07" `
  -Direction Outbound -Protocol TCP -RemotePort 3306 `
  -RemoteAddress 192.168.100.207 -Action Allow
```

## 4. KERMARIA-SRV-01 — WEBPORTAL

### Installation runtime

Installer **Node.js 24 LTS** (installeur MSI officiel,
`node-v24.x.x-x64.msi`). Verification :

```powershell
node --version   # v24.x.x
npm --version
```

Installer **NSSM**. Automatisation possible :

```powershell
$nssmDir = "C:\Program Files\nssm"
New-Item -ItemType Directory -Force -Path $nssmDir | Out-Null

$zip = "$env:TEMP\nssm-2.24.zip"
Invoke-WebRequest -Uri "https://nssm.cc/release/nssm-2.24.zip" -OutFile $zip
Expand-Archive -Path $zip -DestinationPath "$env:TEMP\nssm-2.24-extract" -Force
Copy-Item "$env:TEMP\nssm-2.24-extract\nssm-2.24\win64\nssm.exe" `
  "$nssmDir\nssm.exe" -Force
Remove-Item $zip, "$env:TEMP\nssm-2.24-extract" -Recurse -Force

# Ajouter au PATH Machine (pris en compte a la prochaine session shell)
$path = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($path -notlike "*$nssmDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$path;$nssmDir", "Machine")
}

& "$nssmDir\nssm.exe" version
```

Si `Invoke-WebRequest` echoue (TLS, firewall, proxy), telecharger
manuellement le zip depuis https://nssm.cc/release/nssm-2.24.zip puis
extraire `win64\nssm.exe` vers `C:\Program Files\nssm\nssm.exe`.

### Deploiement binaire

Copier `.\out\webportal\` du poste de dev vers `C:\apps\webportal\`.
Le contenu doit ressembler a :

```text
C:\apps\webportal\
├── apps\webportal\
│   ├── server.js
│   ├── package.json
│   ├── .next\
│   │   └── static\
│   └── public\
├── node_modules\
└── logs\               (a creer)
```

Preparer les logs (memes SIDs langue-neutre que KERMARIA-SRV-02) :

```powershell
New-Item -ItemType Directory -Force -Path C:\apps\webportal\logs
icacls C:\apps\webportal /inheritance:r `
  /grant:r '*S-1-5-32-544:(OI)(CI)F' `
  /grant:r '*S-1-5-18:(OI)(CI)RX' `
  /grant:r 'HOME\svc_api_portal_ad:(OI)(CI)RX'
icacls C:\apps\webportal\logs /grant:r 'HOME\svc_api_portal_ad:(OI)(CI)M'
```

Rappel : le compte AD `HOME\svc_api_portal_ad` doit etre resolvable
(SRV-01 joint au domaine).

### Configuration WEBPORTAL

Meme principe que cote API : toute la config dans un fichier JSON
externe, aucune variable Machine. Node n'a pas de source de config
native comme ASP.NET Core, donc un **wrapper PowerShell**
(`scripts/start-webportal.ps1`) charge le JSON avant d'exec node.

Generation du fichier config depuis le poste de dev, avec le
convertisseur dedie
[`scripts/build-webportal-config.ps1`](../scripts/build-webportal-config.ps1)
(miroir de build-api-config, avec une blocklist plus large qui
exclut les cles server-side seulement — SQL_*, AD_*, BPCE_*, SMTP_*,
LOG_FILE_*, etc.) :

```powershell
.\scripts\build-webportal-config.ps1 `
  -OutputPath \\KERMARIA-SRV-01\C$\ProgramData\Kermaria\webportal.config.json

# Aperçu sans ecriture
.\scripts\build-webportal-config.ps1 -WhatIf
```

Le script ajoute automatiquement des defauts sains si absents du
source : `NODE_ENV=production`, `HOSTNAME=127.0.0.1`, `PORT=3000`.

Contenu attendu (extrait typique) :

```json
{
  "NODE_ENV": "production",
  "HOSTNAME": "127.0.0.1",
  "PORT": "3000",
  "INTERNAL_API_URL": "http://<IP_KERMARIA_SRV_02>:5000",
  "SERVICE_AUTH_TOKEN": "<meme valeur que KERMARIA-SRV-02>",
  "PAYPAL_MODE": "sandbox",
  "PAYPAL_CLIENT_ID": "...",
  "PAYPAL_CLIENT_SECRET": "...",
  "STRIPE_MODE": "test",
  "STRIPE_SECRET_KEY": "...",
  "SIGNUP_ENABLED": "false"
}
```

Sur KERMARIA-SRV-01, ACL restrictive :

```powershell
New-Item -ItemType Directory -Force -Path C:\ProgramData\Kermaria

icacls C:\ProgramData\Kermaria /inheritance:r `
  /grant:r '*S-1-5-32-544:(OI)(CI)F' `
  /grant:r 'HOME\svc_api_portal_ad:(OI)(CI)RX'

icacls C:\ProgramData\Kermaria\webportal.config.json /inheritance:r `
  /grant:r '*S-1-5-32-544:F' `
  /grant:r 'HOME\svc_api_portal_ad:R'
```

Le wrapper `scripts/start-webportal.ps1` (a copier sur SRV-01 dans
`C:\apps\webportal\`) est charge par NSSM. Il lit
`C:\ProgramData\Kermaria\webportal.config.json`, injecte chaque cle
comme env var **de sa session PowerShell**, puis exec
`node C:\apps\webportal\apps\webportal\server.js`. Les env sont
strictement locales au process (jamais Machine).

Copier le wrapper depuis le repo :

```powershell
Copy-Item .\scripts\start-webportal.ps1 `
  \\KERMARIA-SRV-01\C$\apps\webportal\start-webportal.ps1
```

Nettoyer d'anciennes variables Machine si presentes :

```powershell
Get-ChildItem Env: `
  | Where-Object Name -match '^(NODE_ENV|HOSTNAME|PORT|INTERNAL_API_URL|ALLOW_LOCAL_INTERNAL_API_URL|SERVICE_AUTH_TOKEN|SESSION_COOKIE_|PAYPAL_|STRIPE_|SIGNUP_|PUBLIC_VITRINE_|BILLING_|HCAPTCHA_|WEBPORTAL_BASE_URL|PUBLIC_PORTAL_URL)' `
  | ForEach-Object {
      [Environment]::SetEnvironmentVariable($_.Name, $null, 'Machine')
    }
```

### Enregistrement Windows Service via NSSM

NSSM lance **powershell.exe** qui lui-meme lance `start-webportal.ps1`.
`AppDirectory` reste sur `C:\apps\webportal\` (racine du paquet
standalone) pour que `require('next')` resolve le `node_modules`
hoiste — le wrapper laisse le cwd du process Node identique.

Utiliser le chemin absolu de `nssm.exe` pour ne pas dependre du PATH
(le PATH Machine mis a jour a l'install de NSSM n'est visible qu'a
la prochaine session PowerShell). Remplacer `<pwd>` par le vrai mot
de passe du compte AD `HOME\svc_api_portal_ad` avant execution.

```powershell
$nssm = "C:\Program Files\nssm\nssm.exe"
$pshell = (Get-Command powershell.exe).Source

& $nssm install KermariaWebportal $pshell "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File C:\apps\webportal\start-webportal.ps1"
& $nssm set KermariaWebportal AppDirectory "C:\apps\webportal"
& $nssm set KermariaWebportal DisplayName "Kermaria WEBPORTAL"
& $nssm set KermariaWebportal Description "Kermaria Next.js portal front (KERMARIA-SRV-01). Bound to 127.0.0.1:3000, fronted by IIS. Config from C:\ProgramData\Kermaria\webportal.config.json."
& $nssm set KermariaWebportal Start SERVICE_AUTO_START
& $nssm set KermariaWebportal ObjectName "HOME\svc_api_portal_ad" "<pwd>"

# Logs rotatifs
& $nssm set KermariaWebportal AppStdout "C:\apps\webportal\logs\stdout.log"
& $nssm set KermariaWebportal AppStderr "C:\apps\webportal\logs\stderr.log"
& $nssm set KermariaWebportal AppRotateFiles 1
& $nssm set KermariaWebportal AppRotateOnline 1
& $nssm set KermariaWebportal AppRotateBytes 10485760      # 10 Mo
& $nssm set KermariaWebportal AppStopMethodSkip 0
& $nssm set KermariaWebportal AppExit Default Restart
& $nssm set KermariaWebportal AppRestartDelay 5000

Start-Service KermariaWebportal
```

Le wrapper log au demarrage le nombre de cles chargees et les
valeurs de NODE_ENV / HOSTNAME / PORT (jamais les secrets). Erreur
courante : `Config file introuvable` → le fichier config n'est
pas au bon chemin ou l'ACL empeche le service de le lire.

### Verification interne

Depuis KERMARIA-SRV-01 :

```powershell
Invoke-RestMethod http://127.0.0.1:3000/api/health/live
Invoke-RestMethod http://127.0.0.1:3000/api/health/ready
```

Attendu : HTTP 200. `ready` peut echouer si `INTERNAL_API_URL` non
joignable — verifier la route KERMARIA-SRV-01 -> KERMARIA-SRV-02:5000 et la valeur
de `SERVICE_AUTH_TOKEN` identique cote API.

### Pare-feu KERMARIA-SRV-01 (partie WEBPORTAL)

```powershell
# Sortant : uniquement vers KERMARIA-SRV-02 sur 5000
New-NetFirewallRule -DisplayName "API-INTERNAL to KERMARIA-SRV-02" `
  -Direction Outbound -Protocol TCP -RemotePort 5000 `
  -RemoteAddress <IP_KERMARIA_SRV_02> -Action Allow

# Sortant PayPal / Stripe / BPCE / SMTP : uniquement si les modes
# correspondants ne sont pas 'disabled'. Ne pas ouvrir en V0.24 tant
# que le scenario ne le demande pas.
```

## 5. KERMARIA-SRV-01 — IIS + ARR + URL Rewrite

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

## 6. KERMARIA-SRV-01 — TLS via win-acme (Let's Encrypt)

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
| Internet | KERMARIA-SRV-01 | 443 | In | Autorise |
| Internet | KERMARIA-SRV-01 | 80 | In | Autorise (ACME) |
| KERMARIA-SRV-01 | KERMARIA-SRV-02 | 5000 | Out | Autorise |
| KERMARIA-SRV-02 | KERMARIA-SRV-07 | 3306 | Out | Autorise |
| KERMARIA-SRV-01 | Internet 443 | 443 | Out | Autorise si PayPal/Stripe/BPCE/SMTP `!= disabled` |
| Reseau admin | KERMARIA-SRV-01/02/07 | RDP 3389 | In | Autorise depuis VPN uniquement |
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

Sur KERMARIA-SRV-02 :

```powershell
Stop-Service KermariaApiInternal
Rename-Item C:\apps\api-internal C:\apps\api-internal-old-<DATE>
Rename-Item C:\apps\api-internal-staging C:\apps\api-internal
Start-Service KermariaApiInternal
# Verifier /health/ready
# Si OK : Remove-Item -Recurse -Force C:\apps\api-internal-old-<DATE>
# Si KO : Stop, renommage inverse, restart
```

Sur KERMARIA-SRV-01 :

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

1. `Stop-Service KermariaWebportal` sur KERMARIA-SRV-01 (arret trafic
   utilisateur).
2. `Stop-Service KermariaApiInternal` sur KERMARIA-SRV-02.
3. Restaurer les dossiers `-old-<DATE>` par renommage inverse.
4. Restaurer la sauvegarde MariaDB sur KERMARIA-SRV-07 si une migration est
   en cause (procedure detaillee dans
   [`BACKUP_RESTORE.md`](BACKUP_RESTORE.md)).
5. `Start-Service KermariaApiInternal` puis `KermariaWebportal`.
6. Verifier `/health/ready` et `/api/health/ready`.
7. Verifier login client et admin.

## 11. Surveillance minimale

Tache planifiee toutes les 5 min sur un hote de monitoring
(ou KERMARIA-SRV-01 si pas d'hote dedie) :

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

## 12. RAM, CPU et sizing

Materiel disponible :

| Hote | Machine | CPU | RAM |
|---|---|---|---|
| KERMARIA-SRV-01 | Dell Optiplex 5070 | Intel i7-9700 (8c/8t, 3.0-4.7 GHz) | 40 Go DDR4 |
| KERMARIA-SRV-02 | ASUS FX753VD (portable) | Intel i7-7700HQ (4c/8t, 2.8-3.8 GHz) | 32 Go DDR4 |
| KERMARIA-SRV-07 | (existant) | (existant) | (existant) |

Empreinte typique des processus :

| Composant | Hote | RAM active | CPU au ralenti |
|---|---|---|---|
| Windows Server 2022 (avec GUI) | tous | ~1.5 Go | negligeable |
| MariaDB (base < 5 Go) | KERMARIA-SRV-07 | 512 Mo - 1 Go | <1% |
| Kestrel API-INTERNAL | KERMARIA-SRV-02 | 150-300 Mo | <2% |
| Node.js WEBPORTAL | KERMARIA-SRV-01 | 150-300 Mo | <2% |
| IIS worker + ARR | KERMARIA-SRV-01 | 100-200 Mo | <1% |

Consequence :

- **KERMARIA-SRV-01** avec 40 Go a une marge de ~38 Go pour buffers,
  cache disque IIS et pics. Aucune pression memoire attendue.
- **KERMARIA-SRV-02** avec 32 Go peut absorber un dump MariaDB
  temporaire en local (pour transferer vers un stockage tiers) sans
  swap, et heberger a l'avenir un supervisor Grafana/Prometheus
  ou un endpoint de collecte de logs sans redimensionner.
- Le portable ASUS FX753VD implique quelques points d'attention non
  lies au dimensionnement :
  - **batterie** : configurer `powercfg /setacvalueindex … 0` pour
    empecher la mise en veille meme couvercle ferme (Panneau de
    config > Options d'alimentation > Choisir l'action lors de la
    fermeture du capot) ; brancher le secteur en permanence.
  - **GPU GTX 1050 Mobile** : desactiver dans Device Manager pour
    eviter les MAJ NVIDIA Windows Update qui declenchent des
    redemarrages. Non utilise cote serveur.
  - **etat physique** : machine portable = points de defaillance
    en plus (charniere, ecran, clavier). Prevoir sauvegarde du
    disque systeme et plan de bascule vers la cible R740xd dans
    la sequence V1.0 beta 1.

Passer en **Server Core** (sans GUI) n'est **pas necessaire** ici
vu la RAM disponible. Le laisser en mode GUI simplifie la
maintenance ponctuelle (RDP, MMC IIS, gestion certif).

## 13. Suite

Ce runbook couvre la phase de tests KERMARIA-SRV-01/02/07 (V0.24 Brique 1)
et sera adapte comme ossature de la procedure V1.0 beta 1 sur
R740xd (V0.24 Brique 3, livrable
[`PRODUCTION_DEPLOYMENT.md`](PRODUCTION_DEPLOYMENT.md)) avec les
delta suivants :

- comptes de service **AD domain-joined** au lieu de comptes locaux
  (si KERMARIA-SRV-01/02 rejoignent le domaine `home.bzh`) ;
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
