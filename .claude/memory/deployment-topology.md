---
name: deployment-topology
description: "Topologie déploiement Kermaria staging debout depuis 2026-07-03 : SRV-01 (WEBPORTAL + IIS split kermaria-vitrine/kermaria-portal), SRV-02 (API-INTERNAL), SRV-07 = kermaria-srv-07.home.bzh 192.168.100.207 (MariaDB), Windows Server 2022 sans VM. Compte AD HOME\\svc_api_portal_ad. Runbook : docs/DEPLOYMENT_WINDOWS.md."
metadata: 
  node_type: memory
  type: project
  originSessionId: 2d7207aa-a9f5-4387-aa15-8c308b6f44fb
---

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
supprimés dans le chemin nominal. Mot de passe temporaire de
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

Voir aussi [[roadmap-current]], [[infra-r740xd-blocker]].
