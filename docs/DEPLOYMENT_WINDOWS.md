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
qui derive le JSON depuis un `.local.env.ps1` (formes supportees :
`$env:KEY = "value"` et `Set-Item -Path 'Env:KEY-WITH-HYPHEN' -Value 'value'`,
blocklist des cles interdites, n'affiche jamais les valeurs) :

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

Clés host-spécifiques : si une valeur du `.local.env.ps1` de dev
diffère de la cible (typiquement `SQL_HOST`, `localhost` en dev vs
`192.168.100.207` de SRV-07), la forcer au build via `-Override`
plutôt que d'éditer le source de dev :

```powershell
.\scripts\build-api-config.ps1 `
  -OutputPath \\KERMARIA-SRV-02\C$\ProgramData\Kermaria\api-internal.config.json `
  -Override @{ SQL_HOST = "192.168.100.207" }
```

Contrairement à `INTERNAL_API_URL` côté WEBPORTAL, aucune de ces
clés API n'a de garde-fou runtime qui rejette une valeur locale : un
`SQL_HOST` resté sur `localhost` échoue silencieusement à la
connexion MariaDB (`/health/ready` KO) plutôt qu'avec un message
explicite — d'où l'intérêt de l'`-Override` au build.

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
# Split-host : INTERNAL_API_URL DOIT viser l'IP VLAN de SRV-02, jamais
# localhost. Le -Override force la bonne valeur sans toucher au
# .local.env.ps1 de dev (ou localhost:5000 reste correct pour le dev local).
.\scripts\build-webportal-config.ps1 `
  -OutputPath \\KERMARIA-SRV-01\C$\ProgramData\Kermaria\webportal.config.json `
  -Override @{ INTERNAL_API_URL = "http://192.168.100.202:5000" }

# Aperçu sans ecriture
.\scripts\build-webportal-config.ps1 -WhatIf
```

Le script ajoute automatiquement des defauts sains si absents du
source : `NODE_ENV=production`, `HOSTNAME=127.0.0.1`, `PORT=3000`.

> **INTERNAL_API_URL en split-host — piege 503.** Le WEBPORTAL tourne
> sur SRV-01, l'API sur SRV-02 bindée sur `192.168.100.202:5000`.
> `INTERNAL_API_URL` doit donc valoir `http://192.168.100.202:5000`
> (IP VLAN de SRV-02), **jamais** `localhost:5000`. En
> `NODE_ENV=production` une URL locale fait throw
> `validateServerRuntimeConfiguration()`
> ([apps/webportal/lib/runtime-config.ts](../apps/webportal/lib/runtime-config.ts))
> et `/api/health/ready` renvoie 503. `ALLOW_LOCAL_INTERNAL_API_URL`
> est blocklistée par le générateur (jamais écrite dans le JSON) : on
> **ne** la met **pas** à `true` pour contourner — on corrige l'URL.
> Le `.local.env.ps1` de dev garde `localhost:5000` (correct en dev
> local) ; c'est le `-Override` ci-dessus qui cible SRV-02 au build
> staging/prod. Le générateur émet un `AVERTISSEMENT` explicite s'il
> détecte une INTERNAL_API_URL locale avec `NODE_ENV=production`.

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
  "SIGNUP_ENABLED": "false",
  "HCAPTCHA_SITE_KEY": "<site_key>",
  "HCAPTCHA_SECRET_KEY": "<secret_key>"
}
```

> **hCaptcha — pre-requis des que `SIGNUP_ENABLED=true` (recette reelle
> ou prod).** Le signup est **fail-closed** : en `NODE_ENV=production`,
> sans `HCAPTCHA_SECRET_KEY` reel (ou avec un placeholder), *toute*
> soumission `/signup` est refusee (`CAPTCHA_MISCONFIGURED`). Provisionner :
> - un site hCaptcha reel ; `HCAPTCHA_SITE_KEY` (publique, injectee dans
>   le formulaire) et `HCAPTCHA_SECRET_KEY` (server-only) **doivent
>   provenir du meme site** — un couple site/secret depareille renvoie
>   `sitekey-secret-mismatch` et le siteverify echoue en 400
>   `CAPTCHA_FAILED` malgre un token valide ;
> - les **hostnames autorises** du site hCaptcha doivent inclure le
>   domaine servant le formulaire : `portail.home.bzh` (+ `dashboard.home.bzh`
>   et les hosts `*.zacharyhounsa.ovh` si le formulaire y est expose).
>   Un hostname absent renvoie une reponse siteverify avec le champ
>   `hostname` non conforme ;
> - **derriere ARR, ne pas s'appuyer sur `remoteip`.** Le WEBPORTAL ne
>   transmet `remoteip` a hCaptcha que si l'IP client (premier segment de
>   `X-Forwarded-For`) est **publique** ; une IP LAN privee (acces interne
>   a `portail.home.bzh`) est volontairement omise, car elle divergerait
>   de l'IP vue par hCaptcha lors de la resolution du widget et ferait
>   rejeter le siteverify. Aucune config ARR supplementaire n'est requise.
>
> Les **cles de test** hCaptcha (`10000000-ffff-ffff-ffff-000000000001`
> / `0x0000000000000000000000000000000000000000`) passent en direct mais
> ignorent `remoteip` : elles ne reproduisent pas le comportement reel et
> ne valident pas ce pre-requis. Toujours recetter avec de **vraies cles**.
>
> Un echec de verification est desormais journalise sur `stderr`
> (`event: "hcaptcha_verify_failed"`, avec `error_codes`, `hostname`,
> `remoteip_sent`, `correlation_id`) — cf. section "Verification des logs
> NSSM" pour le lire. Aucun secret ni token n'est journalise.

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

### Verification des logs NSSM (stdout/stderr)

Sans ces logs, tout diagnostic applicatif en prod est impossible
(ex. sous-code d'echec hCaptcha, cf. section signup). Verifier apres
`Start-Service` :

```powershell
# 1. Les parametres I/O sont bien poses SUR LE SERVICE installe.
#    (Un service installe avant les `nssm set AppStdout/AppStderr`
#     n'ecrit rien tant qu'on ne les repose pas + restart.)
$nssm = "C:\Program Files\nssm\nssm.exe"
& $nssm get KermariaWebportal AppStdout      # -> C:\apps\webportal\logs\stdout.log
& $nssm get KermariaWebportal AppStderr      # -> C:\apps\webportal\logs\stderr.log

# 2. Le dossier existe ET le compte de service peut y ecrire (Modify).
Test-Path C:\apps\webportal\logs
(Get-Acl C:\apps\webportal\logs).Access |
  Where-Object IdentityReference -match 'svc_api_portal_ad'

# 3. Les fichiers se remplissent (le wrapper ecrit au demarrage).
Get-Content C:\apps\webportal\logs\stdout.log -Tail 20
Get-Content C:\apps\webportal\logs\stderr.log -Tail 20
```

> **Fichiers `stdout.log` / `stderr.log` absents ou vides.** Causes,
> par ordre de frequence :
> 1. **Dossier non provisionne / non inscriptible.** NSSM ne peut pas
>    creer le fichier si `C:\apps\webportal\logs` n'existe pas ou si
>    `HOME\svc_api_portal_ad` n'a pas `Modify` dessus (voir la creation
>    du dossier + `icacls ... :(OI)(CI)M` plus haut). Rejouer ces deux
>    commandes puis `Restart-Service KermariaWebportal`.
> 2. **Parametres I/O non appliques au service installe.** Reposer
>    `AppStdout`/`AppStderr` (+ `AppRotateFiles 1`) puis redemarrer.
> 3. **Log du wrapper avale.** Le wrapper ecrit desormais via
>    `[Console]::Out/Error` (et non `Write-Host`, dont le flux
>    Information n'atteint pas toujours le handle redirige sous un hote
>    de service non-interactif). Un ancien `start-webportal.ps1` sur
>    SRV-01 peut encore utiliser `Write-Host` — recopier la version du
>    repo (`scripts/start-webportal.ps1`).
>
> Les logs applicatifs Next.js (`console.error`, `process.stderr.write`
> de `lib/bff-observability.ts` et de la verification hCaptcha)
> heritent des memes handles que le wrapper : une fois les 3 points
> ci-dessus valides, ils apparaissent dans `stderr.log`.

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

Deux sites IIS distincts sur la meme IP, chacun avec plusieurs
host headers, tous deux reverse-proxy vers le meme Node loopback :

| Site | Hostnames | Role | Header noindex |
|---|---|---|---|
| `kermaria-vitrine` | `www.home.bzh`, `www.zacharyhounsa.ovh` | Vitrine publique V0.27 (landing, `/offres`, `/contact`, `/portfolio/*`, etc.) | strippe |
| `kermaria-portal` | `portail.home.bzh`, `dashboard.home.bzh`, `portail.zacharyhounsa.ovh`, `dashboard.zacharyhounsa.ovh` | Backoffice authentifie (`/login`, `/dashboard`, `/admin/*`, `/api/*`) | conserve |

Node fait deja le routing par path (V0.27 `proxy.ts` bascule
`PublicShell` / `AppShell`). Les deux sites IIS pointent donc sur
le meme process `127.0.0.1:3000` — la separation IIS ne sert qu'a
la lisibilite operationnelle et au controle du header
`X-Robots-Tag`.

### Installation

```powershell
Install-WindowsFeature Web-Server, Web-Common-Http, Web-Static-Content, `
  Web-Http-Redirect, Web-Http-Logging, Web-Custom-Logging, `
  Web-Filtering, Web-Windows-Auth, Web-Basic-Auth, `
  Web-Mgmt-Tools, Web-Mgmt-Console
```

Installer **URL Rewrite 2.1** et **Application Request Routing 3.0**
via les installeurs MSI officiels de iis.net (ou automatiser
depuis `Invoke-WebRequest` sur les liens ci-dessous) :

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

Whitelist des server variables custom (necessaire pour que les
rules puissent `<set name="HTTP_X_FORWARDED_*" />`) :

```powershell
$existing = @(Get-WebConfiguration -pspath 'MACHINE/WEBROOT/APPHOST' `
  -filter "system.webServer/rewrite/allowedServerVariables/add" -ErrorAction SilentlyContinue |
  Select-Object -ExpandProperty name)
foreach ($v in 'HTTP_X_FORWARDED_PROTO','HTTP_X_FORWARDED_HOST') {
    if ($v -notin $existing) {
        Add-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
          -filter "system.webServer/rewrite/allowedServerVariables" `
          -name "." -value @{name=$v}
    }
}
```

### App pool partage `Kermaria-Webportal`

Un seul app pool en **No Managed Code** (les sites ne servent que
du reverse proxy, aucun runtime .NET dans IIS) :

```powershell
$AppPoolName = "Kermaria-Webportal"
if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName
}
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedPipelineMode  -Value "Integrated"
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.identityType -Value "ApplicationPoolIdentity"
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name startMode -Value "AlwaysRunning"
```

### Site `kermaria-portal` (backoffice)

Le site backoffice **redirige `/` vers `/login`** pour eviter
d'exposer la landing marketing aux hostnames `portail.*` /
`dashboard.*`.

```powershell
$Ip         = "192.168.100.201"    # IP publique du serveur
$CertThumb  = "<empreinte du wildcard *.home.bzh + *.zacharyhounsa.ovh>"
$PortalPath = "C:\inetpub\kermaria"
$PortalHosts = @(
    "portail.home.bzh", "dashboard.home.bzh",
    "portail.zacharyhounsa.ovh", "dashboard.zacharyhounsa.ovh"
)

New-Item -ItemType Directory -Force -Path $PortalPath | Out-Null

New-Website -Name "kermaria-portal" -PhysicalPath $PortalPath `
  -ApplicationPool "Kermaria-Webportal" `
  -IPAddress $Ip -Port 80 -HostHeader $PortalHosts[0] -Force

foreach ($h in $PortalHosts) {
    if (-not (Get-WebBinding -Name "kermaria-portal" -Port 80 -HostHeader $h -Protocol http -ErrorAction SilentlyContinue)) {
        New-WebBinding -Name "kermaria-portal" -Protocol http `
          -IPAddress $Ip -Port 80 -HostHeader $h
    }
    if (-not (Get-WebBinding -Name "kermaria-portal" -Port 443 -HostHeader $h -Protocol https -ErrorAction SilentlyContinue)) {
        New-WebBinding -Name "kermaria-portal" -Protocol https `
          -IPAddress $Ip -Port 443 -HostHeader $h -SslFlags 1
        (Get-WebBinding -Name "kermaria-portal" -Port 443 -HostHeader $h -Protocol https).AddSslCertificate($CertThumb, "My")
    }
}
Start-Website -Name "kermaria-portal"
```

`web.config` du site portal (`C:\inetpub\kermaria\web.config`) :

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <!-- Laisse passer ACME HTTP-01 en clair au cas ou -->
        <rule name="AcmeChallenge" stopProcessing="true">
          <match url="^\.well-known/acme-challenge/(.*)$" />
          <action type="None" />
        </rule>

        <rule name="ForceHttps" stopProcessing="true">
          <match url="(.*)" />
          <conditions>
            <add input="{HTTPS}" pattern="off" />
          </conditions>
          <action type="Redirect" url="https://{HTTP_HOST}/{R:1}" redirectType="Permanent" />
        </rule>

        <!-- / → /login pour les hostnames backoffice -->
        <rule name="RootToLogin" stopProcessing="true">
          <match url="^$" />
          <action type="Redirect" url="/login" redirectType="Found" />
        </rule>

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
      </customHeaders>
    </httpProtocol>

    <security>
      <requestFiltering>
        <requestLimits maxAllowedContentLength="10485760" />
      </requestFiltering>
    </security>
  </system.webServer>
</configuration>
```

### Site `kermaria-vitrine` (public, indexable)

Le site vitrine **strippe le header `X-Robots-Tag`** (envoye par
Node globalement via `next.config.ts`, heritage V0.23) pour que les
pages publiques soient indexables. Le backoffice le conserve.

```powershell
$VitrineSite  = "kermaria-vitrine"
$VitrinePath  = "C:\inetpub\kermaria-vitrine"
$VitrineHosts = @("www.home.bzh", "www.zacharyhounsa.ovh")

New-Item -ItemType Directory -Force -Path $VitrinePath | Out-Null

New-Website -Name $VitrineSite -PhysicalPath $VitrinePath `
  -ApplicationPool "Kermaria-Webportal" `
  -IPAddress $Ip -Port 80 -HostHeader $VitrineHosts[0] -Force

foreach ($h in $VitrineHosts) {
    if (-not (Get-WebBinding -Name $VitrineSite -Port 80 -HostHeader $h -Protocol http -ErrorAction SilentlyContinue)) {
        New-WebBinding -Name $VitrineSite -Protocol http `
          -IPAddress $Ip -Port 80 -HostHeader $h
    }
    if (-not (Get-WebBinding -Name $VitrineSite -Port 443 -HostHeader $h -Protocol https -ErrorAction SilentlyContinue)) {
        New-WebBinding -Name $VitrineSite -Protocol https `
          -IPAddress $Ip -Port 443 -HostHeader $h -SslFlags 1
        (Get-WebBinding -Name $VitrineSite -Port 443 -HostHeader $h -Protocol https).AddSslCertificate($CertThumb, "My")
    }
}
Start-Website -Name $VitrineSite
```

`web.config` du site vitrine (`C:\inetpub\kermaria-vitrine\web.config`) :

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="AcmeChallenge" stopProcessing="true">
          <match url="^\.well-known/acme-challenge/(.*)$" />
          <action type="None" />
        </rule>

        <rule name="ForceHttps" stopProcessing="true">
          <match url="(.*)" />
          <conditions>
            <add input="{HTTPS}" pattern="off" />
          </conditions>
          <action type="Redirect" url="https://{HTTP_HOST}/{R:1}" redirectType="Permanent" />
        </rule>

        <rule name="ReverseProxyToNext" stopProcessing="true">
          <match url="(.*)" />
          <action type="Rewrite" url="http://127.0.0.1:3000/{R:1}" />
          <serverVariables>
            <set name="HTTP_X_FORWARDED_PROTO" value="https" />
            <set name="HTTP_X_FORWARDED_HOST" value="{HTTP_HOST}" />
          </serverVariables>
        </rule>
      </rules>

      <outboundRules>
        <!-- Node envoie X-Robots-Tag: noindex, nofollow globalement (next.config.ts,
             heritage V0.23). Sur la vitrine on veut etre indexable. -->
        <rule name="StripXRobotsTag">
          <match serverVariable="RESPONSE_X_Robots_Tag" pattern=".+" />
          <action type="Rewrite" value="" />
        </rule>
      </outboundRules>
    </rewrite>

    <httpProtocol>
      <customHeaders>
        <remove name="X-Powered-By" />
      </customHeaders>
    </httpProtocol>

    <security>
      <requestFiltering>
        <requestLimits maxAllowedContentLength="10485760" />
      </requestFiltering>
    </security>
  </system.webServer>
</configuration>
```

### DNS et hostnames

- **`*.home.bzh`** : DNS interne sur le DC AD (SRV-03 dans notre
  topologie). Ajouter les A records `www`, `portail`, `dashboard`
  → `192.168.100.201`.
- **`*.zacharyhounsa.ovh`** : DNS public OVH. CNAME ou A records
  `www`, `portail`, `dashboard` vers l'IP WAN + port forward
  80/443 vers `192.168.100.201`.

Le certificat wildcard Let's Encrypt en place couvre deja
`*.home.bzh`, `*.zacharyhounsa.ovh` et `*.kermaria35580.ovh` — sa
reutilisation evite un cert propre par sous-domaine. Voir section
6 pour le renouvellement.

### Verification depuis SRV-01

Depuis une session PowerShell 5.1 elevee sur SRV-01, avec le hosts
file ou le DNS interne resolvant les hostnames sur `192.168.100.201` :

```powershell
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

# Backoffice : / → /login
$b = Invoke-WebRequest -Uri "https://portail.home.bzh/" `
  -MaximumRedirection 0 -UseBasicParsing
$b.StatusCode                        # 302
$b.Headers.Location                  # https://portail.home.bzh/login

# Vitrine : landing V0.27 avec X-Robots-Tag strippe
$v = Invoke-WebRequest -Uri "https://www.home.bzh/" -UseBasicParsing
$v.StatusCode                        # 200
$v.Headers['X-Robots-Tag']           # vide → outbound rule OK
$v.Content -match "claire et utile"  # True (hero V0.27)
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

> **Piege (rencontre 2026-07-06).** Le paquet standalone produit par
> `next build` (section 1) ne contient QUE `apps\` + `node_modules\`.
> Il n'inclut NI le wrapper `start-webportal.ps1` que NSSM lance
> (`-File C:\apps\webportal\start-webportal.ps1`), NI le dossier `logs\`
> (avec son ACL `svc_api_portal_ad:(OI)(CI)M` requise pour que NSSM y
> ecrive stdout/stderr). Une bascule par simple renommage les **perd**
> → `Start-Service KermariaWebportal` echoue (`StartServiceFailed`,
> service en etat `Paused`). Avant de renommer, semer ces deux elements
> dans `webportal-staging` (copie depuis la live courante) :

```powershell
# Completer le -staging avec les elements hors paquet standalone
Copy-Item C:\apps\webportal\start-webportal.ps1 `
  C:\apps\webportal-staging\start-webportal.ps1 -Force
New-Item -ItemType Directory -Force -Path C:\apps\webportal-staging\logs | Out-Null
icacls C:\apps\webportal-staging\logs /grant:r 'HOME\svc_api_portal_ad:(OI)(CI)M'

# Bascule
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

## 13. Gotchas rencontres a l'installation (2026-07-03)

Points de vigilance decouverts pendant le premier deploiement reel
sur KERMARIA-SRV-01/02/07. Utile pour eviter de perdre du temps
en cas de re-installation ou de migration vers le R740xd.

### API-INTERNAL

- **`LOG_FILE_DIRECTORY` machine-specifique** : le convertisseur
  `scripts/build-api-config.ps1` bloque la variable en source
  (`.local.env.ps1` du dev) et injecte automatiquement le default
  cible `C:\apps\api-internal\logs`. Si le config.json est edite a
  la main plus tard avec un chemin qui n'existe pas sur la cible,
  `FileLoggerProvider` throw `UnauthorizedAccessException` au start
  du service — le SCM le rebalance en boucle sans jamais ecrire le
  log applicatif.
- **Migrations `--apply-migrations` gated a Development** : override
  session-scope `$env:ASPNETCORE_ENVIRONMENT="Development"` +
  `$env:SQL_USERNAME=kermaria_migrator` le temps de la commande, le
  process quitte apres migration donc l'API ne demarre jamais en
  mode Dev. Fermer la fenetre PowerShell pour purger les env
  locaux.
- **Migrations 004/005/006 avaient un INSERT redondant** dans leur
  propre SQL sur `schema_migrations` — corrige, mais si un
  environnement dev pre-2026-07-03 avait applique la version
  buggee, le premier `--apply-migrations` reproduira "Duplicate
  entry" sur ces IDs. Purger manuellement via `DELETE FROM
  schema_migrations WHERE migration_id IN (...)` puis rejouer.
- **`RuntimeConfigurationValidator.IsPlaceholderSecret` refuse
  toute valeur commencant par "test"** (garde-fou anti-dev-creds
  en staging/prod). Si tu vois "Configuration invalide :
  SQL_PASSWORD, AD_SERVICE_ACCOUNT_PASSWORD", tes creds commencent
  par "Test..." ou "test..." — rotate cote MariaDB et AD, puis
  regenere le config JSON.
- **`AD_INTEGRATION_MODE=disabled` en V0.24** : le validator ne
  check `AD_SERVICE_ACCOUNT_PASSWORD` que si le mode est
  `read_only`/`controlled_write`. Sur un deploiement staging
  V0.24, laisser `disabled` evite ce check.
- **Bootstrap du 1er admin** : `--seed-admin` prompt interactif
  email + mot de passe (mdp masque, jamais loggue). Cree un
  sentinel customer `INTERNAL` si aucun customer n'existe.
  Usable hors Development (contrairement a `--seed-demo-data`).

### WEBPORTAL

- **Wrapper PowerShell obligatoire** : Node n'a pas de source de
  config native, `scripts/start-webportal.ps1` charge le JSON en
  env vars de session puis exec `node server.js`. Aucune pollution
  Machine.
- **Layout standalone Next monorepo-aware** : parce que
  `next.config.ts` declare `turbopack.root = repo racine`,
  `.next/standalone/` sort `apps/webportal/server.js` +
  `node_modules/` a la racine. `AppDirectory` NSSM sur
  `C:\apps\webportal`, chemin server.js sur
  `C:\apps\webportal\apps\webportal\server.js`.
- **PUBLIC_VITRINE_ENABLED** : par defaut `false`, mettre `true`
  dans `webportal.config.json` + `Restart-Service KermariaWebportal`
  pour activer les routes vitrine V0.27. Sinon `/`, `/offres`,
  `/contact` etc. retournent 404 malgre les bindings IIS.
- **`INTERNAL_API_URL=localhost:5000` regenere en split-host → 503**
  (V0.24 Brique 1) : le `.local.env.ps1` de dev porte
  `INTERNAL_API_URL=http://localhost:5000` (correct en dev local).
  Regenerer la config staging depuis ce source sans override
  reinjecte `localhost`, or SRV-02 est bindee sur
  `192.168.100.202:5000`. En `NODE_ENV=production` `runtime-config.ts`
  throw sur une URL locale (sauf `ALLOW_LOCAL_INTERNAL_API_URL=true`,
  blocklistee, a ne PAS activer) et `/api/health/ready` renvoie 503.
  Corriger avec `-Override @{ INTERNAL_API_URL =
  'http://192.168.100.202:5000' }` (voir section KERMARIA-SRV-01 /
  Configuration WEBPORTAL). Le generateur avertit desormais au build
  s'il detecte cette combinaison.

### IIS

- **ARR proxy `enabled=true`** obligatoire au niveau serveur —
  sans ca, URL Rewrite ignore silencieusement les rules `Rewrite
  url="http://..."` (aucune erreur, requetes 404).
- **Whitelist des server variables** au niveau serveur pour
  `HTTP_X_FORWARDED_PROTO` et `HTTP_X_FORWARDED_HOST`. Si non fait,
  URL Rewrite ignore les `<set>` dans les rules.
- **App pool en `No Managed Code`** : les sites reverse proxy
  n'executent aucun code .NET dans IIS. Un pool avec
  `managedRuntimeVersion=""` limite la surface d'attaque.
- **SNI actif (`SslFlags=1`)** sur les bindings 443 permet la
  cohabitation avec les autres sites de meme IP (Default Web Site,
  portfolio-zachary, RADIO-PROXY dans notre topologie). Les
  clients TLS modernes envoient tous SNI. Test `Invoke-WebRequest
  https://192.168.100.201/` avec `-Headers @{Host='...'}` echoue
  au TLS handshake **parce qu'IIS ne peut pas mapper le SNI depuis
  l'IP nue** — tester avec `https://<hostname>/` (via hosts file
  ou DNS interne) pour que le SNI soit correctement envoye.

### PowerShell 5.1

- **`ServerCertificateValidationCallback = { $true }` bugge** en
  session PS 5.1 non-interactive : `PSInvalidOperationException:
  Il n'y a pas d'instance d'execution disponible`. Contournement :
  ajouter la classe `ServerCertificateValidator` via `Add-Type` et
  passer sa methode statique. Souvent inutile si le certif est
  trusted (cas Let's Encrypt wildcard).
- **`WriteAllLines(path, [string], encoding)` avec un scalaire**
  choisit le mauvais overload et ecrit tout sur une ligne. Forcer
  le type array : `[string[]]$lines`. Symptome typique : hosts
  file avec 4 entrees concatenees sans newline.
- **`Set-Content` sur un fichier en cours de lecture par la meme
  session** echoue avec "Le flux ne peut pas etre lu". Utiliser
  `[System.IO.File]::ReadAllLines` + `WriteAllLines` qui ferment
  le handle immediatement.
- **Comptes de service et `icacls`** : les grants sur
  `svc-kermaria-*` ou `HOME\svc_api_portal_ad` echouent avec "Le
  mappage entre les noms de compte et les ID de securite n'a pas
  ete effectue" si le compte n'existe pas encore. Creer d'abord
  via `New-LocalUser` ou verifier `Get-ADUser` avant les ACL.
- **`Administrators` vs `Administrateurs`** : Windows FR nomme le
  groupe local `Administrateurs`. `Administrator` (compte) et
  `Administrateurs` (groupe) sont distincts. Utiliser le SID
  langue-neutre `*S-1-5-32-544` dans `icacls /grant:r` pour eviter
  toute confusion.
- **`sc.exe create` finicky** : espace obligatoire apres chaque
  `parametre= `, echappements guillemets pour `binPath=`, echec
  silencieux qui affiche l'aide. Preferer `New-Service` (quoting
  PS natif).

### Documentation et copy-paste

- **Chat / rendu markdown auto-linkifie les hostnames en `www.*`**
  au moment du copy — la copie d'une commande `www.home.bzh`
  depuis un rendu markdown produit `[www.home.bzh](https://www.home.bzh)`
  en clipboard. Impact : les hostnames pastes dans PowerShell
  contiennent des crochets litteraux, bindings IIS et hosts file
  corrompus. Contournement dans les scripts :
  `$prefix = 'w' + 'ww'; "${prefix}.home.bzh"`. Verification via
  base64 : `[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(...))`
  puis decodage cote humain.

## 14. Suite

Ce runbook couvre la phase de tests KERMARIA-SRV-01/02/07 (V0.24 Brique 1)
et sera adapte comme ossature de la procedure V1.0 beta 1 sur
R740xd (V0.24 Brique 3, livrable
[`PRODUCTION_DEPLOYMENT.md`](PRODUCTION_DEPLOYMENT.md)) avec les
delta suivants :

- comptes de service **AD domain-joined** au lieu de comptes locaux
  (deja fait cote SRV-01/02 via `HOME\svc_api_portal_ad`) ;
- migration secrets vers DPAPI ou secret store dedie (staging
  actuel : fichier JSON avec ACL restrictive) ;
- TLS MariaDB active (`REQUIRE SSL`) ;
- bascule des modes vers `live` selon l'ordre documente en
  V0.24 Brique 3 ;
- HSTS et HSTS preload apres validation ;
- retention des logs et sauvegardes selon la politique
  d'exploitation definitive.

Voir aussi [`DEPLOYMENT.md`](DEPLOYMENT.md), [`SECURITY.md`](SECURITY.md),
[`SECRET_ROTATION.md`](SECRET_ROTATION.md), [`OPERATIONS.md`](OPERATIONS.md),
[`BACKUP_RESTORE.md`](BACKUP_RESTORE.md).
