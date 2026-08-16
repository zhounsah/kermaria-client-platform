---
name: deployment-topology
description: "Topologie CIBLE R740xd (live depuis ~2026-07-23) : ferme Hyper-V ~38 VM sur R740xd (hôte phys. SRV-01, 256 Go RAM, 96 vCPU) + hôtes secondaires Optiplex/FX753vd. Rôles séparés : SRV-12 webportal, SRV-13 API-interne (AD/provisioning), SRV-06/07/08 SQL, SRV-21 AD clients.home.bzh, SRV-30 RDS Clients, SRV-27 RDG, SRV-28 UPD, SRV-24/25 VPN, SRV-11 reverse proxy. L'ancien modèle bare-metal SRV-01/02/07 est PÉRIMÉ (historique en bas)."
metadata: 
  node_type: memory
  type: project
  originSessionId: 2d7207aa-a9f5-4387-aa15-8c308b6f44fb
  modified: 2026-08-04T21:41:38.432Z
---

**RÉÉCRITURE 2026-08-03 — nouvelle topologie R740xd (source : tableau d'infra fourni par l'utilisateur ; toutes les VM déclarées actives). Voir [[infra-r740xd-blocker]] (blocage levé).**

Ferme **Hyper-V** portée par le **R740xd** (hôte physique nommé « SRV-01 » dans le tableau : WS2025 Datacenter, 256 Go RAM, 96 vCPU, NVMe RAID1 1,8 To pour VM sensibles + volumes SAS). Deux hôtes secondaires : Optiplex 5070 (« SRV-02 ») et FX753vd (« SRV-03 »). NAS-KERMARIA (Synology, 8 To, 192.168.100.200) = stockage fichiers/SQL/Icecast.

Numérotation VM **prévisionnelle** SRV-01..SRV-38, IP `192.168.100.2xx`, FQDN `KERMARIA-SRV-xx.home.bzh`. Rôles clés pour la plateforme client :
- **SRV-12** KERMARIA-SRV-12 (Ubuntu, .212, aussi DMZ 192.168.10.212) = **serveur web clients** (vitrine / portail / auth / commandes / factures / demandes).
- **SRV-13** KERMARIA-SRV-13 (WS2025, .213) = **API interne sensible** (actions AD / mot de passe / création comptes / groupes AD / provisioning / audit / SQL).
- **SRV-06/07/08** KERMARIA-SQL-01/02/03 (Ubuntu, MySQL/MariaDB) : SRV-06 principal (Veeam), SRV-07 secondaire, SRV-08 tertiaire. **MAJ 2026-08-03 : la base runtime de l'appli est désormais `kermaria` sur KERMARIA-SRV-06.home.bzh:3306** (comptes `kermaria_api` runtime / `kermaria_migrator` DDL). L'ancienne `test_web`@SRV-07 est abandonnée. Config dans le `.local.env.ps1` de dev (hors repo, à côté du dossier projet — contient des SECRETS LIVE, ne jamais recopier/committer).
- ⚠️ **Env dev local en modes LIVE** (2026-08-03) : `.local.env.ps1` a AD=`controlled_write` sur `clients.home.bzh` (OU=Clients, groupes dans OU=SecurityGroups,OU=Clients ; AD_ALLOWED_ROOTS=OU=Clients ; AD_ALLOWED_GROUPS=TEST_SITE_WEB), et BPCE/Stripe/PayPal/Email tous en `live` (EMAIL_LIVE_ALLOWLIST=*). Conséquence : toute inertie/isolation (ex. comptes démo) doit être imposée PAR LE CODE, pas via des modes globaux non-live. Webhook KoXo = SRV-21 (192.168.100.221:8042).
- **SRV-11** KERMARIA-SRV-11 (Ubuntu, .211 + DMZ) = reverse proxy **Nginx + Cloudflare Tunnel**.
- **AD DS** : forêt `home.bzh` sur SRV-17/18/19 (DC-01/02/03) ; `culturevap.home.bzh` sur SRV-20 (DC-04) ; **`clients.home.bzh` sur SRV-21 (KERMARIA-DC-05, .221)** = domaine des identités clients + cible KoXo. SRV-22 = AD CS.
  - ⚠️ Le PTR de .221 rend **`KERMARIA-SRV-21.clients.home.bzh`** (ni `KERMARIA-SRV-21` ni `KERMARIA-DC-05` ne résolvent depuis RDC-07 ; `clients.home.bzh` résout, lui). **WinRM OK depuis RDC-07 sans mot de passe** (`New-PSSession -ComputerName KERMARIA-SRV-21.clients.home.bzh`), `Copy-Item -ToSession` OK dans `C:\Program Files`.
  - Scripts KoXo déployés **à plat dans `C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\`** (pas de dossier de déploiement dédié) : module, `Sync-KoXoClients.ps1`, receveur webhook, `CLIENTS.xml`, `clients.csv`, `backups\`, `Logs\`. Le déployé pouvait être **très en retard** sur le dépôt (constaté 2026-08-04 : module du 07-30) — depuis, `scripts/koxo/Deploy-KoxoScripts.ps1` fait le déploiement, protège les fichiers du serveur (`CLIENTS.xml`, jeton, CSV, dossiers de données) et pose les variables Machine. **Toujours passer par lui**, jamais par une copie manuelle.
  - ⚠️ **`KOXO_CSV_ENCODING` existe en variable Machine sur SRV-21** : elle **écrase** le défaut du module. Déployer le module corrigé ne suffit donc jamais — vérifier/retirer aussi la variable. Voir [[koxo-accents-majuscules]].
- **RDS** : SRV-29 (RDS-01 Culturevap), **SRV-30 (RDS-02 « Clients-1 »)**, SRV-31/32/33 (RDS Kermaria interne). **SRV-27** KERMARIA-RDG = RD Gateway + Web Access. **SRV-28** KERMARIA-UPD = stockage User Profile Disks RDP (volume secondaire 1024 Go).
- **VPN** : SoftEther SRV-24/25 (VPN-01/02). VLAN **63** = « VPN Clients ».
- Stockage : volume SAS RAID1 dédié **« Stockage dossiers personnels »** (1,8 To) = où poser les quotas FSRM des dossiers clients. Rôles fichiers KERMARIA-FS-01 (NAS) / FS-02 (SRV-01) / FS-03 (SRV-02).
- Supervision SRV-10 (Prometheus), WAC SRV-14, Bastion SRV-15, Veeam SRV-16, Docker SRV-09, Impression SRV-23 (PaperCut).

**VLAN** : 20 Forêt, 61 VPN, 62 VPN CultureVap, **63 VPN Clients**, 90 MGMT, 10 DMZ, 30 IoT, 100 Serveurs, **240 Clients**, **250 Invités**.

⚠️ « VM actives » ≠ « apps migrées ». `docs/DEPLOYMENT_WINDOWS.md` décrit encore l'ancien modèle IIS (à réviser).

**CONFIRMÉ 2026-08-04 (déploiement réel observé) : le WEBPORTAL tourne bien sur SRV-12, en Linux/systemd** — service `kermaria-webportal`, releases dans `/opt/kermaria/releases/<horodatage>`, symlink `/opt/kermaria/webportal`, bascule + rollback par `ln -sfn` (runbook `docs/WEBPORTAL_SRV12_DEPLOYMENT.md`, artefact via `npm run pack:webportal:release`). Front = **nginx** (SRV-11), plus IIS du tout sur le chemin public. Donc :
- l'outbound rule IIS `StripXRobotsTag` de l'ancien modèle **n'existe plus** ; c'est ce qui avait rendu `www.zacharyhounsa.ovh` non indexable (le `noindex` global de `next.config.ts` ne passait plus par aucun strip). Corrigé côté code le 2026-08-04 : `NOINDEX_ROUTE_PREFIXES` + garde-fou `npm run test:seo`. **Ne plus jamais confier l'indexabilité au proxy.**
- nginx ajoute **ses propres** en-têtes de sécurité en doublon de ceux de Node (`X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy` apparaissent deux fois sur `https://www.zacharyhounsa.ovh/`). Conflit à noter : Node envoie `X-Frame-Options: DENY`, nginx `SAMEORIGIN`.

--- HISTORIQUE (ancien bare-metal, PÉRIMÉ) ---

Topologie SRV-01/02/07 fixée le 2026-07-02, matériel confirmé le
2026-07-03, **infra debout et fonctionnelle en staging** le
2026-07-03 :

- **KERMARIA-SRV-01** — Dell Optiplex 5070 (i7-9700 8c/8t, 40 Go
  DDR4). WEBPORTAL Node.js 24 + Next standalone via NSSM Windows
  Service piloté par wrapper `scripts/start-webportal.ps1`. IIS
  front avec deux sites scoped :
  - `kermaria-vitrine` sur `www.home.bzh` + `www.zacharyhounsa.ovh`,
    strippe `X-Robots-Tag` en outbound rule pour indexation SEO.
  - `kermaria-portal` sur `portail.home.bzh` + `dashboard.home.bzh`
    + `portail.zacharyhounsa.ovh` + `dashboard.zacharyhounsa.ovh`,
    redirige `/` → `/login`, conserve `X-Robots-Tag`.
  - Wildcard Let's Encrypt existant `2BC7C742...` réutilisé
    (couvre `*.home.bzh`, `*.zacharyhounsa.ovh`,
    `*.kermaria35580.ovh`), pas de win-acme monté pour l'instant.
  - App pool unique `Kermaria-Webportal` en No Managed Code,
    identité ApplicationPoolIdentity.
  - IIS coexiste avec Default Web Site, RADIO-PROXY, portfolio-zachary
    déjà présents sur la même IP publique 192.168.100.201 —
    séparation par host header + SNI.

- **KERMARIA-SRV-02** — ASUS FX753VD portable (i7-7700HQ 4c/8t,
  32 Go DDR4, GTX 1050 Mobile désactivée dans Device Manager).
  API-INTERNAL dotnet 10 Runtime en Windows Service natif via
  `New-Service` (pas sc.exe create, finicky syntax) +
  `builder.Host.UseWindowsService()`. Écoute sur 192.168.100.202:5000,
  jamais Internet. Portable = points de défaillance physiques
  supplémentaires, `powercfg` configuré pour ignorer la fermeture
  du couvercle, bascule vers R740xd prévue en V1.0 beta 1.

- **KERMARIA-SRV-07** — `kermaria-srv-07.home.bzh` (192.168.100.207).
  MariaDB 11.x. Bind sur `192.168.100.207`, jamais `0.0.0.0`.
  Base réutilisée : **`test_web`** (l'utilisateur avait déjà cette
  base existante côté dev, on l'a récupérée pour staging au lieu
  de créer une base `kermaria` dédiée). Comptes `test_web`
  (runtime, sans DDL) et `kermaria_migrator` (temporaire pour les
  migrations DDL).

**Contraintes utilisateur** (2026-07-02) :
- pas de VM (RAM insuffisante) ;
- Windows Server 2022 bare-metal sur les 3 hôtes ;
- MariaDB déjà sur SRV-07, pas de re-hébergement.

**Compte de service partagé** (décision 2026-07-03) :
`HOME\svc_api_portal_ad`, compte AD pré-existant utilisé pour
KermariaApiInternal (SRV-02) et KermariaWebportal (SRV-01). Les
deux serveurs sont joints au domaine HOME. Les comptes locaux
`svc-kermaria-api` / `svc-kermaria-web` documentés en fallback,
supprimés dans le chemin nominal. Mot de passe [REDACTED] de
recette (valeur faible commençant par « test », non consignée ici)
— à rotate avant sortie V0.24 (le validator `IsPlaceholderSecret`
refuse déjà les creds commençant par "test" en runtime API).

**Prérequis code appliqués le 2026-07-02** (commit `0171298`) :
- `apps/api-internal/Program.cs` : `builder.Host.UseWindowsService()`
  + package NuGet `Microsoft.Extensions.Hosting.WindowsServices 10.0.0`
  dans csproj — sinon le SCM n'arrête pas proprement le process.
- `apps/webportal/next.config.ts` : `output: "standalone"` — sinon
  `next build` produit un `.next/` classique qui exige tout
  `node_modules` en prod (~300 Mo Turbopack, non-viable).

**Config unifiée mono-fichier par app** (patch 2026-07-03) :

`Program.cs` charge `C:\ProgramData\Kermaria\api-internal.config.json`
(chemin overridable via env `KERMARIA_CONFIG_PATH`). Précédence :
`appsettings.json < appsettings.{Env}.json < config.json < env vars < CLI args`.
Le fichier est inséré via `Sources.Insert(envSourceIndex, …)` pour
que les env vars gardent la priorité — permet l'override ad-hoc
pour `--apply-migrations` (SQL_USERNAME=kermaria_migrator en env
session-scope). Fichier optionnel.

**Zéro variable Machine requise** côté API-INTERNAL. L'environnement
(`Staging` / `Production`) est passé via l'argument CLI
`--environment Staging` du service Windows (New-Service
BinaryPathName), parsé par ASP.NET Core dans `CreateBuilder(args)`
avant la lecture du config file.

WEBPORTAL n'a pas d'équivalent Config natif Node → wrapper
`scripts/start-webportal.ps1` lit
`C:\ProgramData\Kermaria\webportal.config.json`, injecte chaque clé
comme env var **de sa session PowerShell** (jamais Machine), puis
exec `node.exe`. Le process Node enfant hérite des env.

Le fichier API contient TOUTE la config runtime (55 clés typiques
extraites depuis `.local.env.ps1` du dev via
`scripts/build-api-config.ps1`) : SQL_*, SERVICE_AUTH_TOKEN, LOG_*,
SESSION_*, LOGIN_*, AD_*, BPCE_*, PAYPAL_*, STRIPE_*, SMTP_*,
EMAIL_*, SIGNUP_*, PUBLIC_VITRINE_*, BILLING_*, HCAPTCHA_*.

Blocklist du convertisseur API (jamais extraites) : DEMO_*,
RUN_MARIADB_TESTS, ALLOW_LOCAL_INTERNAL_API_URL,
ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT, KERMARIA_CONFIG_PATH,
**LOG_FILE_DIRECTORY** (machine-spécifique, injecté par défaut sur
la cible à `C:\apps\api-internal\logs`).

Blocklist supplémentaire WEBPORTAL (via `build-webportal-config.ps1`) :
toutes les clés server-side (SQL_*, AD_*, BPCE_*, SMTP_*, EMAIL_*,
LOG_*, LOGIN_*, SESSION_DURATION_MINUTES) plus les précédentes.

**Override host-spécifique + garde-fou** (patch 2026-07-03, commit
`276f6f2`) : les deux convertisseurs acceptent un param
`-Override @{ CLE = "valeur" }` appliqué APRÈS extraction et defaults,
pour forcer les clés dépendantes de la topologie sans éditer le
`.local.env.ps1` de dev. Cas nominal : `INTERNAL_API_URL` vaut
`http://localhost:5000` en dev (correct) mais doit viser l'IP VLAN
`http://192.168.100.202:5000` de SRV-02 en split-host — sinon en
`NODE_ENV=production` `validateServerRuntimeConfiguration()`
(`apps/webportal/lib/runtime-config.ts`) throw et `/api/health/ready`
renvoie 503. `build-webportal-config.ps1` émet donc un AVERTISSEMENT
au build si `INTERNAL_API_URL` est locale avec `NODE_ENV=production`
(miroir du garde-fou runtime). Côté API, même mécanisme pour
`SQL_HOST` etc. — mais aucun garde-fou runtime : un `SQL_HOST` resté
sur `localhost` échoue silencieusement à la connexion, d'où l'intérêt
de l'`-Override`. Passer une clé blocklistée en `-Override` lève une
erreur.

ACL fichier : `*S-1-5-32-544:F` (Administrateurs) +
`HOME\svc_api_portal_ad:R` uniquement.

**Bootstrap du premier admin** (nouveau flag CLI livré 2026-07-03) :
`--seed-admin` prompt interactif email + display name + mot de
passe (masqué), hash PBKDF2 via `IPortalPasswordService`, insertion
`portal_users` avec role `internal_admin`. Crée un sentinel
customer `INTERNAL` si aucun customer pré-existant. Usable hors
Development (contrairement à `--seed-demo-data`). Aucun credential
ne transite par les args CLI.

**Runbook complet** : [docs/DEPLOYMENT_WINDOWS.md](docs/DEPLOYMENT_WINDOWS.md).
Section 13 "Gotchas rencontrés" liste les pièges concrets du
premier déploiement.

**Why:** l'utilisateur a explicité ces choix après discussion :
Node.js standalone + NSSM plutôt qu'IIS + iisnode (obsolète),
Windows Service natif .NET plutôt que sous IIS ANCM (isole
API-INTERNAL d'IIS et permet à SRV-02 de ne pas installer IIS du
tout), MariaDB sur SRV-07 séparé pour libérer RAM sur SRV-01/02,
réutilisation du wildcard Let's Encrypt et de la stack IIS
préexistante avec cohabitation de plusieurs sites, compte AD
partagé plutôt que locaux pour hygiène credentials centralisée.

**How to apply:** avant tout déploiement ou modification
infrastructure Kermaria, ouvrir DEPLOYMENT_WINDOWS.md. Ne pas
proposer d'alternative Docker/WSL2/Hyper-V — refusé pour raisons
RAM. Ne pas proposer d'installer un SDK sur SRV-01/02 — build sur
poste de dev, copie des artefacts uniquement. Ne pas suggérer
`sc.exe create` pour les services — préférer `New-Service` (moins
finicky). Pour tout hostname `www.*` en script, composer avec
`'w' + 'ww'` pour éviter l'auto-linkification markdown au copier.

**GOTCHA bascule WEBPORTAL (rencontré 2026-07-06, déploiement fix
set-password).** La rename-swap de la section 9 du runbook (`Stop` →
`Rename webportal → -old` → `Rename staging → webportal` → `Start`)
**perd deux éléments** si le dossier `-staging` ne contient QUE le
paquet standalone `out\webportal` (= `apps\`, `node_modules\`) :
- `start-webportal.ps1` — le wrapper que NSSM lance
  (`-File C:\apps\webportal\start-webportal.ps1`). Il vit à la racine
  de `C:\apps\webportal\` mais N'EST PAS produit par `next build`
  standalone. Absent → `Start-Service KermariaWebportal` échoue
  (`StartServiceFailed`, service en état `Paused`).
- le dossier `logs\` avec l'ACL `HOME\svc_api_portal_ad:(OI)(CI)M`
  (NSSM y écrit stdout/stderr + rotation → besoin de Modify, pas juste
  RX hérité de `Users`).
Correctif appliqué : copier `start-webportal.ps1` depuis le `-old`
dans la nouvelle live, recréer `logs\` + `icacls … :(OI)(CI)M`, puis
`Start-Service`. **Pour éviter la rechute : inclure le wrapper + un
`logs\` dans le paquet `-staging` AVANT la bascule.** La section 9 du
runbook ne le mentionne pas — à documenter. Le rename-swap de l'API
(SRV-02) n'a pas ce piège (l'`.exe` publie est autonome). Ordre de
bascule imposé quand le front appelle un nouvel endpoint API : **API
d'abord (SRV-02), webportal ensuite (SRV-01)** ; le nouvel API reste
rétro-compatible avec l'ancien front pendant la fenêtre. Health checks
de bascule : API `http://192.168.100.202:5000/health/ready` (bind IP
VLAN, PAS localhost), WEBPORTAL `http://127.0.0.1:3000/api/health/ready`.
Bascule pilotable à distance en `Invoke-Command -ComputerName` (WinRM
OK depuis le poste de dev, SMB `C$` accessible).

Voir aussi [[roadmap-current]], [[infra-r740xd-blocker]].
