# Environnement DEV isolé — Zachary IT

Mise en place : 2026-09-21. Stack DEV parallèle à la production, sur les mêmes
serveurs, sans modification, redémarrage ni migration de la production.

## 1. Topologie

| Élément | PROD (inchangée) | DEV |
|---|---|---|
| WebPortal (SRV-12) | `kermaria-webportal.service`, `192.168.100.212:3000` | `kermaria-webportal-dev.service`, `192.168.100.212:3100` |
| Utilisateur Linux | `kermaria-web` | `kermaria-web-dev` |
| Releases | `/opt/kermaria/releases/*`, lien `/opt/kermaria/webportal` | `/opt/kermaria/releases-dev/*`, lien `/opt/kermaria/webportal-dev` |
| Env WebPortal | `/etc/kermaria/webportal.env` | `/etc/kermaria/webportal-dev.env` (root 0600) |
| Journaux WebPortal | `/var/log/kermaria/` | `/var/log/kermaria-dev/` |
| API (SRV-13) | `KermariaApiInternal`, `192.168.100.213:5000` | `KermariaApiInternalDev` (« ZacharyIT API Internal DEV »), `192.168.100.213:5100` |
| Compte de service | `HOME\svc-kermaria` | `NT SERVICE\KermariaApiInternalDev` (compte virtuel) |
| Binaires API | `C:\apps\api-internal` | `C:\apps\api-internal-dev` |
| Config API | `C:\ProgramData\Kermaria\api-internal.config.json` + variables Machine | `C:\ProgramData\Kermaria-dev\api-internal.dev.config.json` **seule** |
| Journaux API | `D:\Kermaria\Logs\ApiInternal` | `D:\Kermaria-dev\Logs\ApiInternal` |
| Base (SRV-06) | `kermaria` / `kermaria_api` | `kermaria_dev` / `kermaria_dev` (DML) + `kermaria_dev_migrator` (DDL) |
| Stripe | live | test (désactivé tant que les clés TEST ne sont pas fournies) |
| Outbox / exécuteur | ON | ON |
| Provisioning (AD, KoXo, stockage) | ON | `controlled_write` DEV validé pour l'E2E identité ; stockage headless hors périmètre |
| Email | SMTP OVH réel | SMTP OVH réel, borné par allowlist explicite |
| PayPal / BPCE | live | disabled |
| URL | `zachary-it.fr`, `dashboard.`, `administration.` | `dev.zachary-it.fr` (préparée, **non publiée**) |

`APP_ENV` est orthogonal à `ASPNETCORE_ENVIRONMENT` / `NODE_ENV`. L'API DEV
tourne en `ASPNETCORE_ENVIRONMENT=Staging` : elle garde l'authentification de
service (`X-Service-Auth`), MariaDB obligatoire et les cookies sécurisés. Avec
`Development`, `/internal/*` ne serait plus authentifié.

## 2. Garde-fous

### API (`apps/api-internal/Data/Configuration/DeploymentEnvironmentGuard.cs`)

Évalués au démarrage, avant toute connexion et tout worker. En cas de
violation : log `Critical` « FATAL … » (console JSON et fichier), sortie en
code **78**. Les messages ne contiennent que la famille de clé (`sk_live`…),
jamais la valeur.

`APP_ENV=Development` refuse :

- une clé Stripe `sk_live_` / `rk_live_`, une clé `pk_live_` ou `STRIPE_MODE=live` ;
- `SQL_DATABASE` hors `*_dev` et `SQL_USERNAME` hors `*_dev` / `*_dev_*` ;
- `PAYPAL_MODE=live`, `BPCE_INTEGRATION_MODE=live` ;
- `EMAIL_INTEGRATION_MODE=live` sans allowlist explicite (sans joker) ;
- tout provisioning réel (`BILLING_V2_PROVISIONING_ENABLED`,
  `BILLING_V2_ADDITIONAL_USER_PROVISIONING_ENABLED`,
  `BILLING_V2_SERVICE_FULFILLMENT_ENABLED`, `BILLING_V2_VPS_*`,
  `AD_INTEGRATION_MODE=controlled_write`, `KOXO_SYNC_WEBHOOK_URL`,
  `BILLING_V2_KOXO_STORAGE_URL`) sans **`PROVISIONING_ENABLED=true` et
  `ALLOW_DEV_PROVISIONING=true`** ;
- `ASPNETCORE_ENVIRONMENT=Production`.

Puis, base ouverte : `SELECT DATABASE()` et `SHOW GRANTS FOR CURRENT_USER()`.
Seuls `USAGE ON *.*` et des droits sur la base DEV sont admis : un compte
capable d'atteindre `kermaria`, un rôle ou un privilège global bloque le
démarrage.

`APP_ENV=Production` (ou absent avec `ASPNETCORE_ENVIRONMENT=Production`, cas
de la production actuelle) refuse une clé `sk_test_` / `rk_test_` / `pk_test_`,
`STRIPE_MODE=test`, une base ou un compte `*_dev`.

Résumé écrit au démarrage :

```text
Deployment environment | Environment: Development | Host environment: Staging | Database: kermaria_dev | Stripe mode: test | Stripe key family: sk_test | Outbox executor: enabled | Provisioning: disabled
Deployment environment | SQL account isolation verified on kermaria_dev
```

Autres protections :

- `KERMARIA_CONFIG_AUTHORITATIVE=true` (variable du service DEV) : les
  variables d'environnement non préfixées ne sont plus une source de
  configuration. **Indispensable sur SRV-13**, qui porte en variables Machine
  les `SQL_*`, `AD_*` (mot de passe compris) et `KOXO_SYNC_WEBHOOK_*` de la
  production : sans ce mode, la DEV les hériterait. C'est d'ailleurs ce que le
  garde-fou a bloqué lors de la première migration (`KOXO_SYNC_WEBHOOK_URL`).
- `/internal/webhooks/stripe` refuse (400) un événement dont `livemode` ne
  correspond pas à `STRIPE_MODE` (`STRIPE_LIVEMODE_MISMATCH`) ou qui n'en
  porte pas (`STRIPE_LIVEMODE_MISSING`).
- En-tête `X-Kermaria-App-Env` sur toutes les réponses de l'API.

### WebPortal (`apps/webportal/lib/deployment-environment*.ts`, `instrumentation.ts`)

Au démarrage du serveur Node (même code de sortie 78) :

- `APP_ENV=Development` refuse clés Stripe live, `STRIPE_MODE=live`,
  `PAYPAL_MODE=live`, `STRIPE_WEBHOOK_VERIFY` désactivé, et **toute API qui ne
  s'annonce pas `Development`** (`GET {INTERNAL_API_URL}/health/live`,
  en-tête `X-Kermaria-App-Env`). La production actuelle n'envoie pas cet
  en-tête : un WebPortal DEV pointé sur `:5000` s'arrête.
- `APP_ENV=Production` (ou absent avec `NODE_ENV=production`) refuse les clés
  Stripe de test et `STRIPE_MODE=test`.
- Bandeau permanent « DEVELOPMENT ENVIRONMENT — STRIPE TEST MODE » et
  `X-Robots-Tag: noindex, nofollow` sur toutes les réponses DEV.

Le jeton `SERVICE_AUTH_TOKEN` DEV diffère de celui de la production : même mal
routée, une requête DEV est refusée (401) par l'API PROD.

## 3. Secrets

Fichier **hors Git** `<parent du dépôt>\kermaria-client-platform.dev.env.ps1`
(ACL : utilisateur courant et SYSTEM). Il est distinct de
`kermaria-client-platform.local.env.ps1`, qui porte les clés LIVE : les clés
Stripe TEST et LIVE ne cohabitent jamais dans un même fichier. Les scripts
refusent toute valeur `sk_live_` / `pk_live_` / `rk_live_`.

Sur les serveurs : `api-internal.dev.config.json` (lisible par les
administrateurs et le seul compte virtuel DEV) et `/etc/kermaria/webportal-dev.env`
(root 0600).

## 4. Installation et mise à jour

Toutes les commandes se lancent depuis la racine du dépôt, sur RDC-07.

> **Avertissement opérationnel — 2026-09-28.** Ne pas exécuter
> `scripts/dev-env/Install-ApiInternalDev.ps1` en l'état. Il régénère la
> configuration API DEV depuis une source qui contient encore des valeurs
> LIVE/PROD et pourrait les réinjecter dans DEV. Pour une mise à jour binaire
> autorisée, conserver la configuration DEV existante et effectuer un swap
> staging → ancien dossier → nouveau dossier, puis redémarrer uniquement
> `KermariaApiInternalDev`. Assainir la source DEV/LIVE avant de réutiliser
> l'installateur.

```powershell
# Base et comptes (une fois) : scripts/dev-env/create-dev-database.sql,
# execute sur SRV-06 avec un compte d'administration (mots de passe injectes
# depuis le fichier de secrets DEV, jamais versionnes).

# API DEV — historique : ne pas executer l'installateur ci-dessous tant que
# l'avertissement ci-dessus n'est pas leve.
dotnet publish apps/api-internal/Kermaria.ApiInternal.csproj -c Release -p:UseAppHost=true -o $env:TEMP\api-internal-dev
.\scripts\dev-env\Install-ApiInternalDev.ps1 -PublishDirectory $env:TEMP\api-internal-dev -NoStart
.\scripts\dev-env\Invoke-ApiDevMigrations.ps1            # -SeedDemoData au premier passage
Invoke-Command -ComputerName KERMARIA-SRV-13.home.bzh { Start-Service KermariaApiInternalDev }

# WebPortal DEV : build avec APP_ENV=Development (bandeau aussi sur les pages statiques)
$env:APP_ENV = 'Development'; npm run build --workspace @kermaria/webportal
# puis empaqueter en .tar.gz (meme structure que scripts/pack-webportal-release.ps1)
.\scripts\dev-env\Install-WebportalDev.ps1 -Archive <archive.tar.gz>
```

Ordre : API DEV d'abord (le WebPortal DEV vérifie son identité au démarrage).

### Migration 066 et binlog

SRV-06 est primaire de réplication (`log_bin=1`,
`log_bin_trust_function_creators=0`). MariaDB exige alors `SUPER` pour
`CREATE TRIGGER`, droit que le compte migrateur DEV n'a pas et ne doit pas
avoir. Seule la migration 066 crée des triggers. Pour la DEV, ils ont été créés
par `scripts/dev-env/Complete-DevMigration066.ps1` avec
`DEFINER = kermaria_dev_migrator` : aucun objet DEV ne s'exécute sous une
identité capable d'atteindre `kermaria`. Toute future migration contenant
trigger, fonction ou procédure demandera le même traitement.

La réplication étant sans filtre, `kermaria_dev` et ses comptes sont aussi
répliqués sur SRV-07/SRV-08.

## 5. Stripe TEST (à finaliser)

1. Tableau de bord Stripe, **mode Test** : récupérer `sk_test_…` et `pk_test_…`.
2. Les renseigner dans le fichier de secrets DEV (`DEV_STRIPE_SECRET_KEY`,
   `DEV_STRIPE_PUBLISHABLE_KEY`).
3. Après publication de `dev.zachary-it.fr` : créer en mode Test un endpoint
   `https://dev.zachary-it.fr/api/webhooks/stripe`, distinct de l'endpoint
   live, et reporter son `whsec_…` dans `DEV_STRIPE_WEBHOOK_SECRET`.
4. Relancer `Install-ApiInternalDev.ps1` (API) et `Install-WebportalDev.ps1`
   (WebPortal) : `STRIPE_MODE` passe alors automatiquement à `test`.

## 6. Publication `dev.zachary-it.fr` (à faire après validation)

Modèle : `scripts/dev-env/srv11/dev.zachary-it.fr.conf.template` (non déployé).
Le certificat wildcard existant couvre `*.zachary-it.fr`. La protection se fait
par Cloudflare Access (application sur `dev.zachary-it.fr`, politique Bypass
limitée à `/api/webhooks/stripe`) et par une authentification HTTP nginx. Il
faut aussi ajouter le Public Hostname dans le tunnel Cloudflare (géré à
distance). Les cookies de session et CSRF sont host-only et ne fuient pas entre
`dashboard.zachary-it.fr` et `dev.zachary-it.fr`. Seul le cookie panier est
posé sur `.zachary-it.fr` ; son identifiant n'existe pas dans `kermaria_dev`.

Tant que la DEV n'est pas publiée en HTTPS, la connexion par navigateur sur
`http://192.168.100.212:3100` ne conserve pas la session
(`SESSION_COOKIE_SECURE=true`).

## 7. Vérifications

```powershell
# Identite et sante
ssh kermaria-srv-12 'curl -sI http://192.168.100.213:5100/health/ready | grep -i x-kermaria-app-env'
ssh kermaria-srv-12 'curl -s http://192.168.100.212:3100/api/health/ready'
# Journal de demarrage de l'API DEV
Invoke-Command -ComputerName KERMARIA-SRV-13.home.bzh { Select-String -Path (Get-ChildItem D:\Kermaria-dev\Logs\ApiInternal\*.log | Sort-Object LastWriteTime)[-1] -Pattern 'Deployment environment|FATAL' }
```

```sql
SELECT COUNT(*) FROM kermaria_dev.billing_v2_outbox_events;  -- table outbox du projet
```

## 8. Limites connues

- Le pare-feu Windows de SRV-13 est **désactivé par GPO** (magasin actif :
  `Enabled=False` sur les trois profils). La règle locale
  « Kermaria API DEV 5100 - bloquer hors SRV-12 » existe mais reste sans effet
  tant que la GPO n'est pas revue. Les ports 5000 et 5100 sont donc joignables
  depuis le LAN et le VPN (le VPN arrive par SRV-11) ; seul le jeton de service
  les protège.
- Les garde-fous PROD (refus `sk_test`, base `*_dev`, `livemode=false`) ne sont
  actifs en production qu'au prochain déploiement de ce code.
- `kermaria_api` (compte runtime PROD) détient `ALL PRIVILEGES ON *.* WITH
  GRANT OPTION` : il peut techniquement atteindre `kermaria_dev`. C'est le sens
  PROD → DEV, sans risque pour la production, mais à corriger.
